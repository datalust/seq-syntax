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
using System.Text.Json;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions.Runtime;

namespace Seq.Syntax.Expressions.Ast;

class ConstantExpression : Expression
{
    public ConstantExpression(EvaluationResult constant)
    {
        Constant = constant;
    }

    public EvaluationResult Constant { get; }

    public override string ToString()
    {
        return Constant.TryGetValue(out var node) ? Display(node) : "undefined()";
    }

    // Renders the constant in expression syntax, primarily for diagnostic round-tripping.
    static string Display(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return "null";
            case JsonArray array:
                return "[" + string.Join(", ", array.Select(Display)) + "]";
            case JsonObject obj:
                return "{" + string.Join(", ", obj.Select(m => $"'{SeqExpression.EscapeStringContent(m.Key)}': {Display(m.Value)}")) + "}";
            default:
                if (Values.TryGetString(node, out var s))
                    return "'" + SeqExpression.EscapeStringContent(s) + "'";
                if (Values.TryGetBoolean(node, out var b))
                    return b ? "true" : "false";
                if (Values.TryGetNumeric(node, out var n))
                    return n.ToString(CultureInfo.InvariantCulture);
                return node.ToJsonString(new JsonSerializerOptions());
        }
    }
}
