using System.Diagnostics.Metrics;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Templates;
using Xunit;

namespace Seq.Syntax.Tests;

// The suppression sites feed the `Seq.Syntax` meter; these confirm the wiring and tag values.
public class DiagnosticsTests
{
    static List<(string Name, long Value, string? Kind)> Capture(Action action)
    {
        // Force the counters to exist so the listener enumerates them on Start.
        GC.KeepAlive(Diagnostics.SuppressedErrors);

        // Counters record synchronously on the calling thread; filter to it so parallel tests don't leak in.
        var thread = Environment.CurrentManagedThreadId;
        var results = new List<(string, long, string?)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Seq.Syntax")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (Environment.CurrentManagedThreadId != thread)
                return;

            string? kind = null;
            foreach (var tag in tags)
                if (tag.Key == Diagnostics.TagNames.ErrorKind)
                    kind = tag.Value as string;
            results.Add((instrument.Name, value, kind));
        });
        listener.Start();

        action();

        listener.Dispose();
        return results;
    }

    static void Eval(string expression, string eventJson) =>
        SeqExpression.Compile(expression)((JsonObject)JsonNode.Parse(eventJson)!);

    static void Render(string template, JsonObject evt) =>
        new ExpressionTemplate(template).Format(evt, new StringWriter());

    static void AssertSuppressed(string kind, Action action)
    {
        var measurements = Capture(action);
        Assert.Contains(measurements, m =>
            m.Name == "seq_syntax.evaluation.suppressed_errors" && m.Kind == kind && m.Value == 1);
    }

    [Fact]
    public void OutOfRangeNumberRecordsNumericRange() =>
        AssertSuppressed(Diagnostics.ErrorKinds.NumericRange, () => Eval("A + 1", "{\"A\":1e300}"));

    [Fact]
    public void ArithmeticOverflowRecordsOverflow() =>
        AssertSuppressed(Diagnostics.ErrorKinds.ArithmeticOverflow, () => Eval("A * A", "{\"A\":79228162514264337593543950335}"));

    [Fact]
    public void MalformedStringRecordsMalformedString() =>
        AssertSuppressed(Diagnostics.ErrorKinds.MalformedString, () => Eval("ToUpper(A)", "{\"A\":\"\\ud800\"}"));

    [Fact]
    public void InvalidFormatRecordsInvalidFormat() =>
        AssertSuppressed(Diagnostics.ErrorKinds.InvalidFormat, () => Render("{A:Z9}", (JsonObject)JsonNode.Parse("{\"A\":5}")!));

    [Fact]
    public void ClampedMessageAlignmentIsRecorded()
    {
        var evt = new JsonObject { ["@t"] = "2026-08-27T01:02:03.0000000Z", ["@mt"] = "{A,2000000000}", ["A"] = "5" };
        var measurements = Capture(() => Render("{@Message}", evt));
        Assert.Contains(measurements, m => m.Name == "seq_syntax.rendering.clamped_alignment_widths" && m.Value == 1);
    }

    [Fact]
    public void TruncatedMessageIsRecorded()
    {
        var messageTemplate = string.Concat(Enumerable.Repeat("{A}", 200));
        var evt = new JsonObject { ["@t"] = "2026-08-27T01:02:03.0000000Z", ["@mt"] = messageTemplate, ["A"] = new string('x', 1000) };
        var measurements = Capture(() => Render("{@Message}", evt));
        Assert.Contains(measurements, m => m.Name == "seq_syntax.rendering.truncated_messages" && m.Value == 1);
    }

    [Fact]
    public void CleanEvaluationRecordsNothing()
    {
        var measurements = Capture(() => Eval("A + 1", "{\"A\":41}"));
        Assert.Empty(measurements);
    }
}
