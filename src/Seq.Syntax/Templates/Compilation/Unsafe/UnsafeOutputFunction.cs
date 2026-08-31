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
using Seq.Syntax.Expressions;
using Seq.Syntax.Templates.Encoding;
using System.Text.Json.Nodes;

namespace Seq.Syntax.Templates.Compilation.Unsafe;

/// <summary>
/// Implements the <c>unsafe()</c> function in expression templates: the wrapped value is asserted
/// to already be valid output-language text, so it bypasses the template's output escaper (while
/// remaining subject to the theme). When the template has no escaper there is nothing to bypass,
/// and <c>unsafe()</c> is registered as an identity pass-through.
/// </summary>
class UnsafeOutputFunction : NameResolver
{
    const string FunctionName = "unsafe";

    readonly string _implementationName;

    public UnsafeOutputFunction(bool escaperPresent)
    {
        _implementationName = escaperPresent ? nameof(PreEncode) : nameof(Identity);
    }

    public override bool TryResolveFunctionName(string name, [MaybeNullWhen(false)] out MethodInfo implementation)
    {
        if (name.Equals(FunctionName, StringComparison.OrdinalIgnoreCase))
        {
            implementation = typeof(UnsafeOutputFunction).GetMethod(_implementationName,
                BindingFlags.Static | BindingFlags.Public)!;
            return true;
        }

        implementation = null;
        return false;
    }

    public static EvaluationResult PreEncode(EvaluationResult inner)
    {
        return JsonValue.Create(new PreEncodedValue(inner))!;
    }

    public static EvaluationResult Identity(EvaluationResult inner)
    {
        return inner;
    }
}
