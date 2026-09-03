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
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions.Compilation.Linq;
using Seq.Syntax.Templates.Rendering;

// ReSharper disable ForCanBeConvertedToForeach, InvertIf, MemberCanBePrivate.Global, UnusedMember.Global, InconsistentNaming

namespace Seq.Syntax.Expressions.Runtime;

static class RuntimeOperators
{
    static readonly JsonSerializerOptions ToJsonSerializerOptions = new()
    {
        // Avoids defense-in-depth encoding of HTML content chars/non-ASCII data, which renders the results unreadable
        // for many non-Latin languages.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static EvaluationResult ScalarBoolean(bool value)
    {
        return JsonValue.Create(value);
    }

    public static EvaluationResult Undefined()
    {
        return EvaluationResult.Undefined;
    }

    // `decimal` arithmetic over data-supplied operands can overflow; an overflowing result is undefined.
    static EvaluationResult Arithmetic(decimal left, decimal right, Func<decimal, decimal, bool> guard, Func<decimal, decimal, decimal> op)
    {
        if (!guard(left, right))
            return EvaluationResult.Undefined;

        try
        {
            return JsonValue.Create(op(left, right));
        }
        catch (OverflowException)
        {
            Diagnostics.RecordSuppressedError(Diagnostics.ErrorKinds.ArithmeticOverflow);
            return EvaluationResult.Undefined;
        }
    }

    public static EvaluationResult _Internal_Add(decimal left, decimal right)
    {
        return Arithmetic(left, right, static (_, _) => true, static (l, r) => l + r);
    }

    public static EvaluationResult _Internal_Subtract(decimal left, decimal right)
    {
        return Arithmetic(left, right, static (_, _) => true, static (l, r) => l - r);
    }

    public static EvaluationResult _Internal_Multiply(decimal left, decimal right)
    {
        return Arithmetic(left, right, static (_, _) => true, static (l, r) => l * r);
    }

    public static EvaluationResult _Internal_Divide(decimal left, decimal right)
    {
        return Arithmetic(left, right, static (_, r) => r != 0, static (a, b) => a / b);
    }

    public static EvaluationResult _Internal_Modulo(decimal left, decimal right)
    {
        return Arithmetic(left, right, static (_, r) => r != 0, static (a, b) => a % b);
    }

    public static EvaluationResult _Internal_Power(decimal left, decimal right)
    {
        return Arithmetic(left, right, static (_, _) => true, static (a, b) => (decimal)Math.Pow((double)a, (double)b));
    }

    public static EvaluationResult _Internal_And(EvaluationResult left, EvaluationResult right)
    {
        throw new InvalidOperationException("Logical operators should be evaluated intrinsically.");
    }

    public static EvaluationResult _Internal_Or(EvaluationResult left, EvaluationResult right)
    {
        throw new InvalidOperationException("Logical operators should be evaluated intrinsically.");
    }

    public static EvaluationResult _Internal_LessThanOrEqual(decimal left, decimal right)
    {
        return ScalarBoolean(left <= right);
    }

    public static EvaluationResult _Internal_LessThan(decimal left, decimal right)
    {
        return ScalarBoolean(left < right);
    }

    public static EvaluationResult _Internal_GreaterThan(decimal left, decimal right)
    {
        return ScalarBoolean(left > right);
    }

    public static EvaluationResult _Internal_GreaterThanOrEqual(decimal left, decimal right)
    {
        return ScalarBoolean(left >= right);
    }

    public static EvaluationResult _Internal_Equal(StringComparison sc, JsonNode? left, JsonNode? right)
    {
        // Undefined values propagate through comparisons; the caller short-circuits when either
        // operand is undefined, so here both are defined.
        try
        {
            return ScalarBoolean(UnboxedEqualHelper(sc, left, right));
        }
        catch (InsufficientExecutionStackException)
        {
            Diagnostics.RecordSuppressedError(Diagnostics.ErrorKinds.RecursionDepth);
            return EvaluationResult.Undefined;
        }
    }

    // Structural equality over nodes; JSON null equals JSON null. Recurses over data depth, so it
    // guards the stack (see the callers, which turn a too-deep comparison into `undefined`).
    static bool UnboxedEqualHelper(StringComparison sc, JsonNode? left, JsonNode? right)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();

        if (Values.TryGetNumeric(left, out var ln) &&
            Values.TryGetNumeric(right, out var rn))
            return ln == rn;

        if (Values.TryGetString(left, out var ls) &&
            Values.TryGetString(right, out var rs))
            return ls.Equals(rs, sc);

        if (left is null || right is null)
            return left is null && right is null;

        if (left is JsonValue && right is JsonValue)
        {
            if (Values.TryGetBoolean(left, out var lb) &&
                Values.TryGetBoolean(right, out var rb))
                return lb == rb;

            if (Values.TryGetClrValue<DateTimeOffset>(left, out var ldto) &&
                Values.TryGetClrValue<DateTimeOffset>(right, out var rdto))
                return ldto == rdto;

            if (Values.TryGetClrValue<DateTime>(left, out var ldt) &&
                Values.TryGetClrValue<DateTime>(right, out var rdt))
                return ldt == rdt;

            if (Values.TryGetClrValue<TimeSpan>(left, out var lts) &&
                Values.TryGetClrValue<TimeSpan>(right, out var rts))
                return lts == rts;

            return false;
        }

        if (left is JsonArray la && right is JsonArray ra)
        {
            if (la.Count != ra.Count)
                return false;

            for (var i = 0; i < la.Count; ++i)
            {
                if (!UnboxedEqualHelper(sc, la[i], ra[i]))
                    return false;
            }

            return true;
        }

        if (left is JsonObject lo && right is JsonObject ro)
        {
            if (lo.Count != ro.Count)
                return false;

            foreach (var (name, value) in lo)
            {
                if (!ro.TryGetPropertyValue(name, out var other) ||
                    !UnboxedEqualHelper(sc, value, other))
                    return false;
            }

            return true;
        }

        return false;
    }

