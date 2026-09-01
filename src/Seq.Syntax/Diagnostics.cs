// Copyright © Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Diagnostics.Metrics;

namespace Seq.Syntax;

/// <summary>
/// Counters for the broad categories of bad-data condition that evaluation and rendering suppress
/// rather than surface: a caught exception, a clamped value, or dropped output. Emitted through the
/// <c>Seq.Syntax</c> meter. Steady increments point at malformed events reaching the pipeline.
/// </summary>
static class Diagnostics
{
    static readonly Meter Meter = new("Seq.Syntax", typeof(Diagnostics).Assembly.GetName().Version?.ToString());

    public static class TagNames
    {
        public const string ErrorKind = "seq_syntax.error_kind";
    }

    /// <summary>
    /// Values for the <see cref="TagNames.ErrorKind"/> tag on <see cref="SuppressedErrors"/>.
    /// </summary>
    public static class ErrorKinds
    {
        // A JSON number, or a computed double (∞/NaN from `^`), that falls outside `decimal`.
        public const string NumericRange = "numeric_range";

        // A `decimal` operation that overflowed.
        public const string ArithmeticOverflow = "arithmetic_overflow";

        // A string holding invalid UTF-16 (e.g. a lone surrogate) that couldn't be decoded.
        public const string MalformedString = "malformed_string";

        // An invalid format specifier applied to a value.
        public const string InvalidFormat = "invalid_format";

        // A regular expression driven to its match timeout by adversarial input.
        public const string RegexTimeout = "regex_timeout";

        // A `FromJson()` argument that couldn't be parsed as JSON.
        public const string InvalidJson = "invalid_json";

        // A comparison or render abandoned because the data nested too deeply for the stack.
        public const string RecursionDepth = "recursion_depth";
    }

    public static readonly Counter<long> SuppressedErrors = Meter.CreateCounter<long>(
        "seq_syntax.evaluation.suppressed_errors",
        unit: "{error}",
        description: "The number of bad-data conditions that were caught and degraded to an undefined or empty result, tagged by kind.");

    public static readonly Counter<long> ClampedAlignmentWidths = Meter.CreateCounter<long>(
        "seq_syntax.rendering.clamped_alignment_widths",
        unit: "{clamp}",
        description: "The number of message-template alignment widths clamped to the maximum.");

    public static readonly Counter<long> TruncatedMessages = Meter.CreateCounter<long>(
        "seq_syntax.rendering.truncated_messages",
        unit: "{truncation}",
        description: "The number of rendered messages truncated for exceeding the output length limit.");

    public static void RecordSuppressedError(string kind)
    {
        SuppressedErrors.Add(1, new KeyValuePair<string, object?>(TagNames.ErrorKind, kind));
    }
}
