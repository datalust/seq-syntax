using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Templates;
using Xunit;

namespace Seq.Syntax.Tests.Templates;

public class MessageRenderingTests
{
    static JsonObject MessageEvent(string messageTemplate) => new()
    {
        ["@t"] = "2026-08-27T01:02:03.0000000Z",
        ["@mt"] = messageTemplate
    };

    static string Render(JsonObject evt)
    {
        var template = new ExpressionTemplate("{@Message}");
        var output = new StringWriter();
        template.Format(evt, output);
        return output.ToString();
    }

    [Fact]
    public void MessageRenderingSupportsNestedProperties()
    {
        // From the point of view of Seq and Seq Syntax, dotted identifiers in property names are paths into
        // nested objects. This differs from Serilog's interpretation, which is that they are flat names with
        // embedded dots. When Seq and Serilog are used together, Serilog.Sinks.Seq performs the conversion
        // from flat names to nested objects, so on the server, apps etc. need message rendering to work with
        // the nested data representation.

        var evt = new JsonObject
        {
            ["@t"] = DateTimeOffset.Now.ToString("O"),
            ["@mt"] = "HTTP {request.method} {request.path}",
            ["request"] = new JsonObject
            {
                ["method"] = "GET",
                ["path"] = "/example"
            }
        };

        var message = SeqExpression.Compile("@Message")(evt);

        Assert.True(message.TryGetValue(out var messageValue));
        Assert.Equal("HTTP GET /example", (string)messageValue!);
    }

    [Theory]
    [InlineData("Hello {imagined.name}!")]
    [InlineData("Hello {user.imagined}!")]
    [InlineData("Hello {user.name.first}!")]
    [InlineData("Hello {user.tags.first}!")]
    public void UnresolvableDottedHolesRenderAsRawText(string messageTemplate)
    {
        var evt = MessageEvent(messageTemplate);
        evt["user"] = new JsonObject
        {
            ["name"] = "nblumhardt",
            ["tags"] = new JsonArray("seq")
        };

        Assert.Equal(messageTemplate, Render(evt));
    }

    [Fact]
    public void LiteralDottedPropertyNamesAreNotAddressable()
    {
        // A top-level member whose name contains a literal dot can never be resolved by a dotted
        // hole; the hole is always a path into nested objects.

        var evt = MessageEvent("Hello {user.name}!");
        evt["user.name"] = "flat";

        Assert.Equal("Hello {user.name}!", Render(evt));
    }

    [Fact]
    public void TraversalIgnoresLiteralDottedPropertyNames()
    {
        var evt = MessageEvent("Hello {user.name}!");
        evt["user.name"] = "flat";
        evt["user"] = new JsonObject { ["name"] = "nested" };

        Assert.Equal("Hello nested!", Render(evt));
    }

    [Theory]
    [InlineData("{user.name,10}", "       Ada")]
    [InlineData("{user.name,-10}", "Ada       ")]
    [InlineData("{user.name,2}", "Ada")]
    public void DottedHolesHonorAlignment(string messageTemplate, string expected)
    {
        var evt = MessageEvent(messageTemplate);
        evt["user"] = new JsonObject { ["name"] = "Ada" };

        Assert.Equal(expected, Render(evt));
    }

    [Fact]
    public void DottedHolesResolveNullLeaves()
    {
        var evt = MessageEvent("Value: {user.name}");
        evt["user"] = new JsonObject { ["name"] = null };

        Assert.Equal("Value: null", Render(evt));
    }

    [Fact]
    public void TrailingDotHolesAreLiteralText()
    {
        var evt = MessageEvent("Hello {user.}!");
        evt["user"] = new JsonObject { ["name"] = "Ada" };

        Assert.Equal("Hello {user.}!", Render(evt));
    }
}
