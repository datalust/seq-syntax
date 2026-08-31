using Seq.Syntax.Templates.Themes;

namespace Seq.Syntax.Tests.Support;

/// <summary>
/// Delimits every run with its style name, making run classification directly readable in
/// assertions: a string run renders as <c>&lt;String&gt;…&lt;/String&gt;</c>.
/// </summary>
class MarkerTheme : TemplateTheme
{
    public static MarkerTheme Instance { get; } = new();

    public override string Open(TemplateThemeStyle style)
    {
        return $"<{style}>";
    }

    public override string Close(TemplateThemeStyle style)
    {
        return $"</{style}>";
    }
}
