using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Rendering;
using Xunit;

namespace Seq.Syntax.Tests.Templates;

public class LevelRendererTests
{
    [Theory]
    // No format: the name, verbatim.
    [InlineData("Warning", null, "Warning")]
    [InlineData("OK", null, "OK")]
    // Widths 1–4 use the moniker table when the name is present in it.
    [InlineData("Warning", "u3", "WRN")]
    [InlineData("Warning", "t2", "Wn")]
    [InlineData("Warning", "w4", "warn")]
    [InlineData("Verbose", "u3", "VRB")]
    [InlineData("Error", "t4", "Eror")]
    [InlineData("Trace", "u3", "TRC")]
    [InlineData("Critical", "u3", "CRT")]
    [InlineData("Critical", "t4", "Crit")]
    [InlineData("Emergency", "u4", "EMRG")]
    // Width formats resolve recognized spellings to their canonical names.
    [InlineData("WARN", "u3", "WRN")]
    [InlineData("trce", "u3", "TRC")]
    [InlineData("dbug", "t4", "Dbug")]
    [InlineData("crit", "u3", "CRT")]
    [InlineData("warn", "u12", "WARNING·····")]
    // Names outside the table truncate or pad to the requested width.
    [InlineData("OK", "u3", "OK·")]
    [InlineData("OK", "w1", "o")]
    [InlineData("Blocked", "t4", "Bloc")]
    // Widths above 4 truncate or pad for all names, applying the casing prefix.
    [InlineData("Information", "u12", "INFORMATION·")]
    [InlineData("Information", "w5", "infor")]
    [InlineData("OK", "t5", "OK···")]
    // Simple casing formats apply to the full name, keeping its original spelling.
    [InlineData("Warning", "u", "WARNING")]
    [InlineData("Warning", "w", "warning")]
    [InlineData("WARN", "w", "warn")]
    // Junk formats are benign.
    [InlineData("Warning", "x3", "Warning")]
    [InlineData("Warning", "u0", "")]
    public void MonikersAreComputed(string name, string? format, string expected)
    {
        Assert.Equal(expected, LevelRenderer.GetLevelMoniker(new LevelValue(name), format));
    }
}
