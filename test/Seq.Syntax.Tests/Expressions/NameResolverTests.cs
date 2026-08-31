using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Tests.Support;
using Xunit;

namespace Seq.Syntax.Tests.Expressions;

public class NameResolverTests
{
    public static EvaluationResult Magic(EvaluationResult number)
    {
        if (!Coerce.Numeric(number, out var num))
            return EvaluationResult.Undefined;

        return JsonValue.Create(num + 42);
    }

    public static EvaluationResult SecretWordAt(string word, EvaluationResult index)
    {
        if (!Coerce.Numeric(index, out var i))
            return EvaluationResult.Undefined;

        return JsonValue.Create(word[(int)i].ToString());
    }

    // A required `string` operand: the call short-circuits unless the argument coerces to a string.
    public static EvaluationResult Shout(string value)
    {
        return JsonValue.Create(value.ToUpperInvariant() + "!");
    }

    // A `string?` operand: an undefined argument arrives as null; a defined non-string (JSON null
    // included) short-circuits the call before the body runs.
    public static EvaluationResult Wrap(string? value)
    {
        return JsonValue.Create(value is null ? "<none>" : $"[{value}]");
    }

    // A required `bool` operand: only true Boolean values coerce — there is no truthiness for
    // strings or numbers.
    public static EvaluationResult Negate(bool value)
    {
        return JsonValue.Create(!value);
    }

    public static EvaluationResult NegateOrTrue(bool? value)
    {
        return JsonValue.Create(!(value ?? false));
    }

    // A required `DateTimeOffset` operand: typed date-times, parseable strings, and tick counts
    // coerce; anything else short-circuits.
    public static EvaluationResult YearOf(DateTimeOffset dateTime)
    {
        return JsonValue.Create(dateTime.Year);
    }

    public static EvaluationResult YearOrZero(DateTimeOffset? dateTime)
    {
        return JsonValue.Create(dateTime?.Year ?? 0);
    }

    // A required `TimeSpan` operand: typed time spans, parseable strings, and tick counts coerce.
    public static EvaluationResult WholeSeconds(TimeSpan timeSpan)
    {
        return JsonValue.Create((int)timeSpan.TotalSeconds);
    }

    public static EvaluationResult WholeSecondsOrZero(TimeSpan? timeSpan)
    {
        return JsonValue.Create((int)(timeSpan?.TotalSeconds ?? 0));
    }

    // Functions must return `EvaluationResult`; a method like this one is rejected at compile time.
    public static JsonNode? Unwrapped(EvaluationResult value)
    {
        return value.TryGetValue(out var node) ? node : null;
    }

    static EvaluationResult Eval(string expression) =>
        SeqExpression.Compile(expression, nameResolver: new StaticMemberNameResolver(typeof(NameResolverTests)))(
            Some.InformationEvent());

    [Fact]
    public void RequiredStringOperandsAreCoercedOrShortCircuit()
    {
        Assert.True(Eval("shout('hi') = 'HI!'").IsTrue());
        Assert.False(Eval("shout(undefined())").IsDefined);
        Assert.False(Eval("shout(null)").IsDefined);
        Assert.False(Eval("shout(42)").IsDefined);
    }

    [Fact]
    public void NullableStringOperandsPassUndefinedThroughButRejectDefinedNonStrings()
    {
        Assert.True(Eval("wrap('hi') = '[hi]'").IsTrue());
        Assert.True(Eval("wrap(undefined()) = '<none>'").IsTrue());
        Assert.False(Eval("wrap(null)").IsDefined);
        Assert.False(Eval("wrap(42)").IsDefined);
    }

    [Fact]
    public void RequiredBooleanOperandsAreCoercedOrShortCircuit()
    {
        Assert.True(Eval("negate(false)").IsTrue());
        Assert.True(Eval("negate(1 = 2)").IsTrue());
        Assert.False(Eval("negate(true)").IsTrue());
        Assert.False(Eval("negate(undefined())").IsDefined);
        Assert.False(Eval("negate(null)").IsDefined);
        Assert.False(Eval("negate('true')").IsDefined);
        Assert.False(Eval("negate(1)").IsDefined);
    }

