using Seq.Syntax.Templates.Encoding;

namespace Seq.Syntax.Tests.Support;

/// <summary>
/// Deliberately violates the escaper statelessness contract in order to make run boundaries
/// observable: every escaped content run is bracketed, while markup passes through unmarked.
/// </summary>
class BracketingEscaper : TemplateOutputEscaper
{
    public static BracketingEscaper Instance { get; } = new();

    public override string Escape(string content)
    {
        return $"[{content}]";
    }
}
