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
/// remaining subject to the theme). A template with no escaper has nothing to bypass, so
/// <c>unsafe()</c> is rejected when the template is compiled.
/// </summary>
class UnsafeOutputFunction : NameResolver
{
    const string FunctionName = "unsafe";

    readonly bool _escaperPresent;

    public UnsafeOutputFunction(bool escaperPresent)
    {
        _escaperPresent = escaperPresent;
    }

    public override bool TryResolveFunctionName(string name, [MaybeNullWhen(false)] out MethodInfo implementation)
    {
        if (name.Equals(FunctionName, StringComparison.OrdinalIgnoreCase))
        {
            if (!_escaperPresent)
                throw new ArgumentException(
                    $"The `{FunctionName}()` function requires an output escaper; the template's encoder has none, so there is no escaping to bypass.");

            implementation = typeof(UnsafeOutputFunction).GetMethod(nameof(PreEncode),
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
}
