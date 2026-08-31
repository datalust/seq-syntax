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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace Seq.Syntax.Expressions.Runtime;

/// <summary>
/// Type coercions applied to <see cref="EvaluationResult"/>s by runtime operators and
/// user-defined functions.
/// </summary>
public static class Coerce
{
    /// <summary>
    /// Coerce <paramref name="value"/> to a number. All JSON numbers coerce to
    /// <see cref="decimal"/>.
    /// </summary>
    public static bool Numeric(EvaluationResult value, out decimal numeric)
    {
        if (value.TryGetValue(out var node))
            return Values.TryGetNumeric(node, out numeric);

        numeric = 0;
        return false;
    }

    /// <summary>
    /// Coerce a defined <paramref name="node"/> to a number. All JSON numbers coerce to
    /// <see cref="decimal"/>.
    /// </summary>
    public static bool Numeric(JsonNode? node, out decimal numeric)
    {
        return Values.TryGetNumeric(node, out numeric);
    }

    /// <summary>
    /// Coerce <paramref name="value"/> to a number, returning <c langword="null">null</c> when it is
    /// undefined or does not coerce. Generated call sites bind <see cref="decimal"/> and
    /// <c langword="null">decimal?</c> operator parameters through this: a <see cref="decimal"/>
    /// parameter passes only when the result is non-<c langword="null">null</c>, while a
    /// <c langword="null">decimal?</c> parameter additionally lets an undefined operand through as
    /// <c langword="null">null</c>.
    /// </summary>
    internal static decimal? NumericOrDefault(EvaluationResult value)
    {
        if (value.TryGetValue(out var node) && Values.TryGetNumeric(node, out var numeric))
            return numeric;

        return null;
    }

    /// <summary>
    /// Coerce <paramref name="value"/> to a Boolean.
    /// </summary>
    public static bool Boolean(EvaluationResult value, out bool boolean)
    {
        if (value.TryGetValue(out var node))
            return Values.TryGetBoolean(node, out boolean);

        boolean = false;
        return false;
    }

    /// <summary>
    /// Coerce a defined <paramref name="node"/> to a Boolean.
    /// </summary>
    public static bool Boolean(JsonNode? node, out bool boolean)
    {
        return Values.TryGetBoolean(node, out boolean);
    }

    /// <summary>
    /// Test whether <paramref name="value"/> is the scalar Boolean <c langword="true">true</c>.
    /// </summary>
    public static bool IsTrue(EvaluationResult value)
    {
        return Boolean(value, out var b) && b;
    }

    /// <summary>
    /// Coerce <paramref name="value"/> to a Boolean, returning <c langword="null">null</c> when it
    /// is undefined or does not coerce. Generated call sites bind <see cref="bool"/> and
    /// <c langword="null">bool?</c> operator parameters through this, the way
    /// <see cref="NumericOrDefault"/> binds <see cref="decimal"/> parameters.
    /// </summary>
    internal static bool? BooleanOrDefault(EvaluationResult value)
    {
        if (value.TryGetValue(out var node) && Values.TryGetBoolean(node, out var boolean))
            return boolean;

        return null;
    }

    /// <summary>
    /// Coerce <paramref name="value"/> to a string. JSON strings and typed level values coerce;
    /// other kinds do not (use <c>ToString()</c> for explicit conversion).
    /// </summary>
    public static bool String(EvaluationResult value, [MaybeNullWhen(false)] out string str)
    {
        if (value.TryGetValue(out var node))
            return Values.TryGetString(node, out str);

        str = null;
        return false;
    }

    /// <summary>
    /// Coerce a defined <paramref name="node"/> to a string. JSON strings and typed level values
    /// coerce; other kinds do not (use <c>ToString()</c> for explicit conversion).
    /// </summary>
    public static bool String(JsonNode? node, [MaybeNullWhen(false)] out string str)
    {
        return Values.TryGetString(node, out str);
    }

    /// <summary>
    /// Coerce <paramref name="value"/> to a date-time. Typed date-time values (e.g.
    /// <c>@Timestamp</c>), parseable strings, and numbers (ticks, UTC) coerce; a
    /// <see cref="DateTime"/> of unspecified kind is taken as UTC.
    /// </summary>
    public static bool DateTimeOffset(EvaluationResult value, out DateTimeOffset dateTimeOffset)
    {
        if (value.TryGetValue(out var node))
            return Values.TryGetDateTimeOffset(node, out dateTimeOffset);

        dateTimeOffset = default;
        return false;
    }

