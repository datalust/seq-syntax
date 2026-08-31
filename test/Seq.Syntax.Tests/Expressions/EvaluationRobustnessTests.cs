using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Xunit;

namespace Seq.Syntax.Tests.Expressions;

// Evaluating a compiled expression against adversarial event data must never throw; range, format,
// and depth errors degrade to `undefined`. Each case pairs with a specific crash found by audit.
public class EvaluationRobustnessTests
{
    static JsonObject Event(string json) => (JsonObject)JsonNode.Parse(json)!;

    static EvaluationResult Eval(string expression, string eventJson) =>
        SeqExpression.Compile(expression)(Event(eventJson));

    static JsonObject EventWith(string name, JsonNode? value) => new() { [name] = value };

    // Out-of-`decimal`-range JSON numbers (e.g. 1e300) are non-numeric rather than throwing on coercion.
    [Theory]
    [InlineData("A + 1")]
    [InlineData("A - 1")]
    [InlineData("A * A")]
    [InlineData("A > 1")]
    [InlineData("-A")]
    [InlineData("Round(A, 1)")]
    public void OutOfDecimalRangeNumbersAreUndefined(string expression)
    {
        Assert.False(Eval(expression, "{\"A\":1e300}").IsDefined);
    }

    // An out-of-range operand still compares without throwing (it's simply unequal).
    [Fact]
    public void OutOfRangeNumberComparesUnequal()
    {
        Assert.False(Eval("A = 1", "{\"A\":1e300}").IsTrue());
    }

    // `decimal` arithmetic that overflows is undefined, not an OverflowException.
    [Theory]
    [InlineData("A + A")]
    [InlineData("A * A")]
    [InlineData("A / 0.5")]
    public void OverflowingArithmeticIsUndefined(string expression)
    {
        Assert.False(Eval(expression, "{\"A\":79228162514264337593543950335}").IsDefined);
    }

    // `^` can yield ±∞/NaN; those don't re-enter arithmetic, they're undefined.
    [Theory]
    [InlineData("(10 ^ 400) + 1")]
    [InlineData("Round((-1) ^ 0.5, 1)")]
    public void NonFiniteResultsAreUndefined(string expression)
    {
        Assert.False(Eval(expression, "{}").IsDefined);
    }

    // A lone UTF-16 surrogate makes `GetString()` throw; string coercion degrades to undefined.
    [Fact]
    public void MalformedStringsCoerceToUndefined()
    {
        Assert.False(Eval("ToUpper(A)", "{\"A\":\"\\ud800\"}").IsDefined);
    }

    // A too-large index/length would overflow the `int` cast; it's out of range, so undefined.
    [Theory]
    [InlineData("ElementAt(A, 3000000000)")]
    [InlineData("A[3000000000]")]
    public void OutOfIntRangeIndexIsUndefined(string expression)
    {
        Assert.False(Eval(expression, "{\"A\":[1]}").IsDefined);
    }

    [Fact]
    public void NegativeSubstringLengthIsUndefined()
    {
        Assert.False(Eval("Substring('abc', 0, -1)", "{}").IsDefined);
    }

    [Fact]
    public void OversizeSubstringLengthClampsToString()
    {
        var result = Eval("Substring('abc', 0, 3000000000)", "{}");
        Assert.True(result.TryGetValue(out var node));
        Assert.Equal("abc", (string)node!);
    }

    // `Math.Round(decimal, int)` accepts 0..28; a larger request is undefined, not out-of-range.
    [Fact]
    public void RoundBeyondSupportedPrecisionIsUndefined()
    {
        Assert.False(Eval("Round(1, 29)", "{}").IsDefined);
    }

    [Fact]
    public void RoundAtSupportedPrecisionStillWorks()
    {
        var result = Eval("Round(1.25, 1)", "{}");
        Assert.True(result.TryGetValue(out var node));
        Assert.Equal(1.2m, (decimal)node!);
    }

    // A regex driven to its match timeout by adversarial input is undefined, not a thrown exception.
    [Fact]
    public void RegexTimeoutIsUndefined()
    {
        // `(a|a?)+b` backtracks catastrophically; the 100ms match timeout fires and is swallowed.
        var corpus = new string('a', 40) + "X";
        Assert.False(Eval("IsMatch(A, '(a|a?)+b')", $"{{\"A\":\"{corpus}\"}}").IsDefined);
    }

    // Structural equality over deeply nested data guards the stack: too deep is undefined, not a crash.
    [Fact]
    public void DeeplyNestedEqualityIsUndefined()
    {
        JsonNode deep = JsonValue.Create(0);
        for (var i = 0; i < 100_000; i++)
            deep = new JsonArray(deep);

        var result = SeqExpression.Compile("A = A")(EventWith("A", deep));
        Assert.False(result.IsDefined);
    }

    // A pathologically nested expression is rejected at parse rather than overflowing the stack.
    [Fact]
    public void DeeplyNestedExpressionIsRejected()
    {
        var expression = new string('(', 50_000) + "1" + new string(')', 50_000);
        Assert.False(SeqExpression.TryCompile(expression, out _, out _));
    }
}
