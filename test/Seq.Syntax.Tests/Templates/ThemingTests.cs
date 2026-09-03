using System.Text.Json.Nodes;
using Seq.Syntax.Templates;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Templates.Themes;
using Seq.Syntax.Tests.Support;
using Xunit;

namespace Seq.Syntax.Tests.Templates;

public class ThemingTests
{
    static string Render(string template, JsonObject evt, TemplateOutputEncoder? encoder = null)
    {
        var compiled = new ExpressionTemplate(template, encoder: encoder);
        var output = new StringWriter();
        compiled.Format(evt, output);
        return output.ToString();
    }

    static string RenderMarked(string template, JsonObject evt)
    {
        return Render(template, evt, encoder: new TemplateOutputEncoder(MarkerTheme.Instance));
    }

    static JsonObject MessageEvent(string messageTemplate) =>
        new() { ["@t"] = "2026-08-30T01:02:03.0000000Z", ["@mt"] = messageTemplate };

    [Fact]
    public void MessageHoleScalarKindsSelectStyles()
    {
        var evt = MessageEvent("x{S}{C}{N}{D}{B}{Z}{G}{T}{Missing}");
        evt["S"] = "s";
        evt["C"] = JsonValue.Create('c');
        evt["N"] = 1;
        evt["D"] = JsonValue.Create(2.5);
        evt["B"] = true;
        evt["Z"] = null;
        evt["G"] = JsonValue.Create(Guid.Empty);
        evt["T"] = JsonValue.Create(new DateTimeOffset(2026, 8, 30, 1, 2, 3, TimeSpan.Zero));

        Assert.Equal(
            "<Text>x</Text>" +
            "<String>s</String>" +
            "<String>c</String>" +
            "<Number>1</Number>" +
            "<Number>2.5</Number>" +
            "<Boolean>True</Boolean>" +
            "<Null>null</Null>" +
            $"<Scalar>{Guid.Empty}</Scalar>" +
            "<Scalar>2026-08-30T01:02:03.0000000Z</Scalar>" +
            "<Invalid>{Missing}</Invalid>",
            RenderMarked("{@Message}", evt));
    }

    [Fact]
    public void LiteralTextIsTertiaryTextMarkup()
    {
        Assert.Equal("<TertiaryText>hi </TertiaryText><SecondaryText>s</SecondaryText>",
            RenderMarked("hi {'s'}", MessageEvent("-")));
    }

    [Fact]
    public void StructuredValuesStyleThroughTheJsonWriter()
    {
        var evt = MessageEvent("-");
        evt["A"] = new JsonObject { ["n"] = new JsonArray(1, "s", true, null) };

        Assert.Equal(
            "<TertiaryText>{</TertiaryText>" +
            "<Name>\"n\"</Name>" +
            "<TertiaryText>:</TertiaryText>" +
            "<TertiaryText>[</TertiaryText>" +
            "<Number>1</Number>" +
            "<TertiaryText>,</TertiaryText>" +
            "<String>\"s\"</String>" +
            "<TertiaryText>,</TertiaryText>" +
            "<Boolean>true</Boolean>" +
            "<TertiaryText>,</TertiaryText>" +
            "<Null>null</Null>" +
            "<TertiaryText>]</TertiaryText>" +
            "<TertiaryText>}</TertiaryText>",
            RenderMarked("{A}", evt));
    }

    [Fact]
    public void ExceptionLinesStyleByStackFramePrefix()
    {
        var evt = MessageEvent("-");
        evt["@x"] = "System.Exception: boom\n   at Frame.One()";

        Assert.Equal(
            "<Text>System.Exception: boom\n</Text>" +
            "<SecondaryText>   at Frame.One()\n</SecondaryText>",
            RenderMarked("{@Exception}", evt).ReplaceLineEndings("\n"));
    }

