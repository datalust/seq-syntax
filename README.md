# Seq Syntax

This repository implements Seq-style expressions and the [Seq template language](https://docs.datalust.co/docs/template-syntax) over structured event data.

Expressions and templates evaluate against event JSON documents in Seq's emission schema — the
format delivered to Seq apps and API consumers — represented as `System.Text.Json.Nodes.JsonObject`:

```csharp
var eventJson = JsonNode.Parse(json)!.AsObject();

var expr = SeqExpression.Compile("@Level = 'Warning' and Contains(@Message, 'coffee')");
if (expr(eventJson).IsTrue()) { /* ... */ }

var template = new ExpressionTemplate("[{@Timestamp:HH:mm:ss} {@Level:u3}] {@Message}");
template.Format(eventJson, Console.Out);
```

Keyword properties (`@Timestamp`, `@Level`, `@Message`, …) provide typed views over the
document's `@t`, `@l`, `@mt`/`@m` fields and friends; any other `@` identifier reads the
correspondingly-named document member verbatim.

> **Migrating from 1.x**: version 2.0 removed the Serilog dependency and is a breaking change —
> `SerilogExpression` became `SeqExpression`, `LogEvent` inputs became `JsonObject`, and `@t`,
> `@l`, `@m`, and other short `@` names became plain JSON reads.

```
Error in {Environment}!
```

Here, `Environment` is an event property, producing a message subject like `Error in Production!`.

### Basic syntax

Templates support:

 * Most built-in Seq event properties, including `@Level`, `@Message`, and `@Exception`,
 * First-class properties of events and alerts, like `Environment` in the example above,
 * Most Seq scalar functions, such as `ToIsoString()`, `Coalesce()`, `Substring()`, `IndexOf()`, and so on,
 * Seq operators such as `=`, `<>`, `<`, `>`, `like`, `in`, `is null`,
 * Constant numbers `123.4`, strings `'abc'`, Boolean `true` and `false`, and `null`,
 * Arrays delimited with brackets `[]` and zero-based indexing,
 * Object literals using braces `{}` that support string-based indexing,
 * Most other Seq expression language features.

Literal braces in templated text fields can be escaped by doubling, `{{` and `}}`.

Formatting of dates and numbers can be achieved using .NET format strings following a colon, e.g.:

```
Completed in {Elapsed:0.00} ms
```

### Conditionals and repetition

To conditionally include text, use `{#if expr}`:

```
{#if Count = 0}
  Nothing here
{#else if Count = 1}
  Only one
{#else}
  Found {Count} items
{#end}
```

The `else`/`else if` blocks are optional.

To iterate over array elements or object properties use `{#each e in expr}` or `{#each k, v in expr}`:

```
{#each name, value in @Properties}
  {name} is {value}
{#delimit}
  ---
{#else}
  No properties
{#end}
```

The `delimit` and `else` blocks are optional.

### ANSI terminal output

Pass one of the built-in themes (`Code`, `Grayscale`, `Literate`, or `Sixteen`) to color
terminal output:

```csharp
var template = new ExpressionTemplate(
    "[{@Timestamp:HH:mm:ss} {@Level:u3}] {@Message}\n{@Exception}",
    theme: TemplateTheme.Code);
```

Themes can be customized by overriding the styles of a base theme:

```csharp
var custom = new AnsiTheme((AnsiTheme)TemplateTheme.Literate, new Dictionary<TemplateThemeStyle, string>
{
    [TemplateThemeStyle.LevelInformation] = "\x1b[38;5;34m",
});
```

### Escaping text inserted into HTML message bodies

`TemplateOutputEncoder.Html` escapes event-derived values automatically, so they can be safely
inserted into HTML attributes and element bodies (excluding script and style contexts, in which
no safe escaping is possible).

```csharp
var template = new ExpressionTemplate(
    "<p>{@Message}</p>",
    encoder: TemplateOutputEncoder.Html);
```

Where an event property is known to contain trusted, well-formed HTML, `{unsafe(Markup)}`
substitutes it without escaping.

## Acknowledgements

This project is based on code from [Serilog](https://github.com/serilog/serilog) and [Serilog.Expressions](https://github.com/serilog/serilog-expressions).