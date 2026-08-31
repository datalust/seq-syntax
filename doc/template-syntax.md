---
collection: docs
layout: doc-page
slug: template-syntax
title: Template Syntax
parent: installing-output-apps
order: 4
---
Many Seq apps can format output, for example email notifications or HTTP request bodies, using the [*Seq.Syntax* templating library](https://nuget.org/packages/seq.syntax) .

These templates closely follow the syntax of the Seq query language, adding features to enable generating plain text, HTML, or JSON.

Templates are expanded in the context of an event, such as this one:

![](assets:45ee1ef-image)

Behind the scenes, all of the details of the event - its timestamp, level, message, exception, and properties, are accessible. In the event document, the built-in fields have compact `@`-prefixed names like `@t`; templates read them through built-in properties such as `@Timestamp`. Other regular properties - `RequestUrl` for example - have simple names just as you'd expect.

To get a look at how templates see events, in the view above, choose *Export > Copy raw JSON*. If you pretty-print the result, it should look something like:

```json
{
  "@t":"2024-04-26T01:46:04.8772700Z",
  "@mt":"HTTP {RequestMethod} {RequestPath}",
  "@m":"HTTP GET /api/products",
  "@i":"e80d5c02",
  "@l":"Error",
  "@x":"System.ObjectDisposedException: Cannot access a disposed object...",
  "@tr":"d7a8be8ffe34c7b823c40733d6572e6e",
  "@sp":"81052e753d7d7440",
  "@st":"2024-04-26T01:46:04.8735910Z",
  "@sk":"Internal",
  "Application":"Roastery Web Frontend",
  "Origin":"seqcli sample ingest",
  "RequestId":"6c44ff9f0cb6e5446c9bc9",
  "RequestMethod":"GET",
  "RequestPath":"/api/products",
  "SourceContext":"Roastery.Web.RequestLoggingMiddleware",
  "StatusCode":500
}
```

Now, assume we want to format a short message letting us know that something has broken. Here's a Seq template which does that:

```
{RequestMethod} {RequestPath} failed with status code {StatusCode}
```

The result of evaluating the template will be:

```
GET /api/products failed with status code 500
```

## Built-in properties

The details of the event beyond its first-class properties are read through built-in properties:

| Built-in | Value |
|---|---|
| `@Timestamp` | The event's timestamp (`@t`), as a date-time |
| `@Level` | The event's level (`@l`); `Information` when the event carries no level |
| `@Message` | The rendered message: `@m` when present, otherwise `@mt` rendered over the event's properties |
| `@MessageTemplate` | The raw message template (`@mt`) |
| `@Exception` | The exception text (`@x`) |
| `@EventType` | The numeric event type: `@i` parsed as hexadecimal, or the message template's hash |
| `@Properties` | An object carrying all of the event's first-class properties |
| `@Id` | The event id (`@seqid`) |
| `@TraceId`, `@SpanId` | The trace and span ids (`@tr`, `@sp`) |
| `@ParentId`, `@SpanKind` | The parent span id and span kind (`@ps`, `@sk`) |
| `@Start` | The span's start (`@st`), as a date-time |
| `@Elapsed` | The span's duration: `@Timestamp` - `@Start`, as a time span |
| `@Resource`, `@Scope` | Resource and instrumentation scope attributes (`@ra`, `@sa`) |

The built-in property names are exact and case-sensitive. Any other `@`-prefixed identifier reads the correspondingly-named field of the event document verbatim: `{@l}` is the raw value of the `@l` field - a field that is absent from Information-level events - while `{@Level}` is always defined. The built-in properties are usually what you want.

{.block-info}
> Earlier versions of *Seq.Syntax* instead treated the abbreviated names `@t`, `@m`, `@mt`, `@l`, `@x`, `@i`, `@p`, and `@r` as the built-in properties - for example, `@l` carried the parsed level, defaulting to `Information` when the event had none. In current syntax these spellings read the raw event fields, as described above.

## Basic syntax

Between curly braces (called "holes"), templates support:

* The [built-in Seq event properties](docs:built-in-properties-and-functions) listed above, including `@Level`, `@Message`, and `@Exception`,
* First-class properties of events and alerts, like `RequestMethod` in the example above,
* Most Seq [scalar functions](docs:scalar-functions), such as `ToIsoString()`, `Coalesce()`, `Substring()`, `IndexOf()`, and so on,
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

Array and object literals support the spread operator: `[1, 2, ..others]`, `{a: 1, ..others}`. Specifying an undefined property in an object literal will remove it from the result: `{..User, Email: Undefined()}`.

## JSON output

To construct a well-formed JSON document based on event properties, construct a Seq object literal such as `{n: 42}`, and place it inside a hole, making sure to add a space between the "hole" braces and the object - `{ {n: 42} }` (otherwise, the double braces will be interpreted as an escape sequence).

For example:

```
{ {
   Timestamp: @t,
   Source: 'Seq',
   Contact: { Name: CompanyName, Type: 'Company' }
} }
```

Will result in output resembling:

```json
{
  "Timestamp": "2021-09-15T06:33:21.432",
  "Source": "Seq",
  "Contact": {
    "Name": "Datalust Pty Ltd",
    "Type": "Company"
  }
}
```

{.block-warning}
> Note that the outermost curly braces here must have a space between them, like `{ {`, and **not** `{{`. Only the outermost pair of braces need to be doubled in this manner: their purpose is to create a template "hole" (the first `{`) and then construct an object (the second `{`) which will be serialized to JSON as the value of the hole.

## Conditionals and repetition

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

Holes like `{#if` and `{#else` that begin with `{#` are called template *directives*. Directives don't get substituted directly into the output.

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

{.block-info}
> Template directives like `{#if}` only work in the outermost template - they can't be used as expressions nested inside holes or objects. In an expression, Seq's regular `if` construct can be used.

## Resources and further reading

Seq templates originated from, and share a significant amount of implementation with, the *Serilog.Expressions* templating system.

Many detailed examples for *Serilog.Expressions* carry over to Seq templates, keeping in mind that *Serilog.Expressions* uses the abbreviated built-in names (`@l`, `@m`, and so on) where Seq templates use the built-in properties listed above (`@Level`, `@Message`, ...); see:

* Recipes for [generating plain text output](https://nblumhardt.com/2021/06/customize-serilog-text-output/)
* Recipes for [generating JSON output](https://nblumhardt.com/2021/06/customize-serilog-json-output/)
* The [*Serilog.Expressions* README](https://github.com/serilog/serilog-expressions/#readme)