    /// <summary>
    /// Coerce a defined <paramref name="node"/> to a date-time. Typed date-time values (e.g.
    /// <c>@Timestamp</c>), parseable strings, and numbers (ticks, UTC) coerce; a
    /// <see cref="DateTime"/> of unspecified kind is taken as UTC.
    /// </summary>
    public static bool DateTimeOffset(JsonNode? node, out DateTimeOffset dateTimeOffset)
    {
        return Values.TryGetDateTimeOffset(node, out dateTimeOffset);
    }

    /// <summary>
    /// Coerce <paramref name="value"/> to a time span. Typed time-span values (e.g.
    /// <c>@Elapsed</c>), parseable strings, and numbers (ticks) coerce.
    /// </summary>
    public static bool TimeSpan(EvaluationResult value, out TimeSpan timeSpan)
    {
        if (value.TryGetValue(out var node))
            return Values.TryGetTimeSpan(node, out timeSpan);

        timeSpan = default;
        return false;
    }

    /// <summary>
    /// Coerce a defined <paramref name="node"/> to a time span. Typed time-span values (e.g.
    /// <c>@Elapsed</c>), parseable strings, and numbers (ticks) coerce.
    /// </summary>
    public static bool TimeSpan(JsonNode? node, out TimeSpan timeSpan)
    {
        return Values.TryGetTimeSpan(node, out timeSpan);
    }

    /// <summary>
    /// Coerce <paramref name="value"/> to a date-time, returning <c langword="null">null</c> when it
    /// is undefined or does not coerce. Generated call sites bind <see cref="System.DateTimeOffset"/>
    /// and <c langword="null">DateTimeOffset?</c> operator parameters through this, the way
    /// <see cref="NumericOrDefault"/> binds <see cref="decimal"/> parameters.
    /// </summary>
    internal static DateTimeOffset? DateTimeOffsetOrDefault(EvaluationResult value)
    {
        if (value.TryGetValue(out var node) && Values.TryGetDateTimeOffset(node, out var dateTimeOffset))
            return dateTimeOffset;

        return null;
    }

    /// <summary>
    /// Coerce <paramref name="value"/> to a time span, returning <c langword="null">null</c> when it
    /// is undefined or does not coerce. Generated call sites bind <see cref="System.TimeSpan"/>
    /// and <c langword="null">TimeSpan?</c> operator parameters through this, the way
    /// <see cref="NumericOrDefault"/> binds <see cref="decimal"/> parameters.
    /// </summary>
    internal static TimeSpan? TimeSpanOrDefault(EvaluationResult value)
    {
        if (value.TryGetValue(out var node) && Values.TryGetTimeSpan(node, out var timeSpan))
            return timeSpan;

        return null;
    }

    /// <summary>
    /// Coerce <paramref name="value"/> to a string, returning <c langword="null">null</c> when it is
    /// undefined or does not coerce. Generated call sites bind <see cref="string"/> and
    /// <c langword="null">string?</c> operator parameters through this: a <see cref="string"/>
    /// parameter passes only when the result is non-<c langword="null">null</c>, while a
    /// <c langword="null">string?</c> parameter additionally lets an undefined operand through as
    /// <c langword="null">null</c>.
    /// </summary>
    internal static string? StringOrDefault(EvaluationResult value)
    {
        if (value.TryGetValue(out var node) && Values.TryGetString(node, out var str))
            return str;

        return null;
    }

    internal static bool Predicate(EvaluationResult value,
        [MaybeNullWhen(false)] out Func<EvaluationResult, EvaluationResult> predicate)
    {
        if (value.TryGetValue(out var node))
            return Predicate(node, out predicate);

        predicate = null;
        return false;
    }

    internal static bool Predicate(JsonNode? node,
        [MaybeNullWhen(false)] out Func<EvaluationResult, EvaluationResult> predicate)
    {
        if (Values.TryGetClrValue<Func<EvaluationResult, EvaluationResult>>(node, out var pred))
        {
            predicate = pred;
            return true;
        }

        predicate = null;
        return false;
    }
}