    [Fact]
    public void NullableBooleanOperandsPassUndefinedThroughButRejectDefinedNonBooleans()
    {
        Assert.False(Eval("negateortrue(true)").IsTrue());
        Assert.True(Eval("negateortrue(undefined())").IsTrue());
        Assert.False(Eval("negateortrue(null)").IsDefined);
        Assert.False(Eval("negateortrue('true')").IsDefined);
    }

    [Fact]
    public void RequiredDateTimeOperandsAreCoercedOrShortCircuit()
    {
        Assert.True(Eval("yearof(@Timestamp) >= 2026").IsTrue());
        Assert.True(Eval("yearof('2026-08-27T01:02:03Z') = 2026").IsTrue());
        Assert.True(Eval("yearof(0) = 1").IsTrue());
        Assert.False(Eval("yearof('later')").IsDefined);
        Assert.False(Eval("yearof(undefined())").IsDefined);
        Assert.False(Eval("yearof(null)").IsDefined);
    }

    [Fact]
    public void NullableDateTimeOperandsPassUndefinedThroughButRejectDefinedNonDateTimes()
    {
        Assert.True(Eval("yearorzero('2026-08-27T01:02:03Z') = 2026").IsTrue());
        Assert.True(Eval("yearorzero(undefined()) = 0").IsTrue());
        Assert.False(Eval("yearorzero(null)").IsDefined);
        Assert.False(Eval("yearorzero('later')").IsDefined);
    }

    [Fact]
    public void RequiredTimeSpanOperandsAreCoercedOrShortCircuit()
    {
        Assert.True(Eval("wholeseconds('00:01:30') = 90").IsTrue());
        Assert.True(Eval("wholeseconds(10000000) = 1").IsTrue());
        Assert.False(Eval("wholeseconds('a while')").IsDefined);
        Assert.False(Eval("wholeseconds(undefined())").IsDefined);
        Assert.False(Eval("wholeseconds(null)").IsDefined);
    }

    [Fact]
    public void NullableTimeSpanOperandsPassUndefinedThroughButRejectDefinedNonTimeSpans()
    {
        Assert.True(Eval("wholesecondsorzero('00:01:30') = 90").IsTrue());
        Assert.True(Eval("wholesecondsorzero(undefined()) = 0").IsTrue());
        Assert.False(Eval("wholesecondsorzero(null)").IsDefined);
        Assert.False(Eval("wholesecondsorzero('a while')").IsDefined);
    }

    [Fact]
    public void FunctionsNotReturningEvaluationResultAreRejected()
    {
        var ex = Assert.Throws<ArgumentException>(() => Eval("unwrapped(42)"));
        Assert.Contains("`Unwrapped` implementing function `unwrapped`", ex.Message);
        Assert.Contains("must return `EvaluationResult`", ex.Message);
    }

    class SecretWordResolver : NameResolver
    {
        readonly NameResolver _inner;
        readonly string _word;

        public SecretWordResolver(NameResolver inner, string word)
        {
            _inner = inner;
            _word = word;
        }

        public override bool TryResolveFunctionName(string name, [MaybeNullWhen(false)] out MethodInfo implementation)
            => _inner.TryResolveFunctionName(name, out implementation);

        public override bool TryBindFunctionParameter(ParameterInfo parameter, [MaybeNullWhen(false)] out object boundValue)
        {
            if (parameter.ParameterType == typeof(string))
            {
                boundValue = _word;
                return true;
            }

            boundValue = null;
            return false;
        }
    }

    [Fact]
    public void UserDefinedFunctionsAreCallableInExpressions()
    {
        var expr = SeqExpression.Compile(
            "magic(10) + 3 = 55",
            nameResolver: new StaticMemberNameResolver(typeof(NameResolverTests)));
        Assert.True(expr(Some.InformationEvent()).IsTrue());
    }

    [Fact]
    public void UserDefinedFunctionsCanReceiveUserProvidedParameters()
    {
        var expr = SeqExpression.Compile(
            "SecretWordAt(1) = 'e'",
            nameResolver: new SecretWordResolver(new StaticMemberNameResolver(typeof(NameResolverTests)), "hello"));
        Assert.True(expr(Some.InformationEvent()).IsTrue());
    }
}
