using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Compilation;
using Seq.Syntax.Templates.Rendering;

namespace Seq.Syntax.Templates.Encoding;

/// <summary>
/// Compiles a formatted expression twice — an escaping variant and a markup (theme-only) variant —
/// and selects at runtime: <c>unsafe()</c> output bypasses the escaper but keeps the theme, while
/// everything else is escaped as content. Format strings and alignment apply to the unwrapped
/// value in both variants, which read it through a substitute local.
/// </summary>
class EscapableEncodedCompiledFormattedExpression : CompiledTemplate
{
    static int _nextSubstituteLocalNameSuffix;
    readonly string _substituteLocalName = $"%sub{Interlocked.Increment(ref _nextSubstituteLocalNameSuffix)}";
    readonly Evaluatable _expression;
    readonly CompiledFormattedExpression _content;
    readonly CompiledFormattedExpression _markup;

    public EscapableEncodedCompiledFormattedExpression(Evaluatable expression, string? format, Alignment? alignment, IFormatProvider? formatProvider, TemplateOutputEncoder encoder)
    {
        _expression = expression;
        _content = new CompiledFormattedExpression(GetSubstituteLocalValue, format, alignment, formatProvider, encoder);
        _markup = new CompiledFormattedExpression(GetSubstituteLocalValue, format, alignment, formatProvider, encoder.WithoutEscaper());
    }

    EvaluationResult GetSubstituteLocalValue(EvaluationContext context)
    {
        return Locals.TryGetValue(context.Locals, _substituteLocalName, out var computed)
            ? EvaluationResult.Defined(computed)
            : EvaluationResult.Undefined;
    }

    public override void Evaluate(EvaluationContext ctx, TextWriter output)
    {
        var value = _expression(ctx);

        if (value.TryGetValue(out var node) &&
            Values.TryGetClrValue<PreEncodedValue>(node, out var pv))
        {
            var markupContext = pv.Inner.TryGetValue(out var inner) ?
                new EvaluationContext(ctx.Document, Locals.Set(ctx.Locals, _substituteLocalName, inner)) :
                new EvaluationContext(ctx.Document);
            _markup.Evaluate(markupContext, output);
            return;
        }

        var contentContext = value.TryGetValue(out var substitute)
            ? new EvaluationContext(ctx.Document, Locals.Set(ctx.Locals, _substituteLocalName, substitute))
            : ctx;

        _content.Evaluate(contentContext, output);
    }
}
