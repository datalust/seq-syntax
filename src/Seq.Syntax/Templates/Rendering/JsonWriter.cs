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

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Encoding;
using Seq.Syntax.Templates.Themes;

namespace Seq.Syntax.Templates.Rendering;

/// <summary>
/// Writes <see cref="JsonNode"/> values as compact JSON text through a
/// <see cref="TemplateOutputEncoder"/>: every write is a styled content run. The string escaping
/// and number formatting rules are ported from Serilog's <c>JsonValueFormatter</c>: non-ASCII
/// characters are written verbatim, and element-backed numbers keep their original document text.
/// JSON <c>\u</c>-escaping is applied before the encoder's output escaper.
/// </summary>
class JsonWriter
{
    readonly TemplateOutputEncoder _encoder;
    readonly Run _tertiaryText, _name, _string, _number, _boolean, _null, _scalar;

    public JsonWriter(TemplateOutputEncoder encoder)
    {
        _encoder = encoder;
        _tertiaryText = encoder.GetRun(TemplateThemeStyle.TertiaryText);
        _name = encoder.GetRun(TemplateThemeStyle.Name);
        _string = encoder.GetRun(TemplateThemeStyle.String);
        _number = encoder.GetRun(TemplateThemeStyle.Number);
        _boolean = encoder.GetRun(TemplateThemeStyle.Boolean);
        _null = encoder.GetRun(TemplateThemeStyle.Null);
        _scalar = encoder.GetRun(TemplateThemeStyle.Scalar);
    }

    public void Format(JsonNode? value, TextWriter output)
    {
        var invisibleCharacterCount = 0;
        Format(value, output, ref invisibleCharacterCount);
    }

    public void Format(JsonNode? value, TextWriter output, ref int invisibleCharacterCount)
    {
        try
        {
            FormatNode(value, output, ref invisibleCharacterCount);
        }
        catch (InsufficientExecutionStackException)
        {
            // Pathologically nested data is truncated rather than overflowing the stack.
            Diagnostics.RecordSuppressedError(Diagnostics.ErrorKinds.RecursionDepth);
        }
    }

    void FormatNode(JsonNode? value, TextWriter output, ref int invisibleCharacterCount)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();

