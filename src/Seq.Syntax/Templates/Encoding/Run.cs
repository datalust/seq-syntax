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

namespace Seq.Syntax.Templates.Encoding;

/// <summary>
/// The theme's delimiting text for output runs of one style: a compile-time handle held by
/// compiled template tokens. Theme text is invisible for alignment purposes; <see cref="Open"/>
/// pre-counts both delimiters so that a run cut off before its close still balances the padding
/// calculation.
/// </summary>
readonly struct Run
{
    readonly string? _open;
    readonly string? _close;

    public Run(string? open, string? close)
    {
        _open = open;
        _close = close;
    }

    public RunClose Open(TextWriter output, ref int invisibleCharacterCount)
    {
        if (_open != null)
        {
            output.Write(_open);
            invisibleCharacterCount += _open.Length;
        }

        if (_close == null)
            return default;

        invisibleCharacterCount += _close.Length;
        return new RunClose(output, _close);
    }
}

readonly struct RunClose : IDisposable
{
    readonly TextWriter? _output;
    readonly string? _close;

    public RunClose(TextWriter output, string close)
    {
        _output = output;
        _close = close;
    }

    public void Dispose()
    {
        _output?.Write(_close);
    }
}
