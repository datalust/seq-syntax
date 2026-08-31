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
/// Inserts delimiting text before and after classified runs of template output. Theme text is
/// never escaped by the template's output escaper, and is not counted when applying alignment.
/// </summary>
public abstract class TemplateTheme
{
    /// <summary>
    /// A 256-color theme along the lines of Visual Studio Code.
    /// </summary>
    public static TemplateTheme Code { get; } = AnsiThemes.Code;

    /// <summary>
    /// A theme using only gray, black and white.
    /// </summary>
    public static TemplateTheme Grayscale { get; } = AnsiThemes.Grayscale;

    /// <summary>
    /// A theme in the style of the original <i>Serilog.Sinks.Literate</i>.
    /// </summary>
    public static TemplateTheme Literate { get; } = AnsiThemes.Literate;

    /// <summary>
    /// A theme in the style of the original <i>Serilog.Sinks.Literate</i> using only standard 16
    /// terminal colors that will work on light backgrounds.
    /// </summary>
    public static TemplateTheme Sixteen { get; } = AnsiThemes.Sixteen;

    /// <summary>
    /// Text written before a run of the given style, or <see langword="null"/> for none.
    /// </summary>
    /// <param name="style">The style of the run.</param>
    public abstract string? Open(TemplateThemeStyle style);

    /// <summary>
    /// Text written after a run of the given style, or <see langword="null"/> for none.
    /// </summary>
    /// <param name="style">The style of the run.</param>
    public abstract string? Close(TemplateThemeStyle style);

    /// <summary>
    /// Text that restores a neutral state when output is cut off mid-run — for example, when an
    /// oversized message is truncated at the expansion limit and the closing text of the current
    /// run is dropped along with the overflowing output — or <see langword="null"/> for none.
    /// </summary>
    public virtual string? TruncationRecovery => null;
}
