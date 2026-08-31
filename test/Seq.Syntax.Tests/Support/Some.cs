using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Seq.Syntax.Templates.Messages;

namespace Seq.Syntax.Tests.Support;

static class Some
{
    public static JsonObject InformationEvent(string messageTemplate = "Hello, world!", params object?[] propertyValues)
    {
        return Event(null, default, default, messageTemplate, propertyValues);
    }

    public static JsonObject WarningEvent(string messageTemplate = "Hello, world!", params object?[] propertyValues)
    {
        return Event("Warning", default, default, messageTemplate, propertyValues);
    }

    public static JsonObject Event(string? level = null, ActivityTraceId traceId = default, ActivitySpanId spanId = default, string messageTemplate = "Hello, world!", params object?[] propertyValues)
    {
        var evt = new JsonObject
        {
            ["@t"] = DateTimeOffset.Now.ToString("O"),
            ["@mt"] = messageTemplate
        };

        // The emission convention: levels starting with "Inf" are omitted.
        if (level != null && !level.StartsWith("Inf", StringComparison.OrdinalIgnoreCase))
            evt["@l"] = level;

        if (traceId != default)
            evt["@tr"] = traceId.ToString();

        if (spanId != default)
            evt["@sp"] = spanId.ToString();

        // Pairs template holes with values positionally.
        var next = 0;
        foreach (var token in MessageTemplateParser.Parse(messageTemplate))
        {
            if (token is not PropertyToken pt)
                continue;

            if (next >= propertyValues.Length)
                throw new InvalidOperationException("Template could not be bound.");

            evt[pt.PropertyName] = JsonSerializer.SerializeToNode(propertyValues[next]);
            next += 1;
        }

        return evt;
    }

    public static JsonNode JsonNode()
    {
        return JsonValue.Create(Guid.NewGuid().ToString("N"));
    }
}
