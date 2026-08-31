// Renders a handful of synthetic events (in Seq's emission schema) through themed and encoded
// expression templates, for eyeballing ANSI output in a terminal. Run with:
//
//     dotnet run --project demo/ThemingDemo

using System.Text.Json;
using System.Text.Json.Nodes;
using Seq.Syntax.Templates;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Templates.Themes;

const string textTemplate =
    "[{@Timestamp:HH:mm:ss} {@Level:u3}" +
    "{#if SourceContext is not null} ({Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}){#end}] " +
    "{@Message} (first item is {coalesce(Items[0], '<empty>')}) {rest()}\n{@Exception}";

var events = new[]
{
    Event("Running {Example}", new { Example = "ThemingDemo", Application = "Demo" }),
    Event("Cart contains {@Items}", new
    {
        Items = new[] { "Tea", "Coffee" },
        SourceContext = "ThemingDemo.Program",
        Application = "Demo",
        Elapsed = 1.34,
        Cached = true,
    }),
    Event("Cart contains {@Items}", new { Items = new[] { "Apricots" }, SourceContext = "ThemingDemo.Program", Application = "Demo" }),
    Event("Order {OrderId} could not be {Action}", new { OrderId = 12345, Action = "shipped", Application = "Demo" },
        level: "Error",
        exception: "System.InvalidOperationException: Carrier unavailable\n" +
                   "   at Shipping.Dispatch(Order order)\n" +
                   "   at Checkout.Complete(Cart cart)"),
};

Section("No theme (the default identity encoder)");
RenderAll(new ExpressionTemplate(textTemplate), events);

foreach (var (name, theme) in new[]
         {
             ("Code", TemplateTheme.Code),
             ("Grayscale", TemplateTheme.Grayscale),
             ("Literate", TemplateTheme.Literate),
             ("Sixteen", TemplateTheme.Sixteen),
         })
{
    Section($"TemplateTheme.{name}");
    RenderAll(new ExpressionTemplate(textTemplate, encoder: TemplateOutputEncoder.Ansi(theme)), events);
}

Section("Level styling and alignment across Seq's level vocabulary — {@Level,-12:t4} {@Level}");
var levels = new ExpressionTemplate("{@Level,-12:t4} {@Level}\n", encoder: TemplateOutputEncoder.Ansi(TemplateTheme.Code));
foreach (var spelling in new[] { "trace", "verbose", "dbug", "info", "notice", "warn", "eror", "fatal", "critical", "emerg", "alert", "panic", "OK" })
    levels.Format(Event("-", level: spelling), Console.Out);

Section("A custom theme derived from Literate (in the style of Microsoft.Extensions.Logging's ConsoleLogger)");
var melon = new AnsiTheme((AnsiTheme)TemplateTheme.Literate, new Dictionary<TemplateThemeStyle, string>
{
    // `Information` is dark green in MEL.
    [TemplateThemeStyle.LevelInformation] = "\x1b[38;5;34m",
    [TemplateThemeStyle.String] = "\x1b[38;5;159m",
    [TemplateThemeStyle.Number] = "\x1b[38;5;159m",
});

var mel = new ExpressionTemplate(
    "{@Level:w4}: {SourceContext}\n" +
    "{#if Scope is not null}" +
    "      {#each s in Scope}=> {s}{#delimit} {#end}\n" +
    "{#end}" +
    "      {@Message}\n" +
    "{@Exception}",
    encoder: TemplateOutputEncoder.Ansi(melon));

mel.Format(Event("Host listening at {ListenUri}",
    new { ListenUri = "https://hello-world.local", SourceContext = "ThemingDemo.Program" }), Console.Out);
mel.Format(Event("HTTP {Method} {Path} responded {StatusCode} in {Elapsed:0.000} ms",
    new
    {
        Method = "GET", Path = "/api/hello", StatusCode = 200, Elapsed = 1.23,
        SourceContext = "ThemingDemo.Program",
        Scope = new[] { "Main", "TextFormattingExample2()" },
    }), Console.Out);
mel.Format(Event("We've reached the end of the line",
    new { SourceContext = "ThemingDemo.Program" }, level: "Warning"), Console.Out);

Section("Terminal safety: themed output strips event-derived control characters");
var hostile = Event("Deleting {Path}", new { Path = "\x1b[33;1mC:\\WINDOWS\x1b[0m" }, level: "Warning");
new ExpressionTemplate("[{@Level:u3}] {@Message}\n", encoder: TemplateOutputEncoder.Ansi(TemplateTheme.Code)).Format(hostile, Console.Out);

Section("TemplateOutputEncoder.Html: content is escaped; unsafe() passes markup through");
var htmlTemplate = new ExpressionTemplate(
    "<p>{@Message}</p>\n<p>{unsafe(Signature)}</p>\n",
    encoder: TemplateOutputEncoder.Html);
htmlTemplate.Format(Event("Posted {Comment}", new
{
    Comment = "<script>alert('pwned')</script>",
    Signature = "<em>The Management</em>",
}), Console.Out);

return;

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"### {title}");
    Console.WriteLine();
}

static void RenderAll(ExpressionTemplate template, IEnumerable<JsonObject> events)
{
    foreach (var evt in events)
        template.Format(evt, Console.Out);
}

// Builds an event document in Seq's emission schema, in the manner of the test suite's
// `Some.Event` helper (property values are serialized with System.Text.Json).
static JsonObject Event(string messageTemplate, object? properties = null, string? level = null, string? exception = null)
{
    var evt = new JsonObject
    {
        ["@t"] = DateTimeOffset.Now.ToString("O"),
        ["@mt"] = messageTemplate,
    };

    // The emission convention: levels starting with "Inf" are omitted.
    if (level != null && !level.StartsWith("Inf", StringComparison.OrdinalIgnoreCase))
        evt["@l"] = level;

    if (exception != null)
        evt["@x"] = exception;

    if (properties != null)
    {
        foreach (var (name, value) in JsonSerializer.SerializeToNode(properties)!.AsObject())
            evt[name] = value?.DeepClone();
    }

    return evt;
}
