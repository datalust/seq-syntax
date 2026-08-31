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
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Templates.Themes;

namespace Seq.Syntax.Templates.Compilation;

class CompiledExceptionToken : CompiledTemplate
{
    const string StackFrameLinePrefix = "   ";

    readonly TemplateOutputEncoder _encoder;
    readonly Run _text, _secondaryText;

    public CompiledExceptionToken(TemplateOutputEncoder encoder)
    {
        _encoder = encoder;
        _text = encoder.GetRun(TemplateThemeStyle.Text);
        _secondaryText = encoder.GetRun(TemplateThemeStyle.SecondaryText);
    }

    public override void Evaluate(EvaluationContext ctx, TextWriter output)
    {
        // Padding and alignment are not applied by this renderer.

        if (!ctx.Document.TryGetPropertyValue("@x", out var x) ||
            !Values.TryGetString(x, out var exception))
        {
            return;
        }

        var invisibleCharacterCount = 0;
        var lines = new StringReader(exception);
        string? nextLine;
        while ((nextLine = lines.ReadLine()) != null)
        {
            var run = nextLine.StartsWith(StackFrameLinePrefix, StringComparison.Ordinal) ? _secondaryText : _text;
            using var _ = run.Open(output, ref invisibleCharacterCount);
            _encoder.WriteContent(output, nextLine);
            output.WriteLine();
        }
    }
}
