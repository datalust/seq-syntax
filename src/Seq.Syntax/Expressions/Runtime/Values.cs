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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Seq.Syntax.Templates.Encoding;

namespace Seq.Syntax.Expressions.Runtime;

/// <summary>
/// Helpers over the native <see cref="JsonNode"/> value representation. A <see cref="JsonValue"/>
/// is either element-backed (parsed from a document) or CLR-backed (constructed by the runtime,
/// including the typed <see cref="DateTimeOffset"/>/<see cref="TimeSpan"/>/<see cref="LevelValue"/>
/// values produced by keyword properties, and the delegates threaded through <c>any()</c>/<c>all()</c>).
/// </summary>
static class Values
{
    /// <summary>
    /// Clone a node for insertion into a new container. Cloning is unconditional: the node may be
    /// shared with the source document, a compiled constant, or an earlier evaluation result, and
    /// attaching a shared node to a new parent would corrupt it.
    /// </summary>
    [return: NotNullIfNotNull(nameof(node))]
    public static JsonNode? Clone(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            switch (Underlying(value))
            {
                // Typed scalars degrade to their JSON string forms on insertion. `DeepClone()` does
                // this too, via each type's serializer support, but for a `LevelValue` the direct
                // conversion avoids a serializer round-trip.
                case LevelValue level:
                    return JsonValue.Create(level.Name);
                // `unsafe()` output has no JSON form to degrade to: `DeepClone()` would serialize
                // the wrapper's own fields into the container.
                case PreEncodedValue:
                    throw PreEncodedValue.Misplaced();
            }
        }

