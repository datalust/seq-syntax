using System.Globalization;
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

    static string Render(JsonObject evt, string template = "{@Message}")
    {
        var output = new StringWriter();
        new ExpressionTemplate(template, culture: CultureInfo.InvariantCulture).Format(evt, output);
        return output.ToString();
    }

    static string RenderScalar(JsonNode? value, string? format = null)
    {
        var evt = MessageEvent(format == null ? "{V}" : $"{{V:{format}}}");
        evt["V"] = value;
        return Render(evt);
    }

    public static IEnumerable<object?[]> TypedScalarCases()
    {
        // Every CLR type accepted by JsonValue.Create(), plus the runtime's own typed values.
        yield return [JsonValue.Create("s"), "s"];
        yield return [JsonValue.Create('c'), "c"];
        yield return [JsonValue.Create(true), "True"];
        yield return [JsonValue.Create(false), "False"];
        yield return [JsonValue.Create((byte)1), "1"];
        yield return [JsonValue.Create((sbyte)-1), "-1"];
        yield return [JsonValue.Create((short)-2), "-2"];
        yield return [JsonValue.Create((ushort)2), "2"];
        yield return [JsonValue.Create(-3), "-3"];
        yield return [JsonValue.Create(3u), "3"];
        yield return [JsonValue.Create(-4L), "-4"];
        yield return [JsonValue.Create(4UL), "4"];
        yield return [JsonValue.Create(1.5f), "1.5"];
        yield return [JsonValue.Create(2.5), "2.5"];
        yield return [JsonValue.Create(3.5m), "3.5"];
        yield return [JsonValue.Create(Guid.Parse("0e2f7a0c-0b7e-4a3e-9f2a-6a4b2f6f2e1d")), "0e2f7a0c-0b7e-4a3e-9f2a-6a4b2f6f2e1d"];
        yield return [JsonValue.Create(new DateTime(2026, 8, 27, 1, 2, 3, DateTimeKind.Utc)), "2026-08-27T01:02:03.0000000Z"];
        yield return [JsonValue.Create(new DateTime(2026, 8, 27, 1, 2, 3, DateTimeKind.Unspecified)), "2026-08-27T01:02:03.0000000"];
        yield return [JsonValue.Create(new DateTimeOffset(2026, 8, 27, 1, 2, 3, TimeSpan.Zero)), "2026-08-27T01:02:03.0000000Z"];
        yield return [JsonValue.Create(new DateTimeOffset(2026, 8, 27, 1, 2, 3, TimeSpan.FromHours(10))), "2026-08-27T01:02:03.0000000+10:00"];
        yield return [JsonValue.Create(new TimeSpan(1, 2, 3, 4, 500)), "1.02:03:04.5000000"];
        yield return [JsonValue.Create(TimeSpan.FromMinutes(-90)), "-01:30:00"];
        yield return [null, "null"];
    }

    [Theory]
    [MemberData(nameof(TypedScalarCases))]
    public void TypedScalarsHaveReasonableDefaultRendering(JsonNode? value, string expected)
    {
        Assert.Equal(expected, RenderScalar(value));
    }

    [Theory]
    [InlineData("yyyy-MM-dd", "2026-08-27")]
    [InlineData("HH:mm", "01:02")]
    public void DateTimeValuesHonorFormatSpecifiers(string format, string expected)
    {
        Assert.Equal(expected, RenderScalar(JsonValue.Create(new DateTimeOffset(2026, 8, 27, 1, 2, 3, TimeSpan.Zero)), format));
        Assert.Equal(expected, RenderScalar(JsonValue.Create(new DateTime(2026, 8, 27, 1, 2, 3, DateTimeKind.Utc)), format));
    }

    [Fact]
    public void OtherFormattableScalarsHonorFormatSpecifiers()
    {
        Assert.Equal("00000000000000000000000000000000", RenderScalar(JsonValue.Create(Guid.Empty), "N"));
        Assert.Equal("1:02", RenderScalar(JsonValue.Create(new TimeSpan(1, 2, 0)), "h\\:mm"));
        Assert.Equal("0042", RenderScalar(JsonValue.Create(42), "0000"));
        Assert.Equal("0042", RenderScalar(JsonValue.Create(42m), "0000"));
    }

    [Fact]
    public void InvalidFormatSpecifiersFallBackToDefaultRendering()
    {
        // A trailing escape character is an invalid date/time format string.
        var value = JsonValue.Create(new DateTimeOffset(2026, 8, 27, 1, 2, 3, TimeSpan.Zero));
        Assert.Equal("2026-08-27T01:02:03.0000000Z", RenderScalar(value, "\\"));
    }

    [Fact]
    public void UnknownScalarTypesRenderViaToString()
    {
        Assert.Equal("(1, 2)", RenderScalar(JsonValue.Create((1, 2))));
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
