using Seq.Syntax.Templates.Themes;

namespace Seq.Syntax.Tests.Support;

static class StringHashPrefixingTheme
{
    public static readonly TemplateTheme Instance = new AnsiTheme(new Dictionary<TemplateThemeStyle, string>
    {
        [TemplateThemeStyle.String] = "#"
    });
}