    public static EvaluationResult _Internal_In(StringComparison sc, JsonNode? item, JsonNode? collection)
    {
        if (collection is JsonArray arr)
        {
            try
            {
                for (var i = 0; i < arr.Count; ++i)
                {
                    if (UnboxedEqualHelper(sc, arr[i], item))
                        return ScalarBoolean(true);
                }
            }
            catch (InsufficientExecutionStackException)
            {
                Diagnostics.RecordSuppressedError(Diagnostics.ErrorKinds.RecursionDepth);
                return EvaluationResult.Undefined;
            }

            return ScalarBoolean(false);
        }

        return EvaluationResult.Undefined;
    }

    public static EvaluationResult _Internal_NotIn(StringComparison sc, JsonNode? item, JsonNode? collection)
    {
        return _Internal_StrictNot(_Internal_In(sc, item, collection));
    }

    public static EvaluationResult _Internal_NotEqual(StringComparison sc, JsonNode? left, JsonNode? right)
    {
        try
        {
            return ScalarBoolean(!UnboxedEqualHelper(sc, left, right));
        }
        catch (InsufficientExecutionStackException)
        {
            Diagnostics.RecordSuppressedError(Diagnostics.ErrorKinds.RecursionDepth);
            return EvaluationResult.Undefined;
        }
    }

    public static EvaluationResult _Internal_Negate(decimal operand)
    {
        return JsonValue.Create(-operand);
    }

    public static EvaluationResult Round(decimal number, decimal places)
    {
        if (places < 0 || places > 28) // `Math.Round(decimal, int)` accepts 0..28.
            return EvaluationResult.Undefined;

        return JsonValue.Create(Math.Round(number, (int)places));
    }

    public static EvaluationResult _Internal_Not(EvaluationResult operand)
    {
        if (!operand.IsDefined)
            return ScalarBoolean(true);

        return Coerce.Boolean(operand, out var b) ?
            ScalarBoolean(!b) :
            EvaluationResult.Undefined;
    }

    public static EvaluationResult _Internal_StrictNot(EvaluationResult operand)
    {
        return Coerce.Boolean(operand, out var b) ?
            ScalarBoolean(!b) :
            EvaluationResult.Undefined;
    }

    public static EvaluationResult Contains(StringComparison sc, string @string, string substring)
    {
        return ScalarBoolean(@string.Contains(substring, sc));
    }

    public static EvaluationResult IndexOf(StringComparison sc, string @string, string substring)
    {
        return JsonValue.Create(@string.IndexOf(substring, sc));
    }

    public static EvaluationResult LastIndexOf(StringComparison sc, string @string, string substring)
    {
        return JsonValue.Create(@string.LastIndexOf(substring, sc));
    }

    public static EvaluationResult Length(JsonNode? value)
    {
        if (Coerce.String(value, out var s))
            return JsonValue.Create(s.Length);

        if (value is JsonArray arr)
            return JsonValue.Create(arr.Count);

        return EvaluationResult.Undefined;
    }

