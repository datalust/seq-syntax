using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Xunit;

namespace Seq.Syntax.Tests.Expressions;

public class ExpressionValueTests
{
    [Fact]
    public void UndefinedResultsAreFalse()
    {
        Assert.False(EvaluationResult.Undefined.IsTrue());
    }

    [Fact]
    public void NullResultsAreFalse()
    {
        Assert.False(EvaluationResult.Null.IsTrue());
    }

    [Fact]
    public void NonBooleanResultsAreFalse()
    {
        Assert.False(EvaluationResult.Defined(JsonValue.Create(10)).IsTrue());
    }

    [Fact]
    public void TrueIsTrue()
    {
        Assert.True(EvaluationResult.Defined(JsonValue.Create(true)).IsTrue());
    }

    [Fact]
    public void FalseIsNotTrue()
    {
        Assert.False(EvaluationResult.Defined(JsonValue.Create(false)).IsTrue());
    }

    [Fact]
    public void DefaultIsUndefined()
    {
        Assert.False(default(EvaluationResult).IsDefined);
        Assert.False(default(EvaluationResult).TryGetValue(out _));
    }

    [Fact]
    public void NullIsDefined()
    {
        Assert.True(EvaluationResult.Null.TryGetValue(out var node));
        Assert.Null(node);
    }

    [Fact]
    public void DeconstructionSupportsExhaustiveMatching()
    {
        static string Describe(EvaluationResult result) => result switch
        {
            (false, _) => "undefined",
            (true, null) => "null",
            (true, var node) => $"node {node}"
        };

        Assert.Equal("undefined", Describe(EvaluationResult.Undefined));
        Assert.Equal("null", Describe(EvaluationResult.Null));
        Assert.Equal("node 42", Describe(JsonValue.Create(42)));
    }
}
