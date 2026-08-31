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

namespace Seq.Syntax.Templates.Rendering;

/// <summary>
/// Forwards writes to an inner writer up to a fixed character budget, then silently drops the rest.
/// Bounds output whose size is driven by untrusted input (e.g. a repeated hole in an <c>@mt</c>).
/// </summary>
sealed class LengthLimitedTextWriter : TextWriter
{
    readonly TextWriter _inner;
    int _remaining;

    public LengthLimitedTextWriter(TextWriter inner, int limit)
    {
        _inner = inner;
        _remaining = limit;
    }

    /// <summary>Whether any content has been dropped for exceeding the limit.</summary>
    public bool Truncated { get; private set; }

    public override System.Text.Encoding Encoding => _inner.Encoding;

    public override void Write(char value)
    {
        if (_remaining <= 0)
        {
            Truncated = true;
            return;
        }

        _remaining -= 1;
        _inner.Write(value);
    }

    public override void Write(string? value)
    {
        if (value != null)
            Write(value.AsSpan());
    }

    public override void Write(char[] buffer, int index, int count)
    {
        Write(buffer.AsSpan(index, count));
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        if (buffer.Length == 0)
            return;

        if (buffer.Length <= _remaining)
        {
            _remaining -= buffer.Length;
            _inner.Write(buffer);
        }
        else
        {
            _inner.Write(buffer[.._remaining]);
            _remaining = 0;
            Truncated = true;
        }
    }
}
