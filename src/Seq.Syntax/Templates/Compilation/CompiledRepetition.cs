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
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;

namespace Seq.Syntax.Templates.Compilation;

class CompiledRepetition : CompiledTemplate
{
    readonly Evaluatable _enumerable;
    readonly string? _keyOrElementName;
    readonly string? _valueOrIndexName;
    readonly CompiledTemplate _body;
    readonly CompiledTemplate? _delimiter;
    readonly CompiledTemplate? _alternative;

    public CompiledRepetition(
        Evaluatable enumerable,
        string? keyOrElementName,
        string? valueOrIndexName,
        CompiledTemplate body,
        CompiledTemplate? delimiter,
        CompiledTemplate? alternative)
    {
        _enumerable = enumerable;
        _keyOrElementName = keyOrElementName;
        _valueOrIndexName = valueOrIndexName;
        _body = body;
        _delimiter = delimiter;
        _alternative = alternative;
    }

    public override void Evaluate(EvaluationContext ctx, TextWriter output)
    {
        if (!_enumerable(ctx).TryGetValue(out var enumerable))
        {
            _alternative?.Evaluate(ctx, output);
            return;
        }

        if (enumerable is JsonArray array)
        {
            if (array.Count == 0)
            {
                _alternative?.Evaluate(ctx, output);
                return;
            }

            for (var i = 0; i < array.Count; ++i)
            {
                if (i != 0)
                {
                    _delimiter?.Evaluate(ctx, output);
                }

                var local = _keyOrElementName != null
                    ? new EvaluationContext(ctx.Document, Locals.Set(ctx.Locals, _keyOrElementName, array[i]))
                    : ctx;

                local = _valueOrIndexName != null
                    ? new EvaluationContext(local.Document, Locals.Set(local.Locals, _valueOrIndexName, JsonValue.Create(i)))
                    : local;

                _body.Evaluate(local, output);
            }

            return;
        }

        if (enumerable is JsonObject structure)
        {
            if (structure.Count == 0)
            {
                _alternative?.Evaluate(ctx, output);
                return;
            }

            var first = true;
            foreach (var (name, value) in structure)
            {
                if (first)
                    first = false;
                else
                    _delimiter?.Evaluate(ctx, output);

                var local = _keyOrElementName != null
                    ? new EvaluationContext(ctx.Document, Locals.Set(ctx.Locals, _keyOrElementName, JsonValue.Create(name)))
                    : ctx;

                local = _valueOrIndexName != null
                    ? new EvaluationContext(local.Document, Locals.Set(local.Locals, _valueOrIndexName, value))
                    : local;

                _body.Evaluate(local, output);
            }

            return;
        }

        // Scalars and JSON null are not enumerable.
        _alternative?.Evaluate(ctx, output);
    }
}
