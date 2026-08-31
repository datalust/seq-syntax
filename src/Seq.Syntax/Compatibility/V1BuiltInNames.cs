// Copyright © Datalust and Contributors
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

using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Ast;
using Seq.Syntax.Expressions.Compilation.Transformations;

namespace Seq.Syntax.Compatibility;

sealed class V1BuiltInNames : IdentityTransformer
{
    // Every abbreviated built-in name recognized in v1 maps onto the keyword property with the
    // same meaning; in current syntax the abbreviated spellings would otherwise be plain reads
    // of the corresponding document members. The mapping matters even where a keyword property
    // reads its member verbatim today, because v1 semantics — the `Information` default carried
    // by `@l`, the timestamp parsing applied to `@t` and `@st` — belong to the keyword, not to
    // the raw member.
    readonly Dictionary<string, string> _toCurrentName = new(StringComparer.Ordinal)
    {
        ["t"] = KeywordProperties.Timestamp,
        ["m"] = KeywordProperties.Message,
        ["mt"] = KeywordProperties.MessageTemplate,
        ["l"] = KeywordProperties.Level,
        ["x"] = KeywordProperties.Exception,
        ["i"] = KeywordProperties.EventType,
        ["p"] = KeywordProperties.Properties,
        ["tr"] = KeywordProperties.TraceId,
        ["sp"] = KeywordProperties.SpanId,
        ["st"] = KeywordProperties.Start,
        ["ra"] = KeywordProperties.Resource,
        ["ps"] = KeywordProperties.ParentId,
        ["sk"] = KeywordProperties.SpanKind,
        ["sa"] = KeywordProperties.Scope,
        // `@r` is rewritten to a function call below. Any other `@` name was undefined in v1 and
        // gets the current plain-read behavior instead: current syntax remains usable through
        // this transformation.
    };

    public Expression Rewrite(Expression expression) => base.Transform(expression);

    protected override Expression Transform(AmbientNameExpression px)
    {
        if (!px.IsBuiltIn)
            return px;

        if (_toCurrentName.TryGetValue(px.PropertyName, out var current))
            return new AmbientNameExpression(current, true);

        if (px.PropertyName == "r")
            return new CallExpression(false, V1CompatibilityFunctions.RenderingsFunctionName);

        return px;
    }
}
