using System.Text.Json.Nodes;
using Seq.Syntax.Templates;
using Xunit;

namespace Seq.Syntax.Tests.Expressions;

/// <summary>
/// The original byte-parity tests against Serilog's formatters can no longer run (they required
/// Serilog itself); the parity guarantee is now carried by the corpus baselines. This snapshot
/// keeps the emission-format reproduction path — rebuilding an event JSON document with a
/// template — under direct test with checked-in expected output.
/// </summary>
public class FormatParityTests
{
    const string EmissionReproductionTemplate = "{ {'@t': @t, '@mt': @mt, '@l': @l, '@x': @x, ..rest()} }";

    static string Render(string template, JsonObject eventJson)
    {
        var compiled = new ExpressionTemplate(template);
        var output = new StringWriter();
        compiled.Format(eventJson, output);
        return output.ToString();
    }

    [Fact]
    public void MinimalEventRoundTrips()
    {
        var evt = new JsonObject
        {
            ["@t"] = "2026-08-27T01:02:03.0000000Z",
            ["@mt"] = "Hello, {Name}!",
            ["@i"] = "0a1b2c3d",
            ["Name"] = "world"
        };

        Assert.Equal(
            """{"@t":"2026-08-27T01:02:03.0000000Z","@mt":"Hello, {Name}!","Name":"world"}""",
            Render(EmissionReproductionTemplate, evt));
    }

    [Fact]
    public void RichEventRoundTrips()
    {
        var evt = new JsonObject
        {
            ["@t"] = "2026-08-27T01:02:03.0000000Z",
            ["@mt"] = "{A} and {B}",
            ["@i"] = "0a1b2c3d",
            ["@l"] = "Warning",
            ["@x"] = "boom\nline two",
            ["A"] = 1.5,
            ["B"] = "✓ \"ok\"",
            ["C"] = null
        };

        Assert.Equal(
            """{"@t":"2026-08-27T01:02:03.0000000Z","@mt":"{A} and {B}","@l":"Warning","@x":"boom\nline two","A":1.5,"B":"✓ \"ok\"","C":null}""",
            Render(EmissionReproductionTemplate, evt));
    }
}
