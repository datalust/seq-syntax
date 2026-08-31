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
using Seq.Syntax.Expressions.Runtime;

namespace Seq.Syntax.Expressions;

/// <summary>
/// The result of evaluating an expression: either <em>undefined</em>, or a defined
/// <see cref="JsonNode"/> value, where a <c langword="null">null</c> node is JSON <c>null</c>.
/// </summary>
/// <remarks>Results should be treated as immutable: a returned node may share structure with the
/// source document or with compiled constants.</remarks>
public readonly struct EvaluationResult
{
    readonly bool _isDefined;
    readonly JsonNode? _value;

    EvaluationResult(JsonNode? value)
    {
        _isDefined = true;
        _value = value;
    }

    /// <summary>
    /// The undefined result; also the <c langword="default">default</c> value of the struct.
    /// </summary>
    public static readonly EvaluationResult Undefined = default;

    /// <summary>
    /// The JSON <c>null</c> result.
    /// </summary>
    public static readonly EvaluationResult Null = new(null);

    /// <summary>
    /// A defined result carrying <paramref name="value"/>, where <c langword="null">null</c>
    /// is JSON <c>null</c>.
    /// </summary>
    public static EvaluationResult Defined(JsonNode? value) => new(value);

    /// <summary>
    /// Whether the result is defined. JSON <c>null</c> is a defined result.
    /// </summary>
    public bool IsDefined => _isDefined;

    /// <summary>
    /// The wrapped node. Only meaningful when <see cref="IsDefined"/>; the compiler reads it
    /// after a definedness guard when binding a <c langword="null">JsonNode?</c> parameter.
    /// </summary>
    // ReSharper disable once UnusedMember.Local, ConvertToAutoPropertyWhenPossible
    JsonNode? ReflectionOnlyDefinedValue => _value;

    /// <summary>
    /// Retrieve the result's value. Returns <c langword="false">false</c> if the result is
    /// undefined; a <c langword="true">true</c> return with a <c langword="null">null</c>
    /// <paramref name="value"/> is JSON <c>null</c>.
    /// </summary>
    public bool TryGetValue(out JsonNode? value)
    {
        value = _value;
        return _isDefined;
    }

    /// <summary>
    /// Deconstruct into definedness and value, supporting exhaustive matching:
    /// <c>result switch { (false, _) => …, (true, null) => …, (true, var node) => … }</c>.
    /// </summary>
    public void Deconstruct(out bool isDefined, out JsonNode? value)
    {
        isDefined = _isDefined;
        value = _value;
    }

    /// <summary>
    /// Wrap a non-null node. JSON <c>null</c> and undefined results must be constructed
    /// explicitly via <see cref="Null"/> and <see cref="Undefined"/>.
    /// </summary>
    public static implicit operator EvaluationResult(JsonNode value) => new(value);

    /// <summary>
    /// Whether the result is the scalar Boolean <c langword="true">true</c>.
    /// </summary>
    public bool IsTrue() => Coerce.Boolean(this, out var b) && b;
}
