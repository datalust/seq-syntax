// Copyright 2017 Serilog Contributors
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

using Seq.Syntax.Expressions.Runtime;

// ReSharper disable StringLiteralTypo

namespace Seq.Syntax.Templates.Rendering;

/// <summary>
/// Implements the {@Level} element.
/// can now have a fixed width applied to it, as well as casing rules.
/// Width is set through formats like "u3" (uppercase three chars),
/// "w1" (one lowercase char), or "t4" (title case four chars).
/// Width formats resolve recognized spellings (`WARN`, `trce`, …) to their canonical names, so
/// levels with well-known abbreviations use them at widths 1–4; any other name (and any greater
/// width) is truncated or padded to the width. Without a width the name keeps the document's
/// spelling.
/// </summary>
static class LevelRenderer
{
    const char PaddingCharacter = '·';

    static readonly Dictionary<string, string[]> TitleCaseLevelMonikers =
        new(StringComparer.Ordinal)
        {
            ["Verbose"] = ["V", "Vb", "Vrb", "Verb"],
            ["Debug"] = ["D", "De", "Dbg", "Dbug"],
            ["Information"] = ["I", "In", "Inf", "Info"],
            ["Warning"] = ["W", "Wn", "Wrn", "Warn"],
            ["Error"] = ["E", "Er", "Err", "Eror"],
            ["Fatal"] = ["F", "Fa", "Ftl", "Fatl"],
            ["Trace"] = ["T", "Tr", "Trc", "Trce"],
            ["Notice"] = ["N", "Nt", "Ntc", "Ntce"],
            ["Critical"] = ["C", "Cr", "Crt", "Crit"],
            ["Emergency"] = ["E", "Em", "Emg", "Emrg"],
            ["Alert"] = ["A", "Al", "Alr", "Alrt"],
            ["Panic"] = ["P", "Pa", "Pnc", "Pnic"],
        };

    static readonly Dictionary<string, string[]> LowercaseLevelMonikers =
        TitleCaseLevelMonikers.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(m => m.ToLowerInvariant()).ToArray(),
            StringComparer.Ordinal);

    static readonly Dictionary<string, string[]> UppercaseLevelMonikers =
        TitleCaseLevelMonikers.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(m => m.ToUpperInvariant()).ToArray(),
            StringComparer.Ordinal);

    public static string GetLevelMoniker(LevelValue value, string? format)
    {
        if (format == null)
            return value.Name;

        if (format.Length != 2 && format.Length != 3)
            return Casing.Format(value.Name, format);

        // Using int.Parse() here requires allocating a string to exclude the first character prefix.
        // Junk like "wxy" will be accepted but produce benign results.
        var width = format[1] - '0';
        if (format.Length == 3)
        {
            width *= 10;
            width += format[2] - '0';
        }

        if (width < 1)
            return string.Empty;

        var monikers = format[0] switch
        {
            'w' => LowercaseLevelMonikers,
            'u' => UppercaseLevelMonikers,
            't' => TitleCaseLevelMonikers,
            _ => null
        };

        if (monikers == null)
            return Casing.Format(value.Name, format);

        var name = LevelMapping.TryGetCanonicalName(value.Name, out var canonical) ? canonical : value.Name;

        if (width <= 4 && monikers.TryGetValue(name, out var byWidth))
            return byWidth[width - 1];

        var moniker = name.Length > width
            ? name.Substring(0, width)
            : name;

        moniker = format[0] switch
        {
            'w' => moniker.ToLowerInvariant(),
            'u' => moniker.ToUpperInvariant(),
            _ => moniker
        };

        return moniker.PadRight(width, PaddingCharacter);
    }
}
