using Seq.Syntax.Expressions;

namespace Seq.Syntax.Templates.Encoding;

class PreEncodedValue
{
    public EvaluationResult Inner { get; }

    public PreEncodedValue(EvaluationResult inner)
    {
        Inner = inner;
    }

    public override string ToString()
    {
        // Reached when `unsafe()` output lands somewhere other than direct hole substitution —
        // for example, embedded inside a rendered object literal — where the escaper cannot be
        // selectively bypassed.
        throw new InvalidOperationException("`unsafe()` values can only be substituted directly into template output.");
    }
}
