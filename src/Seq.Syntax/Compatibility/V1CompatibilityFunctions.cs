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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Compilation;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Templates.Messages;

namespace Seq.Syntax.Compatibility;

/// <summary>
/// Implements v1 built-ins with no keyword-property equivalent. <see cref="V1BuiltInNames"/>
/// rewrites the names into calls to these functions.
/// </summary>
class V1CompatibilityFunctions : NameResolver
{
    internal const string RenderingsFunctionName = "_V1Renderings";

    public override bool TryResolveFunctionName(string name, [NotNullWhen(true)] out MethodInfo? implementation)
    {
        if (name == RenderingsFunctionName)
        {
            implementation = typeof(V1CompatibilityFunctions).GetMethod(RenderingsFunctionName)!;
            return true;
        }

        implementation = null;
        return false;
    }

    /// <summary>
    /// The v1 <c>@r</c> renderings array: each hole in <c>@mt</c> that carries a format string,
    /// rendered over the event; undefined when no hole has a format string. v1 computed this from
    /// the parsed message template rather than reading an <c>@r</c> document member, so hole names
    /// resolve the way <c>@Message</c> resolves them (dotted names are paths).
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static EvaluationResult _V1Renderings(CultureInfo? formatProvider, JsonObject eventJson)
    {
        if (!eventJson.TryGetPropertyValue("@mt", out var mt) ||
            !Values.TryGetString(mt, out var messageTemplate))
        {
            return EvaluationResult.Undefined;
        }

        JsonArray? renderings = null;
        CompiledMessageToken? formatter = null;
        foreach (var token in MessageTemplateParser.Parse(messageTemplate))
        {
            if (token is not PropertyToken { Format: not null } pt)
                continue;

            renderings ??= [];
            formatter ??= new CompiledMessageToken(formatProvider, null, TemplateOutputEncoder.Default);

            var space = new StringWriter();
            formatter.EvaluateSingleProperty(eventJson, pt, space);
            renderings.Add(space.ToString());
        }

        return renderings ?? EvaluationResult.Undefined;
    }
}
