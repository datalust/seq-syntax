using Seq.Syntax.Templates;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Tests.Support;
using Xunit;

namespace Seq.Syntax.Tests.Templates;

public class TemplateEncodingTests
{
    public static IEnumerable<object[]> TemplateEvaluationCases =>
        TestCases.ReadAsvCases("template-encoding-cases.asv");

    [Theory]
    [MemberData(nameof(TemplateEvaluationCases))]
    public void TemplatesAreCorrectlyEvaluated(string template, string expected)
    {
        var evt = Some.InformationEvent("Hello, {Name}!", "nblumhardt");
        var compiled = new ExpressionTemplate(template, encoder: new TemplateOutputEncoder(escaper: BracketingEscaper.Instance));
        var output = new StringWriter();
        compiled.Format(evt, output);
        var actual = output.ToString();
        Assert.Equal(expected, actual);
    }

    static string RenderHtmlEncoded(string template, string messageTemplate, params object?[] propertyValues)
    {
        var evt = Some.InformationEvent(messageTemplate, propertyValues);
        var compiled = new ExpressionTemplate(template, encoder: TemplateOutputEncoder.Html);
        var output = new StringWriter();
        compiled.Format(evt, output);
        return output.ToString();
    }

    [Fact]
    public void HtmlEncoderEscapesEventContent()
    {
        var actual = RenderHtmlEncoded("<p>{@Message}</p>", "Posted {Comment}", "<script>alert('hi')</script>");
        Assert.Equal("<p>Posted &lt;script&gt;alert(&#x27;hi&#x27;)&lt;/script&gt;</p>", actual);
    }

    [Fact]
    public void HtmlEncoderPreservesUnsafeOutput()
    {
        var actual = RenderHtmlEncoded("{unsafe(Markup)} & {Markup}", "{Markup}", "<b>hi</b>");
        Assert.Equal("<b>hi</b> & &lt;b&gt;hi&lt;/b&gt;", actual);
    }
}
