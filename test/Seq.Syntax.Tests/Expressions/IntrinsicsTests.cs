using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;
using Xunit;

namespace Seq.Syntax.Tests.Expressions;

public class IntrinsicsTests
{
    [Fact]
    public void EventTypeIsComputedFromDocumentField()
    {
        var evt = new JsonObject { ["@i"] = "a1e77001" };

        var eventType = KeywordProperties.GetEventType(evt);
        Assert.True(eventType.TryGetValue(out var node));
        Assert.True(Values.TryGetClrValue<uint>(node, out var i));
        Assert.Equal(0xa1e77001, i);
    }

    [Fact]
    public void EventTypeFallsBackToMessageTemplateHash()
    {
        var evt = new JsonObject { ["@mt"] = "Hello, {Name}!" };

        var eventType = KeywordProperties.GetEventType(evt);
        Assert.True(eventType.TryGetValue(out var node));
        Assert.True(Values.TryGetClrValue<uint>(node, out var i));
        Assert.Equal(Seq.Syntax.Expressions.Compilation.Linq.EventIdHash.Compute("Hello, {Name}!"), i);
    }

    [Fact]
    public void DataIsACloneOfTheEventDocument()
    {
        var evt = new JsonObject
        {
            ["@mt"] = "Hello, {Name}!",
            ["Name"] = "World"
        };

        var data = KeywordProperties.GetData(evt);
        Assert.True(data.TryGetValue(out var node));
        var clone = Assert.IsType<JsonObject>(node);
        Assert.NotSame(evt, clone);
        Assert.True(JsonNode.DeepEquals(evt, clone));

        clone["Name"] = "Modified";
        Assert.True(Values.TryGetString(evt["Name"], out var original));
        Assert.Equal("World", original);
    }
}
