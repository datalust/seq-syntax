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

namespace Seq.Syntax.Templates.Themes;

/// <summary>
/// An ANSI terminal theme: a map from styles to ANSI escape sequences. Runs in styles the theme
/// defines are followed by the ANSI reset sequence.
/// </summary>
public class AnsiTheme : TemplateTheme
{
    const string AnsiStyleResetSequence = "\x1b[0m";

    readonly Dictionary<TemplateThemeStyle, string> _ansiStyles;

    /// <summary>
    /// Construct a theme given a set of styles.
    /// </summary>
    /// <param name="ansiStyles">Styles to apply within the theme. The dictionary maps style names
    /// to ANSI sequences implementing the styles.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="ansiStyles"/> is <see langword="null"/>.</exception>
    public AnsiTheme(IReadOnlyDictionary<TemplateThemeStyle, string> ansiStyles)
    {
        ArgumentNullException.ThrowIfNull(ansiStyles);
        _ansiStyles = ansiStyles.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// Construct a theme given a base theme and a set of overriding styles.
    /// </summary>
    /// <param name="baseTheme">A base theme, which will supply styles not overridden in <paramref name="ansiStyles"/>.</param>
    /// <param name="ansiStyles">Styles to apply within the theme. The dictionary maps style names
    /// to ANSI sequences implementing the styles.</param>
    /// <exception cref="ArgumentNullException">When either argument is <see langword="null"/>.</exception>
    public AnsiTheme(AnsiTheme baseTheme, IReadOnlyDictionary<TemplateThemeStyle, string> ansiStyles)
    {
        ArgumentNullException.ThrowIfNull(baseTheme);
        ArgumentNullException.ThrowIfNull(ansiStyles);
        _ansiStyles = new Dictionary<TemplateThemeStyle, string>(baseTheme._ansiStyles);
        foreach (var (style, ansiStyle) in ansiStyles)
            _ansiStyles[style] = ansiStyle;
    }

    /// <inheritdoc/>
    public override string? Open(TemplateThemeStyle style)
    {
        return _ansiStyles.GetValueOrDefault(style);
    }

    /// <inheritdoc/>
    public override string? Close(TemplateThemeStyle style)
    {
        return _ansiStyles.ContainsKey(style) ? AnsiStyleResetSequence : null;
    }

    /// <inheritdoc/>
    public override string? TruncationRecovery => AnsiStyleResetSequence;
}
