// Copyright © Datalust and contributors.
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

using System.Text;

namespace Seq.Syntax.Templates.Encoding;

class TerminalOutputEscaper : TemplateOutputEscaper
{
    public override string Escape(string content)
    {
        var firstStripped = -1;
        for (var i = 0; i < content.Length; ++i)
        {
            if (IsStripped(content[i]))
            {
                firstStripped = i;
                break;
            }
        }

        if (firstStripped == -1)
            return content;

        var result = new StringBuilder(content.Length);
        result.Append(content, 0, firstStripped);
        for (var i = firstStripped + 1; i < content.Length; ++i)
        {
            if (!IsStripped(content[i]))
                result.Append(content[i]);
        }

        return result.ToString();
    }

    static bool IsStripped(char c)
    {
        return c < ' ' && c is not ('\t' or '\r' or '\n') ||
               c is >= '\x7f' and <= '\x9f';
    }
}
