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

namespace Seq.Syntax.Expressions;

/// <summary>
/// A compiled expression evaluated against an event JSON document.
/// </summary>
/// <param name="eventJson">An event JSON document in Seq's emission schema.</param>
/// <returns>The result of evaluating the expression: undefined, JSON null, or a
/// <see cref="JsonNode"/> value.</returns>
public delegate EvaluationResult CompiledExpression(JsonObject eventJson);
