using System.Text.Json.Nodes;
using Seq.Syntax.Templates;
using Xunit;

namespace Seq.Syntax.Tests.Templates;

// Rendering a compiled template against adversarial event data must never throw, and untrusted `@mt`
// output must stay bounded. Each case pairs with a specific crash or amplification found by audit.
public class RenderingRobustnessTests
{
    static string Render(string template, JsonObject evt)
    {
        var output = new StringWriter();
        new ExpressionTemplate(template).Format(evt, output);
        return output.ToString();
    }

    static JsonObject Event(string json) => (JsonObject)JsonNode.Parse(json)!;

    static JsonObject MessageEvent(string messageTemplate, JsonNode? a = null) =>
        new() { ["@t"] = "2026-08-27T01:02:03.0000000Z", ["@mt"] = messageTemplate, ["A"] = a };

    // A lone UTF-16 surrogate makes `GetString()` throw; the hole renders empty instead.
    [Fact]
    public void MalformedStringRendersEmpty()
    {
        Assert.Equal("", Render("{A}", Event("{\"A\":\"\\ud800\"}")));
    }

    // An invalid format specifier degrades to default formatting rather than throwing.
    [Fact]
    public void InvalidFormatFallsBackToDefault()
    {
        Assert.Equal("5", Render("{A:Z9}", Event("{\"A\":5}")));
    }

    [Fact]
    public void MessageInvalidFormatFallsBackToDefault()
    {
        Assert.Equal("5", Render("{@Message}", MessageEvent("{A:Z9}", 5)));
    }

    // An outer-template alignment width is clamped too, so padding stays bounded.
    [Fact]
    public void LargeAlignmentWidthInTemplateIsClamped()
    {
        Assert.Equal(1024, Render("{A,2000000000}", Event("{\"A\":\"5\"}")).Length);
    }

    // `@mt` is untrusted: its alignment width is clamped, so padding stays bounded and never overflows.
    [Theory]
    [InlineData("{A,2000000000}")]
    [InlineData("{A,-2147483648}")] // `Math.Abs(int.MinValue)` would otherwise throw.
    public void MessageAlignmentWidthIsClamped(string messageTemplate)
    {
        Assert.Equal(1024, Render("{@Message}", MessageEvent(messageTemplate, "5")).Length);
    }

    // A repeated hole over a large property could amplify output without bound; message output is capped.
    [Fact]
    public void MessageExpansionIsLengthLimited()
    {
        var messageTemplate = string.Concat(Enumerable.Repeat("{A}", 200));
        var large = JsonValue.Create(new string('x', 1000));
        Assert.Equal(16 * 1024, Render("{@Message}", MessageEvent(messageTemplate, large)).Length);
    }

    // Rendering deeply nested data guards the stack: output is truncated, not a crash.
    [Fact]
    public void DeeplyNestedValueRendersWithoutOverflow()
    {
        JsonNode deep = JsonValue.Create(0);
        for (var i = 0; i < 100_000; i++)
            deep = new JsonArray(deep);

        var output = new StringWriter();
        var record = Record.Exception(() => new ExpressionTemplate("{A}").Format(new JsonObject { ["A"] = deep }, output));
        Assert.Null(record);
    }

    // A pathologically nested template is rejected at parse rather than overflowing the stack.
    [Fact]
    public void DeeplyNestedTemplateIsRejected()
    {
        var template = string.Concat(Enumerable.Repeat("{#if true}", 50_000)) + "x" +
                       string.Concat(Enumerable.Repeat("{#end}", 50_000));
        Assert.False(ExpressionTemplate.TryParse(template, out _, out _));
    }
}
