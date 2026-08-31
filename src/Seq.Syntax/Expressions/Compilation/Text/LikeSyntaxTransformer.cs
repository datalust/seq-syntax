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

using System.Text.RegularExpressions;
using Seq.Syntax.Expressions.Ast;
using Seq.Syntax.Expressions.Compilation.Transformations;
using System.Text.Json.Nodes;

namespace Seq.Syntax.Expressions.Compilation.Text;

class LikeSyntaxTransformer: IdentityTransformer
{
    static readonly LikeSyntaxTransformer Instance = new();

    public static Expression Rewrite(Expression expression)
    {
        return Instance.Transform(expression);
    }

    protected override Expression Transform(CallExpression call)
    {
        if (call.Operands.Length != 2)
            return base.Transform(call);

        if (Operators.SameOperator(call.OperatorName, Operators.IntermediateOpLike))
            return TryCompileLikeExpression(call.IgnoreCase, call.Operands[0], call.Operands[1]);

        if (Operators.SameOperator(call.OperatorName, Operators.IntermediateOpNotLike))
            return new CallExpression(
                false,
                Operators.RuntimeOpStrictNot,
                TryCompileLikeExpression(call.IgnoreCase, call.Operands[0], call.Operands[1]));

        return base.Transform(call);
    }

    CallExpression TryCompileLikeExpression(bool ignoreCase, Expression corpus, Expression like)
    {
        if (!Pattern.IsStringConstant(like, out var s))
        {
            // A non-constant pattern can't be compiled; the expression evaluates to undefined.
            return new CallExpression(false, Operators.OpUndefined);
        }

        var regex = LikeToRegex(s);
        var opts = RegexOptions.Compiled | RegexOptions.ExplicitCapture;
        if (ignoreCase)
            opts |= RegexOptions.IgnoreCase;
        var compiled = new Regex(regex, opts, TimeSpan.FromMilliseconds(100));
        var indexof = new IndexOfMatchExpression(Transform(corpus), compiled);
        return new CallExpression(ignoreCase, Operators.RuntimeOpNotEqual, indexof, new ConstantExpression(JsonValue.Create(-1)));
    }

    static string LikeToRegex(string like)
    {
        var begin = "^";
        var regex = "";
        var end = "$";

        for (var i = 0; i < like.Length; ++i)
        {
            var ch = like[i];
            char? following = i == like.Length - 1 ? null : like[i + 1];
            switch (ch)
            {
                case '%' when following == '%':
                    regex += '%';
                    ++i;
                    break;
                case '%':
                {
                    if (i == 0)
                        begin = "";

                    if (i == like.Length - 1)
                        end = "";

                    if (i == 0 && i == like.Length - 1)
                        regex += ".*";

                    if (i != 0 && i != like.Length - 1)
                        regex += "(?:.|\\r|\\n)*"; // ~= RegexOptions.Singleline
                    break;
                }
                case '_' when following == '_':
                    regex += '_';
                    ++i;
                    break;
                case '_':
                    regex += '.'; // Newlines aren't considered matches for _
                    break;
                default:
                    regex += Regex.Escape(ch.ToString());
                    break;
            }
        }

        return begin + regex + end;
    }
}