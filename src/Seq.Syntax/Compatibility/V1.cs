// Copyright © Datalust and Contributors
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
using Seq.Syntax.Expressions.Ast;
using Seq.Syntax.Expressions.Compilation;
using Seq.Syntax.Expressions.Parsing;
using Seq.Syntax.Templates;
using Seq.Syntax.Templates.Ast;
using Seq.Syntax.Templates.Compilation;
using Seq.Syntax.Templates.Compilation.NameResolution;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Templates.Parsing;

namespace Seq.Syntax.Compatibility;

/// <summary>
/// Compiles expressions and parses templates using Seq.Syntax v1 built-in names.
/// </summary>
public static class V1
{
    /// <inheritdoc cref="SeqExpression.TryCompile(string,CultureInfo?,NameResolver,out CompiledExpression,out string)"/>
    public static bool TryCompileExpression(
        string expression,
        CultureInfo? formatProvider,
        NameResolver? nameResolver,
        [MaybeNullWhen(false)] out CompiledExpression result,
        [MaybeNullWhen(true)] out string error)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(nameResolver);

        var expressionParser = new ExpressionParser();
        if (!expressionParser.TryParse(expression, out var root, out error))
        {
            result = null;
            return false;
        }

        root = RewriteExpression(root);

        var evaluate = ExpressionCompiler.Compile(root, formatProvider, DefaultFunctionNameResolver.Build(WithV1Functions(nameResolver)));
        result = eventJson => evaluate(new EvaluationContext(eventJson));
        error = null;
        return true;
    }

    /// <inheritdoc cref="ExpressionTemplate.TryParse(string,CultureInfo?,NameResolver?,TemplateOutputEncoder?,out ExpressionTemplate,out string)"/>
    public static bool TryParseTemplate(
        string template,
        CultureInfo? culture,
        NameResolver? nameResolver,
        TemplateOutputEncoder? encoder,
        [MaybeNullWhen(false)] out ExpressionTemplate result,
        [MaybeNullWhen(true)] out string error)
    {
        ArgumentNullException.ThrowIfNull(template);

        var templateParser = new TemplateParser();
        if (!templateParser.TryParse(template, out var parsed, out error))
        {
            result = null;
            return false;
        }

        parsed = RewriteTemplate(parsed);

        var planned = TemplateLocalNameBinder.BindLocalValueNames(parsed);

        encoder ??= TemplateOutputEncoder.Default;
        result = new ExpressionTemplate(
            TemplateCompiler.Compile(
                planned,
                culture,
                TemplateFunctionNameResolver.Build(WithV1Functions(nameResolver), planned, encoder.HasEscaper),
                encoder));

        return true;
    }

    static NameResolver WithV1Functions(NameResolver? nameResolver)
    {
        var v1Functions = new V1CompatibilityFunctions();
        return nameResolver == null
            ? v1Functions
            : new OrderedNameResolver([v1Functions, nameResolver]);
    }

    static Expression RewriteExpression(Expression expression)
    {
        var transform = new V1BuiltInNames();
        return transform.Rewrite(expression);
    }

    static Template RewriteTemplate(Template template)
    {
        switch (template)
        {
            case TemplateBlock block:
                return new TemplateBlock(block.Elements.Select(RewriteTemplate).ToArray());
            case LiteralText text:
                return text;
            case FormattedExpression fx:
                return new FormattedExpression(RewriteExpression(fx.Expression), fx.Format, fx.Alignment);
            case Conditional cond:
                return new Conditional(
                    RewriteExpression(cond.Condition),
                    RewriteTemplate(cond.Consequent),
                    cond.Alternative != null ? RewriteTemplate(cond.Alternative) : null);
            case Repetition rep:
                return new Repetition(
                    RewriteExpression(rep.Enumerable),
                    rep.BindingNames,
                    RewriteTemplate(rep.Body),
                    rep.Delimiter != null ? RewriteTemplate(rep.Delimiter) : null,
                    rep.Alternative != null ? RewriteTemplate(rep.Alternative) : null);
            default:
                throw new NotSupportedException("Unsupported template type.");
        }
    }
}