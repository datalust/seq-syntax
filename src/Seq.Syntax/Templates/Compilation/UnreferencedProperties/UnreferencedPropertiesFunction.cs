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
using System.Reflection;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Ast;
using Seq.Syntax.Templates.Messages;

namespace Seq.Syntax.Templates.Compilation.UnreferencedProperties;

/// <summary>
/// This little extension implements the <c>rest()</c> function in expression templates. It's based on
/// <c>Serilog.Sinks.SystemConsole.PropertiesTokenRenderer</c>, and is equivalent to how <c>Properties</c> is rendered by
/// the console sink. <c>rest()</c> will return a structure containing all of the user-defined properties from a
/// log event except those referenced in either the event's message template, or the expression template itself.
/// </summary>
/// <remarks>
/// The existing semantics of <c>Properties</c> in output templates isn't suitable for expression templates. The
/// <c>@Properties</c> object provides access to <em>all</em> event properties in an expression template, so it would
/// make no sense to render that object without all of its members.
/// </remarks>
class UnreferencedPropertiesFunction : NameResolver
{
    const string FunctionName = "rest";

    readonly HashSet<string> _referencedInTemplate;

    public UnreferencedPropertiesFunction(Template template)
    {
        var finder = new TemplateReferencedPropertiesFinder();
        _referencedInTemplate = new HashSet<string>(finder.FindReferencedProperties(template));
    }

    public override bool TryBindFunctionParameter(ParameterInfo parameter, [MaybeNullWhen(false)] out object boundValue)
    {
        if (parameter.ParameterType == typeof(UnreferencedPropertiesFunction))
        {
            boundValue = this;
            return true;
        }

        boundValue = null;
        return false;
    }

    public override bool TryResolveFunctionName(string name, [MaybeNullWhen(false)] out MethodInfo implementation)
    {
        if (name.Equals(FunctionName, StringComparison.OrdinalIgnoreCase))
        {
            implementation = typeof(UnreferencedPropertiesFunction).GetMethod(nameof(Implementation),
                BindingFlags.Static | BindingFlags.Public)!;
            return true;
        }

        implementation = null;
        return false;
    }

    public static EvaluationResult Implementation(UnreferencedPropertiesFunction self, JsonObject eventJson, EvaluationResult deep = default)
    {
        var checkMessageTemplate = Coerce.IsTrue(deep);

        HashSet<string>? referencedInMessage = null;
        if (checkMessageTemplate &&
            eventJson.TryGetPropertyValue("@mt", out var mt) &&
            Values.TryGetString(mt, out var messageTemplate))
        {
            referencedInMessage = new HashSet<string>();
            foreach (var token in MessageTemplateParser.Parse(messageTemplate))
            {
                if (token is PropertyToken pt)
                    referencedInMessage.Add(pt.PropertyName);
            }
        }

        var result = new JsonObject();
        foreach (var (name, value) in eventJson)
        {
            string propertyName;
            if (name.StartsWith("@@", StringComparison.Ordinal))
                propertyName = name[1..];
            else if (name.StartsWith('@'))
                continue;
            else
                propertyName = name;

            if (self._referencedInTemplate.Contains(propertyName) ||
                referencedInMessage != null && referencedInMessage.Contains(propertyName))
                continue;

            result[propertyName] = Values.Clone(value);
        }

        return result;
    }
}
