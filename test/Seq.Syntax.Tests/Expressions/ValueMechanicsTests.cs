using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;
using Xunit;

namespace Seq.Syntax.Tests.Expressions;

public class ValueMechanicsTests
{
    [Fact]
    public void RepeatedConstructionYieldsIndependentNodes()
    {
        // The `1` here is a shared compiled constant; without cloning, the first evaluation's
        // result would capture it, corrupting later evaluations.
        var expr = SeqExpression.Compile("{a: 1}");
        var evt = new JsonObject();

        Assert.True(expr(evt).TryGetValue(out var first));
        Assert.True(expr(evt).TryGetValue(out var second));

        var firstObj = Assert.IsType<JsonObject>(first);
        firstObj["a"] = 2;

        Assert.Equal(1, (int)Assert.IsType<JsonObject>(second)["a"]!.GetValue<decimal>());
    }

    [Fact]
    public void DocumentSubtreesAreClonedIntoConstructedValues()
    {
        var evt = new JsonObject { ["User"] = new JsonObject { ["Name"] = "nblumhardt" } };
        var expr = SeqExpression.Compile("{u: User}");

        Assert.True(expr(evt).TryGetValue(out var result));
        Assert.IsType<JsonObject>(result)["u"]!["Name"] = "someone else";

        Assert.Equal("nblumhardt", (string)evt["User"]!["Name"]!);
    }

    [Fact]
    public void ClrBackedValuesRoundTrip()
    {
        var dto = new DateTimeOffset(2026, 8, 27, 1, 2, 3, TimeSpan.FromHours(10));
        Assert.True(Values.TryGetClrValue<DateTimeOffset>(JsonValue.Create(dto), out var dtoBack));
        Assert.Equal(dto, dtoBack);
        Assert.Equal(dto.Offset, dtoBack.Offset);

        Assert.True(Values.TryGetClrValue<TimeSpan>(JsonValue.Create(TimeSpan.FromMinutes(90)), out var tsBack));
        Assert.Equal(TimeSpan.FromMinutes(90), tsBack);
    }

    [Fact]
    public void LevelValuesHandedToCallersCloneAsStrings()
    {
        // A caller re-parenting a `@Level` result, e.g. `seqcli search --column @Level`, sees a
        // JSON string rather than the wrapper's fields.
        var evt = new JsonObject { ["@l"] = "Warning" };
        var expr = SeqExpression.Compile("@Level");

        Assert.True(expr(evt).TryGetValue(out var level));
        var enriched = new JsonObject { ["Column"] = level!.DeepClone() };

        Assert.Equal("Warning", (string)enriched["Column"]!);
        Assert.Equal("""{"Column":"Warning"}""", enriched.ToJsonString());
    }

    [Fact]
    public void CallablesCanBeWrappedAndRecovered()
    {
        var callable = Values.MakeCallable(r => r);
        Assert.True(Coerce.Predicate(callable, out var recovered));
        Assert.True(recovered(EvaluationResult.Defined(JsonValue.Create(true))).IsTrue());
    }

    [Fact]
    public void NumericCoercionSeesThroughRepresentations()
    {
        // TryGetValue<decimal> does not convert CLR-backed values of other numeric types, so the
        // coercion must handle each representation itself.
        Assert.True(Values.TryGetNumeric(JsonNode.Parse("5"), out var fromElement));
        Assert.True(Values.TryGetNumeric(JsonValue.Create(5), out var fromInt));
        Assert.True(Values.TryGetNumeric(JsonValue.Create(5.0), out var fromDouble));
        Assert.True(Values.TryGetNumeric(JsonNode.Parse("79228162514264337593543950335"), out var big));

        Assert.Equal(5m, fromElement);
        Assert.Equal(5m, fromInt);
        Assert.Equal(5m, fromDouble);
        Assert.Equal(decimal.MaxValue, big);

        Assert.False(Values.TryGetNumeric(JsonNode.Parse("\"5\""), out _));
        Assert.False(Values.TryGetNumeric(JsonValue.Create(true), out _));
    }

    [Fact]
    public void KindsAreRecognizedAcrossRepresentations()
    {
        Assert.Equal("number", Values.KindOf(JsonNode.Parse("5")));
        Assert.Equal("number", Values.KindOf(JsonValue.Create(5m)));
        Assert.Equal("bool", Values.KindOf(JsonNode.Parse("true")));
        Assert.Equal("string", Values.KindOf(JsonValue.Create(DateTimeOffset.Now)));
        Assert.Equal("string", Values.KindOf(JsonValue.Create(new LevelValue("Warning"))!));
        Assert.Equal("null", Values.KindOf(null));

        // GetValueKind throws for delegate-backed values, so KindOf must not rely on it.
        Assert.Equal("function", Values.KindOf(JsonValue.Create((Func<int, int>)(x => x))));
    }
}
