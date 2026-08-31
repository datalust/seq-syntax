using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Seq.Syntax.Compatibility;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Compilation;
using Seq.Syntax.Tests.Support;
using Xunit;

namespace Seq.Syntax.Tests.Compatibility;

public class V1Tests
{
    static readonly NameResolver EmptyResolver = new OrderedNameResolver(Array.Empty<NameResolver>());

    [Theory]
    [InlineData("@t", "@Timestamp")]
    [InlineData("@m", "@Message")]
    [InlineData("@mt", "@MessageTemplate")]
    [InlineData("@l", "@Level")]
    [InlineData("@x", "@Exception")]
    [InlineData("@i", "@EventType")]
    [InlineData("@p", "@Properties")]
    [InlineData("@p['Name']", "@Properties['Name']")]
    public void V1BuiltInNamesEvaluateAsTheirCurrentEquivalents(string v1, string current)
    {
        var evt = Some.InformationEvent("Hello, {Name}!", "nblumhardt");

        Assert.Equal(Render(SeqExpression.Compile(current)(evt)), RenderV1(v1, evt));
    }

    [Theory]
    [InlineData("@tr", "@TraceId")]
    [InlineData("@sp", "@SpanId")]
    [InlineData("@st", "@Start")]
    [InlineData("TotalMilliseconds(FromUnixEpoch(@st))", "TotalMilliseconds(FromUnixEpoch(@Start))")]
    [InlineData("@ra['service']", "@Resource['service']")]
    [InlineData("@ps", "@ParentId")]
    [InlineData("@sk", "@SpanKind")]
    [InlineData("@sa['scope']", "@Scope['scope']")]
    public void V1SpanNamesEvaluateAsTheirCurrentEquivalents(string v1, string current)
    {
        var evt = Some.Event(
            "Warning",
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom());
        var timestamp = DateTimeOffset.Parse((string)evt["@t"]!);
        evt["@st"] = (timestamp - TimeSpan.FromSeconds(1.5)).ToString("o");
        evt["@ra"] = new JsonObject { ["service"] = "test" };
        evt["@ps"] = ActivitySpanId.CreateRandom().ToString();
        evt["@sk"] = "Internal";
        evt["@sa"] = new JsonObject { ["scope"] = "Seq.Syntax.Tests" };

        var expected = Render(SeqExpression.Compile(current)(evt));
        Assert.NotEqual("undefined", expected);
        Assert.Equal(expected, RenderV1(v1, evt));
    }

    // In v1, `@l` carried a parsed level that was `Information` when the `@l` member was absent
    // (the emission convention omits it); the mapping onto `@Level` must preserve that. The
    // current syntax's plain read of `@l` — undefined for such events — is not v1-compatible.
    [Fact]
    public void LevelDefaultsToInformationWhenAbsent()
    {
        var evt = Some.InformationEvent();
        Assert.False(evt.ContainsKey("@l"));

        Assert.Equal("true", RenderV1("@l = 'Information'", evt));
        Assert.Equal("false", RenderV1("@l = 'Warning'", evt));
        Assert.Equal("\"Information\"", RenderV1("ToString(@l)", evt));

        Assert.True(V1.TryParseTemplate("{@l}|{@l:u3}", null, null, null, out var template, out var error), error);
        var output = new StringWriter();
        template.Format(evt, output);
        Assert.Equal("Information|INF", output.ToString());
    }

    [Fact]
    public void LevelKeepsItsStringFormInConstructedValues()
    {
        var evt = Some.WarningEvent();

        Assert.Equal("[\"Warning\"]", RenderV1("[@l]", evt));
        Assert.Equal("{\"level\":\"Warning\"}", RenderV1("{level: @l}", evt));
    }

    // v1's `@r` was computed from the message template's format-carrying holes, not read from
    // the document; hole names resolve the way `@Message` resolves them.
    [Fact]
    public void RenderingsAreComputedFromFormattedMessageTemplateHoles()
    {
        var evt = Some.InformationEvent("Completed {Task} in {Elapsed:0.00} ms", "indexing", 12.3456);

        Assert.Equal("[\"12.35\"]", RenderV1("@r", evt));
        Assert.Equal("\"12.35\"", RenderV1("@r[0]", evt));

        Assert.True(V1.TryParseTemplate("{#each r in @r}[{r}]{#end}", CultureInfo.InvariantCulture, null, null, out var template, out var error), error);
        var output = new StringWriter();
        template.Format(evt, output);
        Assert.Equal("[12.35]", output.ToString());
    }

    [Fact]
    public void RenderingsAreUndefinedWithoutFormattedHoles()
    {
        var evt = Some.InformationEvent("Hello, {Name}!", "nblumhardt");

        Assert.Equal("undefined", RenderV1("@r", evt));
    }

    [Fact]
    public void OnlyAtPrefixedBuiltInsAreRewritten()
    {
        var evt = Some.InformationEvent();
        evt["t"] = "a user property that happens to be named `t`";

        Assert.Equal(Render(SeqExpression.Compile("@Timestamp")(evt)), RenderV1("@t", evt));
        Assert.Equal(Render(SeqExpression.Compile("t")(evt)), RenderV1("t", evt));
    }

    [Fact]
    public void CurrentSyntaxContinuesToWorkUnderV1()
    {
        var evt = Some.InformationEvent();

        Assert.Equal(Render(SeqExpression.Compile("@Timestamp")(evt)), RenderV1("@Timestamp", evt));
    }

    [Fact]
    public void TemplatesRenderV1BuiltInNames()
    {
        var evt = Some.InformationEvent("Hello, {Name}!", "nblumhardt");

        Assert.True(V1.TryParseTemplate("{@m} for {@p['Name']}", null, null, null, out var template, out var error), error);

        var output = new StringWriter();
        template.Format(evt, output);
        Assert.Equal("Hello, nblumhardt! for nblumhardt", output.ToString());
    }

    static string RenderV1(string v1Expression, JsonObject evt)
    {
        Assert.True(V1.TryCompileExpression(v1Expression, CultureInfo.InvariantCulture, EmptyResolver, out var compiled, out var error), error);
        return Render(compiled(evt));
    }

    static string Render(EvaluationResult result)
    {
        if (!result.TryGetValue(out var node))
            return "undefined";

        return node?.ToJsonString() ?? "null";
    }
}