    // The rendered level keeps the document's spelling; only the style follows the
    // canonicalized name.
    [Theory]
    [InlineData("TRACE", "LevelVerbose")]
    [InlineData("verbose", "LevelVerbose")]
    [InlineData("dbug", "LevelDebug")]
    [InlineData("info", "LevelInformation")]
    [InlineData("notice", "LevelInformation")]
    [InlineData("WARN", "LevelWarning")]
    [InlineData("eror", "LevelError")]
    [InlineData("fatal", "LevelFatal")]
    [InlineData("critical", "LevelFatal")]
    [InlineData("emerg", "LevelFatal")]
    [InlineData("alert", "LevelFatal")]
    [InlineData("panic", "LevelFatal")]
    [InlineData("OK", "LevelInformation")]
    public void LevelSpellingsStyleByCanonicalName(string level, string expectedStyle)
    {
        var evt = MessageEvent("-");
        evt["@l"] = level;

        Assert.Equal($"<{expectedStyle}>{level}</{expectedStyle}>", RenderMarked("{@Level}", evt));
    }

    [Fact]
    public void AbsentLevelStylesAsInformation()
    {
        Assert.Equal("<LevelInformation>Information</LevelInformation>", RenderMarked("{@Level}", MessageEvent("-")));
    }

    [Fact]
    public void LevelAlignmentPadsToVisibleWidth()
    {
        // "<LevelInformation>" + "</LevelInformation>" are invisible; "INF" pads to visible width 8.
        Assert.Equal("<LevelInformation>INF</LevelInformation>     ", RenderMarked("{@Level,-8:u3}", MessageEvent("-")));
    }

    [Fact]
    public void MessageHoleAlignmentPadsToVisibleWidth()
    {
        var evt = MessageEvent("|{S,-6}|");
        evt["S"] = "ab";

        Assert.Equal("<Text>|</Text><String>ab</String>    <Text>|</Text>", RenderMarked("{@Message}", evt));
    }

    [Fact]
    public void MessageTokenAlignmentPadsToVisibleWidth()
    {
        Assert.Equal("<Text>hi</Text>          ", RenderMarked("{@Message,-12}", MessageEvent("hi")));
    }

    [Fact]
    public void FormattedExpressionAlignmentPadsToVisibleWidth()
    {
        var evt = MessageEvent("-");
        evt["S"] = "ab";

        Assert.Equal("<SecondaryText>ab</SecondaryText>    ", RenderMarked("{S,-6}", evt));
    }

    // Adapted from Serilog.Expressions' `EncodingAppliesToThemedOutput`: there, the whole themed
    // hole — ANSI sequences included — passed through the encoder. Here the escaper applies to
    // content runs only, and the theme's own sequences are never mangled.
    [Fact]
    public void EscaperAppliesToContentWithinThemedRuns()
    {
        var evt = Some.InformationEvent("Hello, {Name}!", "nblumhardt");
        var encoder = new TemplateOutputEncoder(StringHashPrefixingTheme.Instance, BracketingEscaper.Instance);

        Assert.Equal("-[Hello, ]#[nblumhardt]\x1b[0m[!]-", Render("-{@Message}-", evt, encoder: encoder));
    }

    [Fact]
    public void UnsafeBypassesEscaperButKeepsTheme()
    {
        var evt = MessageEvent("-");
        evt["A"] = "x";
        var encoder = new TemplateOutputEncoder(MarkerTheme.Instance, BracketingEscaper.Instance);

        Assert.Equal("<SecondaryText>x</SecondaryText>", Render("{unsafe(A)}", evt, encoder: encoder));
        Assert.Equal("<SecondaryText>[x]</SecondaryText>", Render("{A}", evt, encoder: encoder));
    }

    [Fact]
    public void UnsafeUnderThemeWithoutEscaperIsRejected()
    {
        Assert.Throws<ArgumentException>(() => RenderMarked("{unsafe(A)}", MessageEvent("-")));
    }

    [Fact]
    public void TruncatedMessageRecoversNeutralStyling()
    {
        var evt = MessageEvent(string.Concat(Enumerable.Repeat("{A}", 200)));
        evt["A"] = new string('x', 1000);

        var actual = Render("{@Message}", evt, encoder: TemplateOutputEncoder.Ansi(TemplateTheme.Code));

        Assert.Equal(16 * 1024 + "\x1b[0m".Length, actual.Length);
        Assert.EndsWith("\x1b[0m", actual);
    }

