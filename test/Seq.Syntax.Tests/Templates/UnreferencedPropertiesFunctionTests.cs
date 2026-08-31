using System.Text.Json.Nodes;
using Seq.Syntax.Templates.Ast;
using Seq.Syntax.Templates.Compilation.UnreferencedProperties;
using Seq.Syntax.Templates.Parsing;
using Xunit;

namespace Seq.Syntax.Tests.Templates;

public class UnreferencedPropertiesFunctionTests
{
    [Fact]
    public void UnreferencedPropertiesFunctionIsNamedRest()
    {
        var function = new UnreferencedPropertiesFunction(new LiteralText("test"));
        Assert.True(function.TryResolveFunctionName("Rest", out _));
    }

    [Fact]
    public void UnreferencedPropertiesExcludeThoseInMessageAndTemplate()
    {
        Assert.True(new TemplateParser().TryParse("{@m}{A + 1}{#if true}{B}{#end}", out var template, out _));

        var function = new UnreferencedPropertiesFunction(template);

        var evt = new JsonObject
        {
            ["@mt"] = "{C}",
            ["A"] = null,
            ["B"] = null,
            ["C"] = null,
            ["D"] = null
        };

        var deep = UnreferencedPropertiesFunction.Implementation(function, evt, JsonValue.Create(true));

        Assert.True(deep.TryGetValue(out var deepNode));
        var included = Assert.Single(Assert.IsType<JsonObject>(deepNode));
        Assert.Equal("D", included.Key);

        var shallow = UnreferencedPropertiesFunction.Implementation(function, evt);
        Assert.True(shallow.TryGetValue(out var shallowNode));
        var members = Assert.IsType<JsonObject>(shallowNode);
        Assert.True(members.ContainsKey("C"));
        Assert.True(members.ContainsKey("D"));
    }
}