    public static EvaluationResult StartsWith(StringComparison sc, string value, string substring)
    {
        return ScalarBoolean(value.StartsWith(substring, sc));
    }

    public static EvaluationResult EndsWith(StringComparison sc, string value, string substring)
    {
        return ScalarBoolean(value.EndsWith(substring, sc));
    }

    public static EvaluationResult IsDefined(EvaluationResult value)
    {
        return ScalarBoolean(value.IsDefined);
    }

    public static EvaluationResult ElementAt(StringComparison sc, JsonNode? items, JsonNode? index)
    {
        if (items is JsonArray arr && Coerce.Numeric(index, out var ix))
        {
            // Range-check before narrowing: a large `decimal` throws on the cast to `int`.
            if (ix != Math.Floor(ix) || ix < 0 || ix >= arr.Count)
                return EvaluationResult.Undefined;

            return EvaluationResult.Defined(arr[(int)ix]);
        }

        if (items is JsonObject && Coerce.String(index, out var s))
        {
            return Intrinsics.TryGetStructurePropertyValue(sc, EvaluationResult.Defined(items), s);
        }

        return EvaluationResult.Undefined;
    }

    public static EvaluationResult _Internal_Any(JsonNode? items, JsonNode? predicate)
    {
        if (!Coerce.Predicate(predicate, out var pred))
            return EvaluationResult.Undefined;

        if (items is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; ++i)
            {
                if (Coerce.IsTrue(pred(EvaluationResult.Defined(arr[i]))))
                    return ScalarBoolean(true);
            }

            return ScalarBoolean(false);
        }

        if (items is JsonObject obj)
        {
            foreach (var (_, value) in obj)
            {
                if (Coerce.IsTrue(pred(EvaluationResult.Defined(value))))
                    return ScalarBoolean(true);
            }

            return ScalarBoolean(false);
        }