    [Fact]
    public void CustomAnsiThemesOverrideBaseThemeStyles()
    {
        var custom = new AnsiTheme((AnsiTheme)TemplateTheme.Sixteen, new Dictionary<TemplateThemeStyle, string>
        {
            [TemplateThemeStyle.String] = "\x1b[38;5;159m"
        });

        var evt = MessageEvent("{A}");
        evt["A"] = "x";

        Assert.Equal("\x1b[38;5;159mx\x1b[0m", Render("{@Message}", evt, encoder: TemplateOutputEncoder.Ansi(custom)));
    }

    const string Hostile = "\x1b[31m<script>";

    static JsonObject HostileEvent(bool includePreRenderedMessage)
    {
        var evt = new JsonObject
        {
            ["@t"] = "2026-08-30T01:02:03.0000000Z",
            ["@l"] = Hostile,
            ["@mt"] = $"text{Hostile} {{A}} {{B.C}} {{Missing}}",
            ["@x"] = $"boom{Hostile}\n   at Frame{Hostile}",
            ["A"] = Hostile,
            ["B"] = new JsonObject
            {
                ["C"] = 1.5,
                [$"name{Hostile}"] = new JsonArray(Hostile, 5, true, null, new JsonObject { [Hostile] = Hostile })
            }
        };

        if (includePreRenderedMessage)
            evt["@m"] = $"rendered {Hostile}";

        return evt;
    }

    // The audit backing the design's classification of every write site: an event with hostile
    // text laced through every field must not be able to smuggle a single escaper-significant
    // character into the output.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HostileEventContentCannotReachTerminalOutput(bool includePreRenderedMessage)
    {
        var evt = HostileEvent(includePreRenderedMessage);
        var encoder = new TemplateOutputEncoder(escaper: TemplateOutputEscaper.Terminal);

        var actual = Render("{@Level} {@Message} {@Exception} {A,12} {B} {rest()}", evt, encoder: encoder);

        Assert.DoesNotContain('\x1b', actual);
        Assert.Contains("<script>", actual);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HostileEventContentCannotReachHtmlOutput(bool includePreRenderedMessage)
    {
        var evt = HostileEvent(includePreRenderedMessage);

        var actual = Render("{@Level} {@Message} {@Exception} {A,12} {B} {rest()}", evt,
            encoder: TemplateOutputEncoder.Html);

        Assert.DoesNotContain('<', actual);
        Assert.Contains("&lt;script&gt;", actual);
    }

    [Fact]
    public void AnsiThemedOutputIsTerminalSafeByDefault()
    {
        var actual = Render("{@Message}", HostileEvent(includePreRenderedMessage: true), encoder: TemplateOutputEncoder.Ansi(TemplateTheme.Code));

        // The theme's own sequences survive; event-derived ESC characters do not.
        Assert.Equal("\x1b[38;5;0253mrendered [31m<script>\x1b[0m", actual);
    }

    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData("keep\ttabs\r\nand newlines", "keep\ttabs\r\nand newlines")]
    [InlineData("\x1b[31mred\x1b[0m", "[31mred[0m")]
    [InlineData("nul\0bel\abs\b", "nulbelbs")]
    [InlineData("del\u007Fc1\u0080\u009F-", "delc1-")]
    public void TerminalEscaperStripsControlCharacters(string content, string expected)
    {
        Assert.Equal(expected, TemplateOutputEscaper.Terminal.Escape(content));
    }

    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData("a & b", "a &amp; b")]
    [InlineData("<b attr=\"v\">'q'</b>", "&lt;b attr=&quot;v&quot;&gt;&#x27;q&#x27;&lt;/b&gt;")]
    public void HtmlEscaperEscapesMarkupSignificantCharacters(string content, string expected)
    {
        Assert.Equal(expected, TemplateOutputEscaper.Html.Escape(content));
    }
}