        return node?.DeepClone();
    }

    /// <summary>
    /// The value wrapped by <paramref name="value"/>: a <see cref="JsonElement"/> when
    /// element-backed, otherwise the original CLR object.
    /// </summary>
    public static object Underlying(JsonValue value)
    {
        return value.GetValue<object>();
    }

    public static bool TryGetNumeric(JsonNode? node, out decimal numeric)
    {
        if (node is JsonValue value)
        {
            switch (Underlying(value))
            {
                case JsonElement { ValueKind: JsonValueKind.Number } element:
                    if (element.TryGetDecimal(out numeric))
                        return true;
                    return TryConvertToDecimal(element.GetDouble(), out numeric);
                case decimal dec: numeric = dec; return true;
                case int i: numeric = i; return true;
                case long l: numeric = l; return true;
                case double dbl: return TryConvertToDecimal(dbl, out numeric);
                case float f: return TryConvertToDecimal(f, out numeric);
                case uint ui: numeric = ui; return true;
                case ulong ul: numeric = ul; return true;
                case byte b: numeric = b; return true;
                case sbyte sb: numeric = sb; return true;
                case short s: numeric = s; return true;
                case ushort us: numeric = us; return true;
            }
        }

        numeric = 0;
        return false;
    }

    static bool TryConvertToDecimal(double value, out decimal numeric)
    {
        try
        {
            numeric = (decimal)value;
            return true;
        }
        catch (OverflowException)
        {
            // NaN, ±∞, and magnitudes beyond `decimal` don't convert; they're treated as non-numeric.
            Diagnostics.RecordSuppressedError(Diagnostics.ErrorKinds.NumericRange);
            numeric = 0;
            return false;
        }
    }

    public static bool TryGetString(JsonNode? node, [MaybeNullWhen(false)] out string str)
    {
        if (node is JsonValue value)
        {
            switch (Underlying(value))
            {
                case JsonElement { ValueKind: JsonValueKind.String } element:
                    return TryGetElementString(element, out str);
                case string s: str = s; return true;
                case LevelValue level: str = level.Name; return true;
                case PreEncodedValue: throw PreEncodedValue.Misplaced();
            }
        }

        str = null;
        return false;
    }

    public static bool TryGetElementString(JsonElement element, [MaybeNullWhen(false)] out string str)
    {
        try
        {
            str = element.GetString()!;
            return true;
        }
        catch (InvalidOperationException)
        {
            // Element-backed strings holding invalid UTF-16 (e.g. a lone surrogate) throw on decode; degrade instead.
            Diagnostics.RecordSuppressedError(Diagnostics.ErrorKinds.MalformedString);
            str = null;
            return false;
        }
    }

    public static bool TryGetDateTimeOffset(JsonNode? node, out DateTimeOffset dateTimeOffset)
    {
        if (node is JsonValue value)
        {
            switch (Underlying(value))
            {
                case DateTimeOffset dto:
                    dateTimeOffset = dto;
                    return true;
                case DateTime dt:
                    dateTimeOffset = dt.Kind == DateTimeKind.Unspecified ? new DateTime(dt.Ticks, DateTimeKind.Utc) : dt;
                    return true;
            }
        }

        if (TryGetString(node, out var str) &&
            DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            dateTimeOffset = parsed;
            return true;
        }

        if (TryGetNumeric(node, out var ticks) && ticks >= 0 && ticks <= DateTime.MaxValue.Ticks)
        {
            dateTimeOffset = new DateTime((long)ticks, DateTimeKind.Utc);
            return true;
        }

        dateTimeOffset = default;
        return false;
    }

    public static bool TryGetTimeSpan(JsonNode? node, out TimeSpan timeSpan)
    {
        if (node is JsonValue value && Underlying(value) is TimeSpan ts)
        {
            timeSpan = ts;
            return true;
        }

        if (TryGetString(node, out var str) &&
            TimeSpan.TryParse(str, CultureInfo.InvariantCulture, out var parsed))
        {
            timeSpan = parsed;
            return true;
        }

        if (TryGetNumeric(node, out var ticks) && ticks is >= long.MinValue and <= long.MaxValue)
        {
            timeSpan = TimeSpan.FromTicks((long)ticks);
            return true;
        }

        timeSpan = TimeSpan.Zero;
        return false;
    }

    public static bool TryGetBoolean(JsonNode? node, out bool boolean)
    {
        if (node is JsonValue value)
        {
            switch (Underlying(value))
            {
                case JsonElement { ValueKind: JsonValueKind.True }: boolean = true; return true;
                case JsonElement { ValueKind: JsonValueKind.False }: boolean = false; return true;
                case bool b: boolean = b; return true;
            }
        }

        boolean = false;
        return false;
    }

    public static bool TryGetClrValue<T>(JsonNode? node, [MaybeNullWhen(false)] out T value)
        where T : notnull
    {
        if (node is JsonValue jsonValue && Underlying(jsonValue) is T match)
        {
            value = match;
            return true;
        }

        value = default;
        return false;
    }

    public static EvaluationResult MakeCallable(Func<EvaluationResult, EvaluationResult> callable)
    {
        return JsonValue.Create(callable)!;
    }

    public static EvaluationResult MakeCallable(Func<EvaluationResult, EvaluationResult, EvaluationResult> callable)
    {
        return JsonValue.Create(callable)!;
    }

    /// <summary>
    /// The JSON kind of <paramref name="node"/>, in the Seq query language's `TypeOf()` naming:
    /// <c>object</c>, <c>array</c>, <c>string</c>, <c>number</c>, <c>bool</c>, or <c>null</c>.
    /// </summary>
    public static string KindOf(JsonNode? node)
    {
        return node switch
        {
            null => "null",
            JsonObject => "object",
            JsonArray => "array",
            JsonValue value => Underlying(value) switch
            {
                JsonElement element => element.ValueKind switch
                {
                    JsonValueKind.String => "string",
                    JsonValueKind.Number => "number",
                    JsonValueKind.True or JsonValueKind.False => "bool",
                    _ => "null"
                },
                string or LevelValue or DateTime or DateTimeOffset or TimeSpan => "string",
                bool => "bool",
                Delegate => "function",
                _ => "number"
            },
            _ => "null"
        };
    }
}
