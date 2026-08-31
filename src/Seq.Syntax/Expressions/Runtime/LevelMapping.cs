// Copyright © Datalust and contributors.
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

namespace Seq.Syntax.Expressions.Runtime;

// Recognizes the level spellings that arrive via OTLP and other non-Serilog sources (`info`,
// `WARN`, `trce`, `critical`, …); the table is copied from SeqCli's `LevelMapping`. `@Level`
// itself preserves the document's spelling — the table drives theme-style selection only.
static class LevelMapping
{
    static readonly Dictionary<string, string> LevelsByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["t"] = "Trace",
            ["tr"] = "Trace",
            ["trc"] = "Trace",
            ["trce"] = "Trace",
            ["trace"] = "Trace",
            ["v"] = "Verbose",
            ["ver"] = "Verbose",
            ["vrb"] = "Verbose",
            ["verb"] = "Verbose",
            ["verbose"] = "Verbose",
            ["d"] = "Debug",
            ["de"] = "Debug",
            ["dbg"] = "Debug",
            ["deb"] = "Debug",
            ["dbug"] = "Debug",
            ["debu"] = "Debug",
            ["debug"] = "Debug",
            ["i"] = "Information",
            ["in"] = "Information",
            ["inf"] = "Information",
            ["info"] = "Information",
            ["information"] = "Information",
            ["notice"] = "Notice",
            ["w"] = "Warning",
            ["wa"] = "Warning",
            ["war"] = "Warning",
            ["wrn"] = "Warning",
            ["warn"] = "Warning",
            ["warning"] = "Warning",
            ["e"] = "Error",
            ["er"] = "Error",
            ["err"] = "Error",
            ["erro"] = "Error",
            ["eror"] = "Error",
            ["error"] = "Error",
            ["f"] = "Fatal",
            ["fa"] = "Fatal",
            ["ftl"] = "Fatal",
            ["fat"] = "Fatal",
            ["fatl"] = "Fatal",
            ["fatal"] = "Fatal",
            ["c"] = "Critical",
            ["cr"] = "Critical",
            ["crt"] = "Critical",
            ["cri"] = "Critical",
            ["crit"] = "Critical",
            ["critical"] = "Critical",
            ["emerg"] = "Emergency",
            ["alert"] = "Alert",
            ["panic"] = "Panic"
        };

    static readonly LevelValue Information = new("Information");

    /// <summary>
    /// Map an <c>@l</c> value to a typed level. An absent level is <c>Information</c>, by the
    /// emission convention that levels starting with "Inf" are omitted; a present level keeps
    /// its original spelling.
    /// </summary>
    public static LevelValue ToLevelValue(string? level)
    {
        if (string.IsNullOrEmpty(level))
            return Information;

        return new LevelValue(level);
    }

    /// <summary>
    /// The canonical name for a recognized level spelling (<c>WARN</c> → <c>Warning</c>). Used
    /// when selecting theme styles; never applied to the level value itself.
    /// </summary>
    public static bool TryGetCanonicalName(string level, [NotNullWhen(true)] out string? canonical)
    {
        return LevelsByName.TryGetValue(level, out canonical);
    }
}
