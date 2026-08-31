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

namespace Seq.Syntax.Templates.Encoding;

/// <summary>
/// Escapes event-derived content so that it cannot be misinterpreted in the output format.
/// Template output is escaped one run at a time, so implementations must be stateless and
/// character-level: escaping runs individually must equal escaping their concatenation.
/// </summary>
public abstract class TemplateOutputEscaper
{
    /// <summary>
    /// Escape <paramref name="content"/>.
    /// </summary>
    /// <param name="content">Event-derived text destined for template output.</param>
    /// <returns>The escaped text.</returns>
    public abstract string Escape(string content);

    /// <summary>
    /// Strips C0 control characters (except <c>'\t'</c>, <c>'\r'</c>, and <c>'\n'</c>), DEL, and
    /// C1 controls (U+0080–U+009F). Neutralizes ANSI escape sequence injection into terminal output.
    /// </summary>
    public static TemplateOutputEscaper Terminal { get; } = new TerminalOutputEscaper();

    /// <summary>
    /// Escapes <c>'&amp;'</c>, <c>'&lt;'</c>, <c>'&gt;'</c>, <c>'"'</c>, and <c>'\''</c>, making
    /// content safe for substitution into HTML attributes and element bodies (excluding script
    /// and style contexts, in which no safe escaping is possible).
    /// </summary>
    public static TemplateOutputEscaper Html { get; } = new HtmlOutputEscaper();
}