        return EvaluationResult.Undefined;
    }

    public static EvaluationResult _Internal_All(JsonNode? items, JsonNode? predicate)
    {
        if (!Coerce.Predicate(predicate, out var pred))
            return EvaluationResult.Undefined;

        if (items is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; ++i)
            {
                if (!Coerce.IsTrue(pred(EvaluationResult.Defined(arr[i]))))
                    return ScalarBoolean(false);
            }

            return ScalarBoolean(true);
        }

        if (items is JsonObject obj)
        {
            foreach (var (_, value) in obj)
            {
                if (!Coerce.IsTrue(pred(EvaluationResult.Defined(value))))
                    return ScalarBoolean(false);
            }

            return ScalarBoolean(true);
        }

        return EvaluationResult.Undefined;
    }

    public static EvaluationResult TagOf(JsonNode? value)
    {
        if (value is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("$type", out var tag) &&
                Values.TryGetString(tag, out var tagName))
                return JsonValue.Create(tagName);

            return EvaluationResult.Null;
        }

        return EvaluationResult.Undefined;
    }

    public static EvaluationResult TypeOf(EvaluationResult value)
    {
        if (!value.TryGetValue(out var node))
            return JsonValue.Create("undefined");

        return JsonValue.Create(Values.KindOf(node));
    }

    public static EvaluationResult _Internal_IsNull(EvaluationResult value)
    {
        return ScalarBoolean(!value.TryGetValue(out var node) || node is null);
    }

    public static EvaluationResult _Internal_IsNotNull(EvaluationResult value)
    {
        return ScalarBoolean(value.TryGetValue(out var node) && node is not null);
    }

    // Ideally this will be compiled as a short-circuiting intrinsic
    public static EvaluationResult Coalesce(EvaluationResult value0, EvaluationResult value1)
    {
        if (!value0.TryGetValue(out var node) || node is null)
            return value1;

        return value0;
    }

    public static EvaluationResult Substring(string @string, decimal startIndex, decimal? length = null)
    {
        if (startIndex < 0 || startIndex >= @string.Length || (int)startIndex != startIndex)
            return EvaluationResult.Undefined;

        // A missing `length` (undefined) takes the rest of the string; a null or non-numeric one has
        // already short-circuited the call at the binding site, so here it is always a number.
        if (length is not { } len)
            return JsonValue.Create(@string.Substring((int)startIndex));

        // Reject a negative or non-integral length in `decimal` space; a large one is handled by the
        // `len + startIndex > @string.Length` fast path below, so the surviving `(int)len` cast is always in range.
        if (len < 0 || len != Math.Floor(len))
            return EvaluationResult.Undefined;

        if (len + startIndex > @string.Length)
            return JsonValue.Create(@string.Substring((int)startIndex));

        return JsonValue.Create(@string.Substring((int)startIndex, (int)len));
    }

    public static EvaluationResult Replace(StringComparison sc, string @string, string substring, string replacement)
    {
        return JsonValue.Create(@string.Replace(substring, replacement, sc));
    }

    public static EvaluationResult Concat(string string0, string string1)
    {
        return JsonValue.Create(string0 + string1);
    }

    public static EvaluationResult IndexOfMatch(StringComparison sc, EvaluationResult corpus, EvaluationResult regex)
    {
        throw new InvalidOperationException("Regular expression evaluation is intrinsic.");
    }

    public static EvaluationResult IsMatch(StringComparison sc, EvaluationResult corpus, EvaluationResult regex)
    {
        throw new InvalidOperationException("Regular expression evaluation is intrinsic.");
    }

    // Ideally this will be compiled as a short-circuiting intrinsic
    public static EvaluationResult _Internal_IfThenElse(
        EvaluationResult condition,
        EvaluationResult consequent,
        EvaluationResult alternative)
    {
        return Coerce.IsTrue(condition) ? consequent : alternative;
    }

    public static EvaluationResult ToString(IFormatProvider? formatProvider, JsonNode? value, EvaluationResult format = default)
    {
        if (value is not JsonValue scalar)
            return EvaluationResult.Undefined;

        string? fmt = null;
        if (format.TryGetValue(out var formatNode) && formatNode is not null)
        {
            if (!Values.TryGetString(formatNode, out fmt))
                return EvaluationResult.Undefined;
        }

        var toString = Values.FormatScalarValue(Values.Underlying(scalar), fmt, formatProvider);
        return toString == null ? EvaluationResult.Undefined : JsonValue.Create(toString);
    }
    
    public static EvaluationResult UtcDateTime(DateTimeOffset dateTime)
    {
        return JsonValue.Create(dateTime.UtcDateTime);
    }

    public static EvaluationResult Now()
    {
        return JsonValue.Create(DateTimeOffset.Now);
    }

    public static EvaluationResult ToLower(CultureInfo? culture, string value)
    {
        return JsonValue.Create(value.ToLower(culture));
    }

    public static EvaluationResult ToUpper(CultureInfo? culture, string value)
    {
        return JsonValue.Create(value.ToUpper(culture));
    }

    public static EvaluationResult UriEncode(string value)
    {
        return JsonValue.Create(Uri.EscapeDataString(value));
    }

    public static EvaluationResult ToJson(JsonNode? value)
    { 
        // `Values.Clone` handles the level wrapper type and rejects pre-encoded `unsafe()` output.
        var node = value is JsonValue ? Values.Clone(value) : value;
        return JsonValue.Create(node?.ToJsonString(ToJsonSerializerOptions) ?? "null");
    }

    public static EvaluationResult FromJson(string json)
    {
        try
        {
            // `Parse` returns null for the JSON literal `null`.
            return EvaluationResult.Defined(JsonNode.Parse(json));
        }
        catch (JsonException)
        {
            Diagnostics.RecordSuppressedError(Diagnostics.ErrorKinds.InvalidJson);
            return EvaluationResult.Undefined;
        }
    }

    public static EvaluationResult IsSpan(JsonObject eventJson)
    {
        return ScalarBoolean(eventJson.ContainsKey("@tr") &&
                             eventJson.ContainsKey("@sp") &&
                             eventJson.ContainsKey("@st"));
    }

    public static EvaluationResult IsRootSpan(JsonObject eventJson)
    {
        return ScalarBoolean(eventJson.ContainsKey("@tr") &&
                             eventJson.ContainsKey("@sp") &&
                             eventJson.ContainsKey("@st") &&
                             !eventJson.ContainsKey("@ps"));
    }

    public static EvaluationResult FromUnixEpoch(DateTimeOffset dateTime)
    {
        return JsonValue.Create(dateTime.UtcDateTime - DateTime.UnixEpoch)!;
    }

    public static EvaluationResult TotalMilliseconds(TimeSpan timeSpan)
    {
        return JsonValue.Create(timeSpan.Ticks / (decimal)TimeSpan.TicksPerMillisecond);
    }
}
