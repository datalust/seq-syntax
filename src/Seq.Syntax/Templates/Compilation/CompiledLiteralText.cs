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

using Seq.Syntax.Expressions;
using Seq.Syntax.Templates.Encoding;

namespace Seq.Syntax.Templates.Compilation;

class CompiledLiteralText : CompiledTemplate
{
    readonly string _text;
    readonly Run _tertiaryText;

    public CompiledLiteralText(string text, Run tertiaryText)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _tertiaryText = tertiaryText;
    }

    public override void Evaluate(EvaluationContext ctx, TextWriter output)
    {
        var invisibleCharacterCount = 0;
        using var _ = _tertiaryText.Open(output, ref invisibleCharacterCount);

        // Literal text is the template author's markup — it is never escaped.
        output.Write(_text);
    }
}
