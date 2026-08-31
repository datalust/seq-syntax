// Copyright 2013-2015 Serilog Contributors
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

namespace Seq.Syntax.Templates.Messages;

abstract class MessageTemplateToken
{
}

sealed class TextToken : MessageTemplateToken
{
    public TextToken(string text)
    {
        Text = text;
    }

    public string Text { get; }
}

sealed class PropertyToken : MessageTemplateToken
{
    public PropertyToken(string propertyName, string rawText, string? format, Rendering.Alignment? alignment)
    {
        PropertyName = propertyName;
        RawText = rawText;
        Format = format;
        Alignment = alignment;
    }

    public string PropertyName { get; }
    public string RawText { get; }
    public string? Format { get; }
    public Rendering.Alignment? Alignment { get; }

    public override string ToString() => RawText;
}
