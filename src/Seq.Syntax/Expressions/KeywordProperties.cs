// Copyright © Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Globalization;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions.Compilation.Linq;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Compilation;

namespace Seq.Syntax.Expressions;

/// <summary>
/// The keyword properties: exact, case-sensitive <c>@</c>-prefixed names that desugar at compile
/// time into intrinsics over the event JSON document. Any other <c>@</c> identifier is a plain
/// read of the correspondingly-named document member.
/// </summary>
static class KeywordProperties
{
    public const string Timestamp = "Timestamp";
    public const string Level = "Level";
    public const string Message = "Message";
    public const string MessageTemplate = "MessageTemplate";
    public const string Exception = "Exception";
    public const string EventType = "EventType";
    public const string Properties = "Properties";
    public const string Id = "Id";
    public const string TraceId = "TraceId";
    public const string SpanId = "SpanId";
    public const string ParentId = "ParentId";
    public const string SpanKind = "SpanKind";
    public const string Start = "Start";
    public const string Elapsed = "Elapsed";
    public const string Resource = "Resource";
    public const string Scope = "Scope";
    public const string Arrived = "Arrived";
    public const string Document = "Document";
    public const string Data = "Data";

    static string? GetStringField(JsonObject eventJson, string field)
    {
        return eventJson.TryGetPropertyValue(field, out var node) &&
               Values.TryGetString(node, out var s)
            ? s
            : null;
    }

    static DateTimeOffset? GetTimestampField(JsonObject eventJson, string field)
    {
        return GetStringField(eventJson, field) is { } s &&
               DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto
            : null;
    }

    public static EvaluationResult GetTimestamp(JsonObject eventJson)
    {
        return GetTimestampField(eventJson, "@t") is { } dto
            ? JsonValue.Create(dto)
            : EvaluationResult.Undefined;
    }

    internal static LevelValue GetLevelValue(JsonObject eventJson)
    {
        return LevelMapping.ToLevelValue(GetStringField(eventJson, "@l"));
    }

    public static EvaluationResult GetLevel(JsonObject eventJson)
    {
        return JsonValue.Create(GetLevelValue(eventJson))!;
    }

    public static EvaluationResult GetMessage(CompiledMessageToken formatter, EvaluationContext ctx)
    {
        if (!ctx.Document.ContainsKey("@m") && !ctx.Document.ContainsKey("@mt"))
            return EvaluationResult.Undefined;

        return JsonValue.Create(Intrinsics.RenderMessage(formatter, ctx));
    }

    public static EvaluationResult GetEventType(JsonObject eventJson)
    {
        if (GetStringField(eventJson, "@i") is { } hex &&
            uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var i))
        {
            return JsonValue.Create(i);
        }

        if (GetStringField(eventJson, "@mt") is { } messageTemplate)
            return JsonValue.Create(EventIdHash.Compute(messageTemplate));

        return EvaluationResult.Undefined;
    }

    // All members that don't name built-in `@` fields, with `@@`-escaped names un-escaped to
    // their real `@…` spellings.
    public static EvaluationResult GetProperties(JsonObject eventJson)
    {
        var properties = new JsonObject();
        foreach (var (name, value) in eventJson)
        {
            if (name.StartsWith("@@", StringComparison.Ordinal))
                properties[name[1..]] = Values.Clone(value);
            else if (!name.StartsWith('@'))
                properties[name] = Values.Clone(value);
        }

        return properties;
    }

    // The complete event document, verbatim.
    public static EvaluationResult GetData(JsonObject eventJson)
    {
        return Values.Clone(eventJson);
    }

    public static EvaluationResult GetStart(JsonObject eventJson)
    {
        return GetTimestampField(eventJson, "@st") is { } dto
            ? JsonValue.Create(dto)
            : EvaluationResult.Undefined;
    }

    public static EvaluationResult GetElapsed(JsonObject eventJson)
    {
        if (GetTimestampField(eventJson, "@t") is { } t &&
            GetTimestampField(eventJson, "@st") is { } st)
            return JsonValue.Create(t - st)!;

        return EvaluationResult.Undefined;
    }
}
