using System.Globalization;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Tests.Support;
using Xunit;

namespace Seq.Syntax.Tests.Expressions;

public class ExpressionEvaluationTests
{
    public static IEnumerable<object[]> ExpressionEvaluationCases =>
        TestCases.ReadAsvCases("expression-evaluation-cases.asv");

    [Theory]
    [MemberData(nameof(ExpressionEvaluationCases))]
    public void ExpressionsAreCorrectlyEvaluated(string expr, string result)
    {
        var evt = Some.InformationEvent();

        evt["User"] = new JsonObject
        {
            ["Id"] = 42,
            ["Name"] = "nblumhardt"
        };

        var timestamp = DateTimeOffset.Parse((string)evt["@t"]!);
        evt["@st"] = (timestamp - TimeSpan.FromMinutes(10)).ToString("o");

        var frFr = CultureInfo.GetCultureInfoByIetfLanguageTag("fr-FR");
        var actual = SeqExpression.Compile(expr, formatProvider: frFr)(evt);
        var expected = SeqExpression.Compile(result)(evt);

        if (!expected.IsDefined)
        {
            Assert.True(!actual.IsDefined, $"Expected value: undefined{Environment.NewLine}Actual value: {Display(actual)}");
        }
        else
        {
            Assert.True(
                actual.TryGetValue(out var actualNode) &&
                expected.TryGetValue(out var expectedNode) &&
                Coerce.IsTrue(RuntimeOperators._Internal_Equal(StringComparison.OrdinalIgnoreCase, actualNode, expectedNode)),
                $"Expected value: {Display(expected)}{Environment.NewLine}Actual value: {Display(actual)}");
        }
    }

    static string Display(EvaluationResult value)
    {
        if (!value.TryGetValue(out var node))
            return "undefined";

        return node?.ToJsonString() ?? "null";
    }
}
