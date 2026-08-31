using Seq.Syntax.Expressions;

namespace Seq.Syntax.Templates.Encoding;

class PreEncodedValue
{
    public EvaluationResult Inner { get; }

    public PreEncodedValue(EvaluationResult inner)
    {
        Inner = inner;
    }

    /// <summary>
    /// Raised where <c>unsafe()</c> output lands somewhere other than direct hole substitution —
    /// passed on to another function, or nested in an object or array literal.
    /// </summary>
    public static Exception Misplaced()
    {
        return new InvalidOperationException("`unsafe()` values can only be substituted directly into template output.");
    }

    public override string ToString()
    {
        throw Misplaced();
    }
}
