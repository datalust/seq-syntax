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

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Compilation;

namespace Seq.Syntax.Expressions.Compilation.Linq;

static class Intrinsics
{
    static readonly EvaluationResult NegativeOne = JsonValue.Create(-1);

    public static JsonArray CollectSequenceElements(EvaluationResult[] elements)
    {
        var arr = new JsonArray();
        foreach (var element in elements)
        {
            if (element.TryGetValue(out var node))
                arr.Add(Values.Clone(node));
        }

        return arr;
    }

    public static JsonArray ExtendSequenceValueWithItem(JsonArray elements, EvaluationResult element)
    {
        // Mutates the array; returned so we can nest calls instead of emitting a block.
        if (element.TryGetValue(out var node))
            elements.Add(Values.Clone(node));
        return elements;
    }

    public static JsonArray ExtendSequenceValueWithSpread(JsonArray elements, EvaluationResult content)
    {
        if (content.TryGetValue(out var node) && node is JsonArray spread)
        {
            foreach (var element in spread)
                elements.Add(Values.Clone(element));
        }

        return elements;
    }

    public static EvaluationResult ConstructSequenceValue(JsonArray elements)
    {
        return elements;
    }

    // Used when an object expression contains no spreads: names are deduplicated at compile
    // time, so members can be added directly, skipping undefined values.
    public static EvaluationResult ConstructStructureValue(string[] names, EvaluationResult[] values)
    {
        var obj = new JsonObject();
        for (var i = 0; i < names.Length; ++i)
        {
            if (values[i].TryGetValue(out var node))
                obj[names[i]] = Values.Clone(node);
        }

        return obj;
    }

    public static JsonObject CollectStructureProperties(string[] names, EvaluationResult[] values)
    {
        var obj = new JsonObject();
        for (var i = 0; i < names.Length; ++i)
            SetOrErase(obj, names[i], values[i]);

        return obj;
    }

    public static JsonObject ExtendStructureValueWithSpread(JsonObject properties, EvaluationResult content)
    {
        if (content.TryGetValue(out var node) && node is JsonObject spread)
        {
            foreach (var (name, value) in spread)
                SetOrErase(properties, name, EvaluationResult.Defined(value));
        }

        return properties;
    }

    public static JsonObject ExtendStructureValueWithProperty(JsonObject properties, string name, EvaluationResult value)
    {
        SetOrErase(properties, name, value);
        return properties;
    }

    // Last-in wins: a redefined member moves to the end, and an undefined value erases the member.
    static void SetOrErase(JsonObject obj, string name, EvaluationResult value)
    {
        obj.Remove(name);
        if (value.TryGetValue(out var node))
            obj.Add(name, Values.Clone(node));
    }

    public static EvaluationResult CompleteStructureValue(JsonObject properties)
    {
        return properties;
    }

    public static bool CoerceToScalarBoolean(EvaluationResult value)
    {
        return Coerce.IsTrue(value);
    }

    public static EvaluationResult IndexOfMatch(EvaluationResult value, Regex regex)
    {
        if (!Coerce.String(value, out var s))
            return EvaluationResult.Undefined;
        
        try
        {
            var m = regex.Match(s);
            if (m.Success)
                return JsonValue.Create(m.Index);
            return NegativeOne;
        }
        catch (RegexMatchTimeoutException)
        {
            // Excessive backtracking on adversarial input is undefined, not a thrown error.
            Diagnostics.RecordSuppressedError(Diagnostics.ErrorKinds.RegexTimeout);
            return EvaluationResult.Undefined;
        }
    }

    public static EvaluationResult GetPropertyValue(EvaluationContext ctx, string propertyName)
    {
        if (!ctx.Document.TryGetPropertyValue(propertyName, out var value))
            return EvaluationResult.Undefined;

        return EvaluationResult.Defined(value);
    }

    public static EvaluationResult GetLocalValue(EvaluationContext ctx, string localName)
    {
        if (!Locals.TryGetValue(ctx.Locals, localName, out var value))
            return EvaluationResult.Undefined;

        return EvaluationResult.Defined(value);
    }

    public static EvaluationResult TryGetStructurePropertyValue(StringComparison sc, EvaluationResult maybeStructure, string name)
    {
        if (maybeStructure.TryGetValue(out var node) && node is JsonObject structure)
        {
            foreach (var (memberName, value) in structure)
            {
                if (memberName.Equals(name, sc))
                    return EvaluationResult.Defined(value);
            }
        }

        return EvaluationResult.Undefined;
    }

    // Use of `CompiledMessageToken` is a layering violation here, but we want to ensure the formatting implementations
    // line up exactly.
    public static string RenderMessage(CompiledMessageToken formatter, EvaluationContext ctx)
    {
        var sw = new StringWriter();
        formatter.Evaluate(ctx, sw);
        return sw.ToString();
    }
}
