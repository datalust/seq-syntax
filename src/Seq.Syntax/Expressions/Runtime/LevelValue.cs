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

namespace Seq.Syntax.Expressions.Runtime;

/// <summary>
/// The typed value of the <c>@Level</c> keyword property: the level name exactly as spelled in
/// the document's <c>@l</c> (<c>Information</c> when absent). Distinguishes levels from plain
/// strings so that fixed-width moniker formats apply.
/// </summary>
sealed class LevelValue(string name)
{
    public string Name { get; } = name;

    public override string ToString() => Name;
}
