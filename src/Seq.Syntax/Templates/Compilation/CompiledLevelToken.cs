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
using Seq.Syntax.Templates.Rendering;
using Seq.Syntax.Templates.Themes;

namespace Seq.Syntax.Templates.Compilation;

class CompiledLevelToken : CompiledTemplate
{
    // Seq's canonical level names, mapped onto the six Serilog-derived level styles so that
    // themes remain copy-pastable between the Serilog ecosystem and Seq.Syntax. `LevelMapping`
    // canonicalizes `@l` spellings (`warn`, `WARNING`, …) for the lookup only — the rendered
    // level keeps the document's spelling — and an unrecognized name styles as
    // `LevelInformation`.
    static readonly Dictionary<string, TemplateThemeStyle> CanonicalLevelStyles =
        new(StringComparer.Ordinal)
        {
            ["Trace"] = TemplateThemeStyle.LevelVerbose,
            ["Verbose"] = TemplateThemeStyle.LevelVerbose,
            ["Debug"] = TemplateThemeStyle.LevelDebug,
            ["Information"] = TemplateThemeStyle.LevelInformation,
            ["Notice"] = TemplateThemeStyle.LevelInformation,
            ["Warning"] = TemplateThemeStyle.LevelWarning,
            ["Error"] = TemplateThemeStyle.LevelError,
            ["Fatal"] = TemplateThemeStyle.LevelFatal,
            ["Critical"] = TemplateThemeStyle.LevelFatal,
            ["Emergency"] = TemplateThemeStyle.LevelFatal,
            ["Alert"] = TemplateThemeStyle.LevelFatal,
            ["Panic"] = TemplateThemeStyle.LevelFatal,
        };

    readonly string? _format;
    readonly Alignment? _alignment;
    readonly TemplateOutputEncoder _encoder;
    readonly Dictionary<string, Run> _levelRuns;
    readonly Run _unrecognizedLevelRun;

    public CompiledLevelToken(string? format, Alignment? alignment, TemplateOutputEncoder encoder)
    {
        _format = format;
        _alignment = alignment;
        _encoder = encoder;
        _levelRuns = CanonicalLevelStyles.ToDictionary(
            kv => kv.Key,
            kv => encoder.GetRun(kv.Value),
            StringComparer.Ordinal);
        _unrecognizedLevelRun = encoder.GetRun(TemplateThemeStyle.LevelInformation);
    }

    public override void Evaluate(EvaluationContext ctx, TextWriter output)
    {
        var invisibleCharacterCount = 0;

        if (_alignment == null)
        {
            EvaluateUnaligned(ctx, output, ref invisibleCharacterCount);
        }
        else
        {
            var writer = new StringWriter();
            EvaluateUnaligned(ctx, writer, ref invisibleCharacterCount);
            Padding.Apply(output, writer.ToString(), _alignment.Value.Widen(invisibleCharacterCount));
        }
    }

    void EvaluateUnaligned(EvaluationContext ctx, TextWriter output, ref int invisibleCharacterCount)
    {
        var level = KeywordProperties.GetLevelValue(ctx.Document);
        var styleName = LevelMapping.TryGetCanonicalName(level.Name, out var canonical) ? canonical : level.Name;
        if (!_levelRuns.TryGetValue(styleName, out var run))
            run = _unrecognizedLevelRun;

        using var _ = run.Open(output, ref invisibleCharacterCount);

        // The moniker is content: the `@l` value passes through with its original spelling,
        // except that fixed-width formats abbreviate via the canonical name.
        _encoder.WriteContent(output, LevelRenderer.GetLevelMoniker(level, _format));
    }
}
