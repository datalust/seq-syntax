// Copyright 2013-2026 Serilog Contributors
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
using System.Text;
using Seq.Syntax.Templates.Rendering;

namespace Seq.Syntax.Templates.Messages;

/// <summary>
/// A minimal message-template parser vendored from Serilog. Destructuring hints (<c>@</c>/<c>$</c>)
/// are accepted and discarded; malformed holes are treated as literal text, matching Serilog's
/// best-effort behavior.
/// </summary>
static class MessageTemplateParser
{
    static readonly TextToken EmptyTextToken = new("");
    static readonly char[] CurlyBraceChars = ['{', '}'];

    public static IReadOnlyList<MessageTemplateToken> Parse(string messageTemplate)
    {
        var tokens = new List<MessageTemplateToken>();

        if (messageTemplate.Length == 0)
        {
            tokens.Add(EmptyTextToken);
            return tokens;
        }

        var nextIndex = 0;
        while (true)
        {
            var beforeText = nextIndex;
            var tt = ParseTextToken(nextIndex, messageTemplate, out nextIndex);
            if (nextIndex > beforeText)
                tokens.Add(tt);

            if (nextIndex == messageTemplate.Length)
                return tokens;

            var beforeProp = nextIndex;
            var pt = ParsePropertyToken(nextIndex, messageTemplate, out nextIndex);
            if (beforeProp < nextIndex)
                tokens.Add(pt);

            if (nextIndex == messageTemplate.Length)
                return tokens;
        }
    }

    static MessageTemplateToken ParsePropertyToken(int startAt, string messageTemplate, out int next)
    {
        var first = startAt;
        startAt++;

        startAt = messageTemplate.IndexOf('}', startAt);
        if (startAt == -1)
        {
            next = messageTemplate.Length;
            return new TextToken(messageTemplate[first..]);
        }

        next = startAt + 1;

        var rawText = messageTemplate.Substring(first, next - first);
        var tagContent = rawText.Substring(1, next - (first + 2));
        if (tagContent.Length == 0)
            return new TextToken(rawText);

        if (!TrySplitTagContent(tagContent, out var propertyNameAndDestructuring, out var format, out var alignment))
            return new TextToken(rawText);

        var propertyName = propertyNameAndDestructuring;
        if (propertyName.Length != 0 && propertyName[0] is '@' or '$')
            propertyName = propertyName[1..];

        if (propertyName.Length == 0)
            return new TextToken(rawText);

        if (char.IsDigit(propertyName[0]))
        {
            foreach (var c in propertyName)
            {
                if (!char.IsDigit(c))
                    return new TextToken(rawText);
            }
        }
        else
        {
            var beginIdent = true;
            foreach (var c in propertyName)
            {
                if (!TryContinuePropertyName(c, ref beginIdent))
                    return new TextToken(rawText);
            }

            if (beginIdent)
                return new TextToken(rawText);
        }

        if (format != null && format.Contains('}'))
            return new TextToken(rawText);

        Alignment? alignmentValue = null;
        if (alignment != null)
        {
            if (alignment[0] == '+')
                return new TextToken(rawText);

            if (!int.TryParse(alignment, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var width))
                return new TextToken(rawText);

            var direction = alignment[0] == '-' ? AlignmentDirection.Left : AlignmentDirection.Right;
            // `@mt` is untrusted: clamp the width (via `long` so `int.MinValue` doesn't overflow `Math.Abs`).
            var absolute = Math.Abs((long)width);
            if (absolute > Alignment.MaxWidth)
                Diagnostics.ClampedAlignmentWidths.Add(1);
            alignmentValue = new Alignment(direction, (int)Math.Min(absolute, Alignment.MaxWidth));
        }

        return new PropertyToken(propertyName, rawText, format, alignmentValue);
    }

    static bool TrySplitTagContent(string tagContent, out string propertyNameAndDestructuring, out string? format, out string? alignment)
    {
        var formatDelim = tagContent.IndexOf(':');
        var alignmentDelim = tagContent.IndexOf(',');
        if (formatDelim == -1 && alignmentDelim == -1)
        {
            propertyNameAndDestructuring = tagContent;
            format = null;
            alignment = null;
            return true;
        }

        if (alignmentDelim == -1 || (formatDelim != -1 && alignmentDelim > formatDelim))
        {
            propertyNameAndDestructuring = tagContent[..formatDelim];
            format = formatDelim == tagContent.Length - 1 ? null : tagContent[(formatDelim + 1)..];
            alignment = null;
            return true;
        }

        propertyNameAndDestructuring = tagContent[..alignmentDelim];
        if (formatDelim == -1)
        {
            if (alignmentDelim == tagContent.Length - 1)
            {
                alignment = format = null;
                return false;
            }

            format = null;
            alignment = tagContent[(alignmentDelim + 1)..];
            return true;
        }

        if (alignmentDelim == formatDelim - 1)
        {
            alignment = format = null;
            return false;
        }

        alignment = tagContent.Substring(alignmentDelim + 1, formatDelim - alignmentDelim - 1);
        format = formatDelim == tagContent.Length - 1 ? null : tagContent[(formatDelim + 1)..];
        return true;
    }

    static bool TryContinuePropertyName(char c, ref bool beginIdent)
    {
        if (beginIdent)
        {
            if (char.IsLetter(c) || c is '_')
            {
                beginIdent = false;
                return true;
            }

            return false;
        }

        if (char.IsLetterOrDigit(c) || c is '_')
            return true;

        if (c is '.')
        {
            beginIdent = true;
            return true;
        }

        return false;
    }

    static TextToken ParseTextToken(int startAt, string messageTemplate, out int next)
    {
        // Escape sequences ({{ and }}) require accumulation; without them the token is a single
        // substring of the template.

        var i = messageTemplate.IndexOfAny(CurlyBraceChars, startAt);
        if (i == -1)
        {
            next = messageTemplate.Length;
            return new TextToken(messageTemplate[startAt..]);
        }

        StringBuilder accum;
        var ch = messageTemplate[i];
        ++i;

        if (ch == '{')
        {
            if (i < messageTemplate.Length && messageTemplate[i] == '{')
            {
                accum = new StringBuilder(messageTemplate, startAt, i - startAt, messageTemplate.Length - startAt);
                ++i;
            }
            else
            {
                next = i - 1;
                return next == startAt ? EmptyTextToken : new TextToken(messageTemplate.Substring(startAt, i - 1 - startAt));
            }
        }
        else // ch == '}'
        {
            accum = new StringBuilder(messageTemplate, startAt, i - startAt, messageTemplate.Length - startAt);
            if (i < messageTemplate.Length && messageTemplate[i] == '}')
                ++i;
        }

        while (i < messageTemplate.Length)
        {
            ch = messageTemplate[i];
            ++i;

            if (ch == '{')
            {
                if (i < messageTemplate.Length && messageTemplate[i] == '{')
                {
                    accum.Append(ch);
                    ++i;
                }
                else
                {
                    next = i - 1;
                    return new TextToken(accum.ToString());
                }
            }
            else
            {
                accum.Append(ch);
                if (ch == '}' && i < messageTemplate.Length && messageTemplate[i] == '}')
                    ++i;
            }
        }

        next = i;
        return new TextToken(accum.ToString());
    }
}
