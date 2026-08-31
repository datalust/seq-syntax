using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Tests.Support;
using Xunit;

namespace Seq.Syntax.Tests.Expressions.Runtime;

public class LocalsTests
{
    [Fact]
    public void NoValueIsDefinedInNoLocals()
    {
        Assert.False(Locals.TryGetValue(null, "A", out _));
    }

    [Fact]
    public void ASetValueIsRetrieved()
    {
        var expected = Some.JsonNode();
        var locals = Locals.Set(null, "A", expected);
        Assert.True(Locals.TryGetValue(locals, "A", out var actual));
        Assert.Same(expected, actual);
    }

    [Fact]
    public void ASetNullValueIsRetrieved()
    {
        var locals = Locals.Set(null, "A", null);
        Assert.True(Locals.TryGetValue(locals, "A", out var actual));
        Assert.Null(actual);
    }

    [Fact]
    public void ASetValueIsRetrievedFromMany()
    {
        var expected = Some.JsonNode();
        var locals = Locals.Set(null, "A", expected);
        locals = Locals.Set(locals, "B", Some.JsonNode());
        Assert.True(Locals.TryGetValue(locals, "A", out var actual));
        Assert.Same(expected, actual);
    }

    [Fact]
    public void TheTopmostValueIsRetrievedForAName()
    {
        var expected = Some.JsonNode();
        var locals = Locals.Set(null, "A", Some.JsonNode());
        locals = Locals.Set(locals, "B", Some.JsonNode());
        locals = Locals.Set(locals, "A", expected);
        Assert.True(Locals.TryGetValue(locals, "A", out var actual));
        Assert.Same(expected, actual);
    }
}
