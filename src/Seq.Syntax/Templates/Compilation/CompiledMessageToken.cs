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

using System.Text.Json;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Templates.Messages;
using Seq.Syntax.Templates.Rendering;
using Seq.Syntax.Templates.Themes;

namespace Seq.Syntax.Templates.Compilation;

/// <summary>
/// Renders the event's message: the pre-rendered <c>@m</c> when present, otherwise the
/// <c>@mt</c> message template over the document's properties. Dotted names in template holes
/// are paths into nested objects, matching how Seq stores properties on the server side.
/// </summary>
class CompiledMessageToken : CompiledTemplate
{
    // The message template comes from untrusted `@mt`; a repeated hole over a large property can
    // amplify output without bound, so total message expansion is capped (outer templates are trusted).
    const int MessageLengthLimit = 16 * 1024;

    readonly IFormatProvider? _formatProvider;
    readonly Alignment? _alignment;
    readonly TemplateOutputEncoder _encoder;
    readonly Run _text, _invalid, _null, _boolean, _string, _number, _scalar;
    readonly JsonWriter _jsonWriter;

    public CompiledMessageToken(IFormatProvider? formatProvider, Alignment? alignment, TemplateOutputEncoder encoder)
    {
        _formatProvider = formatProvider;
        _alignment = alignment;
        _encoder = encoder;
        _text = encoder.GetRun(TemplateThemeStyle.Text);
        _invalid = encoder.GetRun(TemplateThemeStyle.Invalid);
        _null = encoder.GetRun(TemplateThemeStyle.Null);
        _boolean = encoder.GetRun(TemplateThemeStyle.Boolean);
        _string = encoder.GetRun(TemplateThemeStyle.String);
        _number = encoder.GetRun(TemplateThemeStyle.Number);
        _scalar = encoder.GetRun(TemplateThemeStyle.Scalar);
        _jsonWriter = new JsonWriter(encoder);
    }

    public override void Evaluate(EvaluationContext ctx, TextWriter output)
    {
        var invisibleCharacterCount = 0;

        if (_alignment == null)
        {
            var limited = new LengthLimitedTextWriter(output, MessageLengthLimit);
            EvaluateUnaligned(ctx.Document, limited, ref invisibleCharacterCount);
            RecoverFromTruncation(limited, output, ref invisibleCharacterCount);
        }
        else
        {
            var writer = new StringWriter();
            var limited = new LengthLimitedTextWriter(writer, MessageLengthLimit);
            EvaluateUnaligned(ctx.Document, limited, ref invisibleCharacterCount);
            RecoverFromTruncation(limited, writer, ref invisibleCharacterCount);
            Padding.Apply(output, writer.ToString(), _alignment.Value.Widen(invisibleCharacterCount));
        }
    }

    // Truncation can cut mid-run, dropping the run's balancing close along with the overflowing
    // output and leaving e.g. a terminal styled; the theme's recovery text is written past the
    // length limit to restore a neutral state.
    void RecoverFromTruncation(LengthLimitedTextWriter limited, TextWriter unlimited, ref int invisibleCharacterCount)
    {
        if (!limited.Truncated)
            return;

        Diagnostics.TruncatedMessages.Add(1);

        if (_encoder.Theme?.TruncationRecovery is { } recovery)
        {
            unlimited.Write(recovery);
            invisibleCharacterCount += recovery.Length;
        }
    }

    void EvaluateUnaligned(JsonObject eventJson, TextWriter output, ref int invisibleCharacterCount)
    {
        if (eventJson.TryGetPropertyValue("@m", out var m) &&
            Values.TryGetString(m, out var message))
        {
            using var _ = _text.Open(output, ref invisibleCharacterCount);
            _encoder.WriteContent(output, message);
            return;
        }

        if (!eventJson.TryGetPropertyValue("@mt", out var mt) ||
            !Values.TryGetString(mt, out var messageTemplate))
        {
            return;
        }

        foreach (var token in MessageTemplateParser.Parse(messageTemplate))
        {
            switch (token)
            {
                case TextToken tt:
                {
                    using var _ = _text.Open(output, ref invisibleCharacterCount);
                    _encoder.WriteContent(output, tt.Text);
                    break;
                }
                case PropertyToken pt:
                    EvaluateProperty(eventJson, pt, output, ref invisibleCharacterCount);
                    break;
            }
        }
    }

    /// <summary>
    /// Render one message-template hole the way a full message rendering would: the v1
    /// compatibility <c>@r</c> implementation reconstructs the renderings array this way.
    /// </summary>
    internal void EvaluateSingleProperty(JsonObject eventJson, PropertyToken pt, TextWriter output)
    {
        var invisibleCharacterCount = 0;
        EvaluateProperty(eventJson, pt, output, ref invisibleCharacterCount);
    }

