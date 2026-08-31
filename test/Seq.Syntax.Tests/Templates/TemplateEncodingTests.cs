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
    public void UnsafeWithoutEscaperIsRejectedAtCompileTime()
    {
        Assert.Throws<ArgumentException>(() => new ExpressionTemplate("{unsafe(Markup)}"));

        Assert.Throws<ArgumentException>(() => new ExpressionTemplate(
            "{unsafe(Markup)}",
            encoder: new TemplateOutputEncoder(MarkerTheme.Instance)));
    }

    [Theory]
    [InlineData("{ToString(unsafe(Markup))}")]     // consumed by another function
    [InlineData("{ToUpper(unsafe(Markup))}")]      // coerced to a string
    [InlineData("{Substring(unsafe(Markup),0,1)}")]
    [InlineData("{ {a: unsafe(Markup)} }")]        // nested in an object literal
    [InlineData("{[unsafe(Markup)]}")]             // nested in an array literal
    [InlineData("{#if unsafe(Markup) like '%b%'}y{#end}")]
    public void MisplacedUnsafeValueFailsWhenRendered(string template)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => RenderHtmlEncoded(template, "{Markup}", "<b>hi</b>"));

        Assert.Equal("`unsafe()` values can only be substituted directly into template output.", ex.Message);
    }

    [Theory]
    [InlineData("{unsafe(Markup)}", "<b>hi</b>")]
    [InlineData("{unsafe(Markup),12}", "   <b>hi</b>")]
    [InlineData("{#each x in [1]}{unsafe(Markup)}{#end}", "<b>hi</b>")]
    [InlineData("{Coalesce(unsafe(Markup), 'x')}", "<b>hi</b>")]
    public void UnsafeValueSubstitutedDirectlyBypassesEscaper(string template, string expected)
    {
        Assert.Equal(expected, RenderHtmlEncoded(template, "{Markup}", "<b>hi</b>"));
    }

    [Fact]
    public void HtmlEncoderPreservesUnsafeOutput()
    {
        var actual = RenderHtmlEncoded("{unsafe(Markup)} & {Markup}", "{Markup}", "<b>hi</b>");
        Assert.Equal("<b>hi</b> & &lt;b&gt;hi&lt;/b&gt;", actual);
    }
}
