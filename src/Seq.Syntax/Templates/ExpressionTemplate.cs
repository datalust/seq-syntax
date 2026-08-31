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
using System.Globalization;
using Seq.Syntax.Expressions;
using Seq.Syntax.Templates.Compilation;
using Seq.Syntax.Templates.Compilation.NameResolution;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Templates.Parsing;
using Seq.Syntax.Templates.Themes;
using System.Text.Json.Nodes;

// ReSharper disable MemberCanBePrivate.Global, UnusedMember.Global

namespace Seq.Syntax.Templates;

/// <summary>
/// Formats event JSON documents into text using embedded expressions.
/// </summary>
public class ExpressionTemplate
{
    readonly CompiledTemplate _compiled;

    /// <summary>
    /// Construct an <see cref="ExpressionTemplate"/>.
    /// </summary>
    /// <param name="template">The template text.</param>
    /// <param name="result">The parsed template, if successful.</param>
    /// <param name="error">A description of the error, if unsuccessful.</param>
    /// <returns><c langword="true">true</c> if the template was well-formed.</returns>
    public static bool TryParse(
        string template,
        [MaybeNullWhen(false)] out ExpressionTemplate result,
        [MaybeNullWhen(true)] out string error)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        return TryParse(template, null, null, null, null, out result, out error);
    }

    /// <summary>
    /// Construct an <see cref="ExpressionTemplate"/>.
    /// </summary>
    /// <param name="template">The template text.</param>
    /// <param name="culture">Optionally, a <see cref="CultureInfo"/> to use when formatting
    /// embedded values.</param>
    /// <param name="result">The parsed template, if successful.</param>
    /// <param name="error">A description of the error, if unsuccessful.</param>
    /// <param name="nameResolver">Optionally, a <see cref="NameResolver"/>
    /// with which to resolve function names that appear in the template.</param>
    /// <param name="theme">Optionally, a theme for ANSI terminal output; shorthand for
    /// <c>encoder: TemplateOutputEncoder.Ansi(theme)</c>, and exclusive with <paramref name="encoder"/>.</param>
    /// <param name="encoder">Optionally, an encoder applying a theme and/or escaper to template output.</param>
    /// <returns><c langword="true">true</c> if the template was well-formed.</returns>
    /// <exception cref="ArgumentException">Both <paramref name="theme"/> and <paramref name="encoder"/>
    /// are supplied.</exception>
    public static bool TryParse(
        string template,
        CultureInfo? culture,
        NameResolver? nameResolver,
        TemplateTheme? theme,
        TemplateOutputEncoder? encoder,
        [MaybeNullWhen(false)] out ExpressionTemplate result,
        [MaybeNullWhen(true)] out string error)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));

        var outputEncoder = CreateOutputEncoder(theme, encoder);

        var templateParser = new TemplateParser();
        if (!templateParser.TryParse(template, out var parsed, out error))
        {
            result = null;
            return false;
        }

        var planned = TemplateLocalNameBinder.BindLocalValueNames(parsed);

        result = new ExpressionTemplate(
            TemplateCompiler.Compile(
                planned,
                culture,
                TemplateFunctionNameResolver.Build(nameResolver, planned, outputEncoder.HasEscaper),
                outputEncoder));

        return true;
    }

    internal ExpressionTemplate(CompiledTemplate compiled)
    {
        _compiled = compiled;
    }

    /// <summary>
    /// Construct an <see cref="ExpressionTemplate"/>.
    /// </summary>
    /// <param name="template">The template text.</param>
    /// <param name="culture">Optionally, a <see cref="CultureInfo"/> to use when formatting
    /// embedded values.</param>
    /// <param name="nameResolver">Optionally, a <see cref="NameResolver"/>
    /// with which to resolve function names that appear in the template.</param>
    /// <param name="theme">Optionally, a theme for ANSI terminal output; shorthand for
    /// <c>encoder: TemplateOutputEncoder.Ansi(theme)</c>, and exclusive with <paramref name="encoder"/>.</param>
    /// <param name="encoder">Optionally, an encoder applying a theme and/or escaper to template output.</param>
    /// <exception cref="ArgumentException">Both <paramref name="theme"/> and <paramref name="encoder"/>
    /// are supplied, or the template is malformed.</exception>
    public ExpressionTemplate(
        string template,
        CultureInfo? culture = null,
        NameResolver? nameResolver = null,
        TemplateTheme? theme = null,
        TemplateOutputEncoder? encoder = null)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));

        var outputEncoder = CreateOutputEncoder(theme, encoder);

        var templateParser = new TemplateParser();
        if (!templateParser.TryParse(template, out var parsed, out var error))
            throw new ArgumentException(error);

        var planned = TemplateLocalNameBinder.BindLocalValueNames(parsed);

        _compiled = TemplateCompiler.Compile(
            planned,
            culture,
            TemplateFunctionNameResolver.Build(nameResolver, planned, outputEncoder.HasEscaper),
            outputEncoder);
    }

    internal static TemplateOutputEncoder CreateOutputEncoder(TemplateTheme? theme, TemplateOutputEncoder? encoder)
    {
        if (theme != null && encoder != null)
            throw new ArgumentException(
                $"Supply either `theme` or `encoder`, but not both. A theme is combined with a custom escaper by constructing a {nameof(TemplateOutputEncoder)} directly.");

        if (theme != null)
            return TemplateOutputEncoder.Ansi(theme);

        return encoder ?? TemplateOutputEncoder.Default;
    }


    /// <summary>
    /// Format <paramref name="eventJson"/> into <paramref name="output"/>.
    /// </summary>
    /// <param name="eventJson">An event JSON document in Seq's emission schema.</param>
    /// <param name="output">The output writer.</param>
    public void Format(JsonObject eventJson, TextWriter output)
    {
        _compiled.Evaluate(new EvaluationContext(eventJson), output);
    }

    /// <summary>
    /// Escape <paramref name="text"/> so that it will be interpreted as
    /// literal text when incorporated into an expression template.
    /// </summary>
    /// <param name="text">The text to apply escaping to.</param>
    /// <returns>The text with any template special characters escaped.</returns>
    public static string EscapeLiteralText(string text)
    {
        return text.Replace("{", "{{").Replace("}", "}}");
    }
}