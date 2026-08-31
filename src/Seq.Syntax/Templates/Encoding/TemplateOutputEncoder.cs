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

using Seq.Syntax.Templates.Themes;

namespace Seq.Syntax.Templates.Encoding;

/// <summary>
/// Encodes template output. Template evaluation produces a sequence of runs, each classified by
/// a <see cref="TemplateThemeStyle"/> and as either <em>content</em> (event-derived text) or
/// <em>markup</em> (text the template author controls: literal template text, padding, and
/// <c>unsafe()</c> output). The theme, when present, delimits every run; the escaper, when
/// present, transforms content — and only content — before it is written.
/// </summary>
public class TemplateOutputEncoder
{
    static readonly TemplateThemeStyle[] Styles = Enum.GetValues<TemplateThemeStyle>();

    internal static TemplateOutputEncoder Default { get; } = new();

    readonly Run[] _runs;

    /// <summary>
    /// Construct a <see cref="TemplateOutputEncoder"/>.
    /// </summary>
    /// <param name="theme">Optionally, a theme delimiting classified output runs.</param>
    /// <param name="escaper">Optionally, an escaper applied to event-derived content.</param>
    public TemplateOutputEncoder(TemplateTheme? theme = null, TemplateOutputEscaper? escaper = null)
    {
        Theme = theme;
        Escaper = escaper;

        _runs = new Run[Styles.Length];
        if (theme != null)
        {
            foreach (var style in Styles)
                _runs[(int)style] = new Run(theme.Open(style), theme.Close(style));
        }
    }

    /// <summary>
    /// An encoder for ANSI terminal output: the given theme, plus the
    /// <see cref="TemplateOutputEscaper.Terminal"/> escaper so that event data cannot smuggle
    /// control characters or ANSI escape sequences into the terminal.
    /// </summary>
    /// <param name="theme">The theme to apply.</param>
    public static TemplateOutputEncoder Ansi(TemplateTheme theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        return new TemplateOutputEncoder(theme, TemplateOutputEscaper.Terminal);
    }

    /// <summary>
    /// An encoder for HTML output: the <see cref="TemplateOutputEscaper.Html"/> escaper, no theme.
    /// </summary>
    public static TemplateOutputEncoder Html { get; } = new(escaper: TemplateOutputEscaper.Html);

    internal TemplateTheme? Theme { get; }

    internal TemplateOutputEscaper? Escaper { get; }

    internal bool HasEscaper => Escaper != null;

    internal Run GetRun(TemplateThemeStyle style)
    {
        return _runs[(int)style];
    }

    internal void WriteContent(TextWriter output, string content)
    {
        output.Write(Escaper == null ? content : Escaper.Escape(content));
    }

    internal TemplateOutputEncoder WithoutEscaper()
    {
        return Escaper == null ? this : new TemplateOutputEncoder(Theme);
    }
}
