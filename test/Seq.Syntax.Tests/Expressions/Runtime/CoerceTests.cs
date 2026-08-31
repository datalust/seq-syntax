using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Tests.Support;
using Xunit;

namespace Seq.Syntax.Tests.Expressions.Runtime;

public class CoerceTests
{
    static readonly DateTimeOffset SomeInstant = new(2026, 8, 27, 1, 2, 3, TimeSpan.FromHours(10));

    [Fact]
    public void TypedDateTimeOffsetsCoerce()
    {
        Assert.True(Coerce.DateTimeOffset(EvaluationResult.Defined(JsonValue.Create(SomeInstant)), out var dto));
        Assert.Equal(SomeInstant, dto);
        Assert.Equal(SomeInstant.Offset, dto.Offset);
    }

    [Fact]
    public void TypedDateTimesCoerce()
    {
        Assert.True(Coerce.DateTimeOffset(JsonValue.Create(new DateTime(2026, 8, 27, 1, 2, 3, DateTimeKind.Utc)), out var utc));
        Assert.Equal(TimeSpan.Zero, utc.Offset);

        // An unspecified-kind date-time is taken as UTC.
        Assert.True(Coerce.DateTimeOffset(JsonValue.Create(new DateTime(2026, 8, 27, 1, 2, 3, DateTimeKind.Unspecified)), out var unspecified));
        Assert.Equal(utc, unspecified);
    }

    [Fact]
    public void StringsCoerceToDateTimeOffsets()
    {
        // Element-backed, the way strings arrive from event documents.
        var node = JsonNode.Parse("\"2026-08-27T01:02:03.0000000+10:00\"");
        Assert.True(Coerce.DateTimeOffset(node, out var dto));
        Assert.Equal(SomeInstant, dto);
        Assert.Equal(SomeInstant.Offset, dto.Offset);

        Assert.False(Coerce.DateTimeOffset(JsonValue.Create("just now"), out _));
    }

    [Fact]
    public void NumbersCoerceToDateTimeOffsetsAsUtcTicks()
    {
        Assert.True(Coerce.DateTimeOffset(JsonNode.Parse(SomeInstant.UtcTicks.ToString()), out var dto));
        Assert.Equal(SomeInstant, dto);
        Assert.Equal(TimeSpan.Zero, dto.Offset);

        Assert.False(Coerce.DateTimeOffset(JsonValue.Create(-1), out _));
        Assert.False(Coerce.DateTimeOffset(JsonValue.Create(decimal.MaxValue), out _));
    }

    [Fact]
    public void NonDateTimeValuesDoNotCoerceToDateTimeOffsets()
    {
        Assert.False(Coerce.DateTimeOffset(EvaluationResult.Undefined, out _));
        Assert.False(Coerce.DateTimeOffset(EvaluationResult.Null, out _));
        Assert.False(Coerce.DateTimeOffset(JsonNode.Parse("{}"), out _));
        Assert.False(Coerce.DateTimeOffset(JsonValue.Create(true), out _));
    }

    [Fact]
    public void TypedTimeSpansCoerce()
    {
        Assert.True(Coerce.TimeSpan(EvaluationResult.Defined(JsonValue.Create(TimeSpan.FromMinutes(90))), out var ts));
        Assert.Equal(TimeSpan.FromMinutes(90), ts);
    }

    [Fact]
    public void StringsCoerceToTimeSpans()
    {
        var node = JsonNode.Parse("\"01:30:00\"");
        Assert.True(Coerce.TimeSpan(node, out var ts));
        Assert.Equal(TimeSpan.FromMinutes(90), ts);

        Assert.False(Coerce.TimeSpan(JsonValue.Create("a while"), out _));
    }

    [Fact]
    public void NumbersCoerceToTimeSpansAsTicks()
    {
        Assert.True(Coerce.TimeSpan(JsonNode.Parse(TimeSpan.FromMinutes(90).Ticks.ToString()), out var ts));
        Assert.Equal(TimeSpan.FromMinutes(90), ts);

        Assert.True(Coerce.TimeSpan(JsonValue.Create(-TimeSpan.TicksPerSecond), out var negative));
        Assert.Equal(TimeSpan.FromSeconds(-1), negative);

        Assert.False(Coerce.TimeSpan(JsonValue.Create(decimal.MaxValue), out _));
    }

    [Fact]
    public void NonTimeSpanValuesDoNotCoerceToTimeSpans()
    {
        Assert.False(Coerce.TimeSpan(EvaluationResult.Undefined, out _));
        Assert.False(Coerce.TimeSpan(EvaluationResult.Null, out _));
        Assert.False(Coerce.TimeSpan(JsonNode.Parse("[]"), out _));
    }

    [Fact]
    public void KeywordPropertyResultsCoerce()
    {
        var evt = Some.Event("Warning");
        var timestamp = DateTimeOffset.Parse((string)evt["@t"]!);
        evt["@st"] = (timestamp - TimeSpan.FromSeconds(90)).ToString("o");

        Assert.True(Coerce.DateTimeOffset(SeqExpression.Compile("@Timestamp")(evt), out var dto));
        Assert.Equal(timestamp, dto);

        Assert.True(Coerce.TimeSpan(SeqExpression.Compile("@Elapsed")(evt), out var elapsed));
        Assert.Equal(TimeSpan.FromSeconds(90), elapsed);
    }
}
