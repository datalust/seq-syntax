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

using System.Globalization;
using System.Runtime.CompilerServices;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Ast;
using Seq.Syntax.Expressions.Compilation;
using Seq.Syntax.Templates.Ast;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Templates.Rendering;
using Seq.Syntax.Templates.Themes;

namespace Seq.Syntax.Templates.Compilation;

static class TemplateCompiler
{
    public static CompiledTemplate Compile(Template template,
        CultureInfo? culture, NameResolver nameResolver,
        TemplateOutputEncoder encoder)
    {
        // Recurses over nested template blocks/directives; guard against deeply nested templates.
        RuntimeHelpers.EnsureSufficientExecutionStack();

        return template switch
        {
            LiteralText text => new CompiledLiteralText(text.Text, encoder.GetRun(TemplateThemeStyle.TertiaryText)),
            FormattedExpression { Expression: AmbientNameExpression { IsBuiltIn: true, PropertyName: KeywordProperties.Level } } level =>
                new CompiledLevelToken(level.Format, level.Alignment, encoder),
            FormattedExpression
            {
                Expression: AmbientNameExpression { IsBuiltIn: true, PropertyName: KeywordProperties.Exception },
                Alignment: null,
                Format: null
            } => new CompiledExceptionToken(encoder),
            FormattedExpression
            {
                Expression: AmbientNameExpression { IsBuiltIn: true, PropertyName: KeywordProperties.Message },
                Format: null
            } message => new CompiledMessageToken(culture, message.Alignment, encoder),
            FormattedExpression expression => MakeCompiledFormattedExpression(
                ExpressionCompiler.Compile(expression.Expression, culture, nameResolver), expression.Format, expression.Alignment, culture, encoder),
            TemplateBlock block => new CompiledTemplateBlock(block.Elements.Select(e => Compile(e, culture, nameResolver, encoder)).ToArray()),
            Conditional conditional => new CompiledConditional(
                ExpressionCompiler.Compile(conditional.Condition, culture, nameResolver),
                Compile(conditional.Consequent, culture, nameResolver, encoder),
                conditional.Alternative == null ? null : Compile(conditional.Alternative, culture, nameResolver, encoder)),
            Repetition repetition => new CompiledRepetition(
                ExpressionCompiler.Compile(repetition.Enumerable, culture, nameResolver),
                repetition.BindingNames.Length > 0 ? repetition.BindingNames[0] : null,
                repetition.BindingNames.Length > 1 ? repetition.BindingNames[1] : null,
                Compile(repetition.Body, culture, nameResolver, encoder),
                repetition.Delimiter == null ? null : Compile(repetition.Delimiter, culture, nameResolver, encoder),
                repetition.Alternative == null ? null : Compile(repetition.Alternative, culture, nameResolver, encoder)),
            _ => throw new NotSupportedException()
        };
    }

    // `unsafe()` selectively bypasses the escaper, so the escaper-aware wrapper is only needed
    // (and its extra evaluation plumbing only paid for) when an escaper is configured.
    static CompiledTemplate MakeCompiledFormattedExpression(
        Evaluatable expression, string? format, Alignment? alignment, IFormatProvider? formatProvider,
        TemplateOutputEncoder encoder)
    {
        return encoder.HasEscaper
            ? new EscapableEncodedCompiledFormattedExpression(expression, format, alignment, formatProvider, encoder)
            : new CompiledFormattedExpression(expression, format, alignment, formatProvider, encoder);
    }
}