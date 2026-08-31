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

using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Templates.Rendering;
using Seq.Syntax.Templates.Themes;

namespace Seq.Syntax.Templates.Compilation;

class CompiledFormattedExpression : CompiledTemplate
{
    readonly Evaluatable _expression;
    readonly EvaluationResult _format;
    readonly Alignment? _alignment;
    readonly IFormatProvider? _formatProvider;
    readonly TemplateOutputEncoder _encoder;
    readonly Run _secondaryText;
    readonly JsonWriter _jsonWriter;

    public CompiledFormattedExpression(Evaluatable expression, string? format, Alignment? alignment, IFormatProvider? formatProvider, TemplateOutputEncoder encoder)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _format = format == null ? EvaluationResult.Null : JsonValue.Create(format);
        _alignment = alignment;
        _formatProvider = formatProvider;
        _encoder = encoder;
        _secondaryText = encoder.GetRun(TemplateThemeStyle.SecondaryText);
        _jsonWriter = new JsonWriter(encoder);
    }

    public override void Evaluate(EvaluationContext ctx, TextWriter output)
    {
        var invisibleCharacterCount = 0;

        if (_alignment == null)
        {
            EvaluateUnaligned(ctx, output, _formatProvider, ref invisibleCharacterCount);
        }
        else
        {
            var writer = new StringWriter();
            EvaluateUnaligned(ctx, writer, _formatProvider, ref invisibleCharacterCount);
            Padding.Apply(output, writer.ToString(), _alignment.Value.Widen(invisibleCharacterCount));
        }
    }

    void EvaluateUnaligned(EvaluationContext ctx, TextWriter output, IFormatProvider? formatProvider, ref int invisibleCharacterCount)
    {
        var value = _expression(ctx);
        if (!value.TryGetValue(out var node))
            return; // Undefined is empty

        if (node is null or JsonValue)
        {
            var runtimeResult = RuntimeOperators.ToString(formatProvider, node, _format);

            // No distinction made here between invalid values and those that return undefined from `ToString`.
            if (Coerce.String(runtimeResult, out var toString))
            {
                using var _ = _secondaryText.Open(output, ref invisibleCharacterCount);
                _encoder.WriteContent(output, toString);
            }
        }
        else
        {
            _jsonWriter.Format(node, output, ref invisibleCharacterCount);
        }
    }
}