    void EvaluateProperty(JsonObject properties, PropertyToken pt, TextWriter output, ref int invisibleCharacterCount)
    {
        var rest = pt.PropertyName.AsSpan();
        if (!TryGetNextStep(rest, out var name, out rest))
        {
            WriteUnresolvable(pt, output, ref invisibleCharacterCount);
            return;
        }

        if (!properties.TryGetPropertyValue(name.ToString(), out var value))
        {
            WriteUnresolvable(pt, output, ref invisibleCharacterCount);
            return;
        }

        while (TryGetNextStep(rest, out name, out rest))
        {
            if (value is not JsonObject obj ||
                !obj.TryGetPropertyValue(name.ToString(), out value))
            {
                WriteUnresolvable(pt, output, ref invisibleCharacterCount);
                return;
            }
        }

        if (pt.Alignment is null)
        {
            EvaluatePropertyUnaligned(value, output, pt.Format, ref invisibleCharacterCount);
            return;
        }

        var buffer = new StringWriter();
        var resultInvisibleCharacters = 0;

        EvaluatePropertyUnaligned(value, buffer, pt.Format, ref resultInvisibleCharacters);

        var result = buffer.ToString();
        invisibleCharacterCount += resultInvisibleCharacters;

        if (result.Length - resultInvisibleCharacters >= pt.Alignment.Value.Width)
            output.Write(result);
        else
            Padding.Apply(output, result, pt.Alignment.Value.Widen(resultInvisibleCharacters));
    }

    void WriteUnresolvable(PropertyToken pt, TextWriter output, ref int invisibleCharacterCount)
    {
        using var _ = _invalid.Open(output, ref invisibleCharacterCount);
        _encoder.WriteContent(output, pt.RawText);
    }

    void EvaluatePropertyUnaligned(JsonNode? propertyValue, TextWriter output, string? format, ref int invisibleCharacterCount)
    {
        if (propertyValue == null)
        {
            using var _ = _null.Open(output, ref invisibleCharacterCount);
            _encoder.WriteContent(output, "null");
            return;
        }

        if (propertyValue is not JsonValue scalar)
        {
            _jsonWriter.Format(propertyValue, output, ref invisibleCharacterCount);
            return;
        }

        var underlying = Values.Underlying(scalar);
        switch (underlying)
        {
            case JsonElement { ValueKind: JsonValueKind.String } element:
            {
                // A malformed (invalid UTF-16) string renders as empty rather than throwing.
                if (Values.TryGetElementString(element, out var s))
                {
                    using var _ = _string.Open(output, ref invisibleCharacterCount);
                    _encoder.WriteContent(output, s);
                }
                break;
            }
            case string str:
            {
                using var _ = _string.Open(output, ref invisibleCharacterCount);
                _encoder.WriteContent(output, str);
                break;
            }
            case char c:
            {
                using var _ = _string.Open(output, ref invisibleCharacterCount);
                _encoder.WriteContent(output, c.ToString());
                break;
            }
            case JsonElement { ValueKind: JsonValueKind.True } or true:
            {
                using var _ = _boolean.Open(output, ref invisibleCharacterCount);
                _encoder.WriteContent(output, bool.TrueString);
                break;
            }
            case JsonElement { ValueKind: JsonValueKind.False } or false:
            {
                using var _ = _boolean.Open(output, ref invisibleCharacterCount);
                _encoder.WriteContent(output, bool.FalseString);
                break;
            }
            case JsonElement { ValueKind: JsonValueKind.Number }
                or int or uint or long or ulong or decimal or byte or sbyte or short or ushort or double or float:
            {
                WriteFormattedScalar(_number, underlying, format, output, ref invisibleCharacterCount);
                break;
            }
            default:
            {
                // DateTime, DateTimeOffset, TimeSpan, Guid, and anything else. No case is needed for
                // LevelValue: it's produced only by keyword properties, never captured by a message template.
                WriteFormattedScalar(_scalar, underlying, format, output, ref invisibleCharacterCount);
                break;
            }
        }
    }

    void WriteFormattedScalar(Run run, object underlying, string? format, TextWriter output, ref int invisibleCharacterCount)
    {
        using var _ = run.Open(output, ref invisibleCharacterCount);
        _encoder.WriteContent(output, Values.FormatScalarValue(underlying, format, _formatProvider) ?? "");
    }

    static bool TryGetNextStep(ReadOnlySpan<char> path, out ReadOnlySpan<char> name, out ReadOnlySpan<char> rest)
    {
        if (path.Length == 0)
        {
            name = [];
            rest = [];
            return false;
        }

        var i = path.IndexOf('.');
        if (i == -1)
        {
            name = path;
            rest = [];
            return true;
        }

        name = path[..i];
        rest = path[(i + 1)..];

        return true;
    }
}