        switch (value)
        {
            case null:
            {
                using var _ = _null.Open(output, ref invisibleCharacterCount);
                _encoder.WriteContent(output, "null");
                break;
            }
            case JsonArray array:
            {
                WritePunctuation("[", output, ref invisibleCharacterCount);
                var delim = "";
                foreach (var element in array)
                {
                    if (delim.Length != 0)
                        WritePunctuation(delim, output, ref invisibleCharacterCount);
                    delim = ",";
                    FormatNode(element, output, ref invisibleCharacterCount);
                }
                WritePunctuation("]", output, ref invisibleCharacterCount);
                break;
            }
            case JsonObject obj:
            {
                WritePunctuation("{", output, ref invisibleCharacterCount);
                var delim = "";
                foreach (var (name, member) in obj)
                {
                    if (delim.Length != 0)
                        WritePunctuation(delim, output, ref invisibleCharacterCount);
                    delim = ",";
                    using (_name.Open(output, ref invisibleCharacterCount))
                        WriteQuotedJsonStringContent(name, output);
                    WritePunctuation(":", output, ref invisibleCharacterCount);
                    FormatNode(member, output, ref invisibleCharacterCount);
                }
                WritePunctuation("}", output, ref invisibleCharacterCount);
                break;
            }
            case JsonValue scalar:
                FormatScalar(scalar, output, ref invisibleCharacterCount);
                break;
        }
    }

    void FormatScalar(JsonValue value, TextWriter output, ref int invisibleCharacterCount)
    {
        switch (Values.Underlying(value))
        {
            case JsonElement { ValueKind: JsonValueKind.String } element:
            {
                // A malformed (invalid UTF-16) string renders as empty rather than throwing.
                using var _ = _string.Open(output, ref invisibleCharacterCount);
                WriteQuotedJsonStringContent(Values.TryGetElementString(element, out var decoded) ? decoded : "", output);
                break;
            }
            case JsonElement element:
            {
                // Numbers keep the document's original text; true/false/null round-trip exactly.
                var run = element.ValueKind switch
                {
                    JsonValueKind.Number => _number,
                    JsonValueKind.True or JsonValueKind.False => _boolean,
                    JsonValueKind.Null => _null,
                    _ => _scalar,
                };
                using var _ = run.Open(output, ref invisibleCharacterCount);
                _encoder.WriteContent(output, element.GetRawText());
                break;
            }
            case string s:
            {
                using var _ = _string.Open(output, ref invisibleCharacterCount);
                WriteQuotedJsonStringContent(s, output);
                break;
            }
            case bool b:
            {
                using var _ = _boolean.Open(output, ref invisibleCharacterCount);
                _encoder.WriteContent(output, b ? "true" : "false");
                break;
            }
            case int or uint or long or ulong or decimal or byte or sbyte or short or ushort:
            {
                using var _ = _number.Open(output, ref invisibleCharacterCount);
                _encoder.WriteContent(output, ((IFormattable)Values.Underlying(value)).ToString(null, CultureInfo.InvariantCulture));
                break;
            }
            case double d:
            {
                using var _ = _number.Open(output, ref invisibleCharacterCount);
                if (double.IsNaN(d) || double.IsInfinity(d))
                    WriteQuotedJsonStringContent(d.ToString(CultureInfo.InvariantCulture), output);
                else
                    _encoder.WriteContent(output, d.ToString("R", CultureInfo.InvariantCulture));
                break;
            }
            case float f:
            {
                using var _ = _number.Open(output, ref invisibleCharacterCount);
                if (float.IsNaN(f) || float.IsInfinity(f))
                    WriteQuotedJsonStringContent(f.ToString(CultureInfo.InvariantCulture), output);
                else
                    _encoder.WriteContent(output, f.ToString("R", CultureInfo.InvariantCulture));
                break;
            }
            case DateTimeOffset dto:
            {
                using var _ = _scalar.Open(output, ref invisibleCharacterCount);
                WriteQuotedJsonStringContent(dto.ToString("O", CultureInfo.InvariantCulture), output);
                break;
            }
            case DateTime dt:
            {
                using var _ = _scalar.Open(output, ref invisibleCharacterCount);
                WriteQuotedJsonStringContent(dt.ToString("O", CultureInfo.InvariantCulture), output);
                break;
            }
            case TimeSpan ts:
            {
                using var _ = _scalar.Open(output, ref invisibleCharacterCount);
                WriteQuotedJsonStringContent(ts.ToString(null, CultureInfo.InvariantCulture), output);
                break;
            }
            case LevelValue level:
            {
                using var _ = _scalar.Open(output, ref invisibleCharacterCount);
                WriteQuotedJsonStringContent(level.Name, output);
                break;
            }
            case var other:
            {
                using var _ = _scalar.Open(output, ref invisibleCharacterCount);
                WriteQuotedJsonStringContent(other.ToString() ?? "", output);
                break;
            }
        }
    }

    void WritePunctuation(string text, TextWriter output, ref int invisibleCharacterCount)
    {
        using var _ = _tertiaryText.Open(output, ref invisibleCharacterCount);
        _encoder.WriteContent(output, text);
    }

    void WriteQuotedJsonStringContent(string str, TextWriter output)
    {
        if (!_encoder.HasEscaper)
        {
            WriteQuotedJsonString(str, output);
            return;
        }

        var buffer = new StringWriter();
        WriteQuotedJsonString(str, buffer);
        _encoder.WriteContent(output, buffer.ToString());
    }

    static void WriteQuotedJsonString(string str, TextWriter output)
    {
        output.Write('"');

        var cleanSegmentStart = 0;
        var anyEscaped = false;

        for (var i = 0; i < str.Length; ++i)
        {
            var c = str[i];
            if (c is < (char)32 or '\\' or '"')
            {
                anyEscaped = true;

                output.Write(str.AsSpan(cleanSegmentStart, i - cleanSegmentStart));
                cleanSegmentStart = i + 1;

                switch (c)
                {
                    case '"': output.Write("\\\""); break;
                    case '\\': output.Write("\\\\"); break;
                    case '\n': output.Write("\\n"); break;
                    case '\r': output.Write("\\r"); break;
                    case '\f': output.Write("\\f"); break;
                    case '\t': output.Write("\\t"); break;
                    default:
                        output.Write("\\u");
                        output.Write(((int)c).ToString("X4"));
                        break;
                }
            }
        }

        if (anyEscaped)
        {
            if (cleanSegmentStart != str.Length)
                output.Write(str.AsSpan(cleanSegmentStart));
        }
        else
        {
            output.Write(str);
        }

        output.Write('"');
    }
}
