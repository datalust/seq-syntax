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

using System.Diagnostics.CodeAnalysis;
using Seq.Syntax.Templates.Ast;
using Seq.Syntax.Templates.Messages;

namespace Seq.Syntax.Templates.Parsing;

/// <summary>Parser for the Seq.Syntax templating language.</summary>
/// <seealso cref="MessageTemplateParser"/>
class TemplateParser
{
    readonly TemplateTokenizer _tokenizer = new();
    readonly TemplateTokenParsers _templateTokenParsers = new();

    public bool TryParse(
        string template,
        [MaybeNullWhen(false)] out Template parsed,
        [MaybeNullWhen(true)] out string error)
    {
        ArgumentNullException.ThrowIfNull(template);

        var tokenList = _tokenizer.TryTokenize(template);
        if (!tokenList.HasValue)
        {
            error = tokenList.ToString();
            parsed = null;
            return false;
        }

        Superpower.Model.TokenListParserResult<Expressions.Parsing.ExpressionToken, Template> result;
        try
        {
            result = _templateTokenParsers.TryParse(tokenList.Value);
        }
        catch (InsufficientExecutionStackException)
        {
            error = "the template is nested too deeply.";
            parsed = null;
            return false;
        }

        if (!result.HasValue)
        {
            error = result.ToString();
            parsed = null;
            return false;
        }

        parsed = result.Value;
        error = null;
        return true;
    }
}