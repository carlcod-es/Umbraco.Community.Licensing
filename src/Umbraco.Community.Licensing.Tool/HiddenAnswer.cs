using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// A hidden answer being typed (ADR-0021, "Byte-only passphrase path"): key presses go into a
/// pinned <see cref="char"/> buffer of <see cref="Capacity"/>; presses beyond it are counted, not
/// stored, so "too long" is still detected. Every character key is kept as typed, ASCII or not, so
/// the passphrase rule can refuse it; only non-character keys are ignored. The real console and
/// the scripted console both feed it, so they cannot diverge. The buffer is zeroed on dispose.
/// </summary>
internal sealed class HiddenAnswer : IDisposable
{
    /// <summary>The passphrase limit of 256 (ADR-0019) plus one, so a too-long answer is seen.</summary>
    internal const int Capacity = 257;

    private readonly char[] _buffer = GC.AllocateArray<char>(Capacity, pinned: true);

    /// <summary>The number of characters typed and not erased, including any beyond the buffer.</summary>
    public int Length { get; private set; }

    /// <summary>The characters held: the first <see cref="Capacity"/> typed.</summary>
    public ReadOnlySpan<char> Chars => _buffer.AsSpan(0, Math.Min(Length, Capacity));

    /// <summary>
    /// True when Ctrl+D (Unix) or Ctrl+Z (Windows) was pressed with nothing typed: the end-of-input key, so the
    /// answer is not an answer and the console reports end of input. Pressed after other keys, it is ignored.
    /// </summary>
    public bool EndOfInput { get; private set; }

    /// <summary>The buffer itself, for tests that check it was zeroed.</summary>
    internal char[] Raw => _buffer;

    /// <summary>Handles one key press; true when it ends the answer: Enter, or the end-of-input key on an empty answer (<see cref="EndOfInput"/>).</summary>
    public bool Press(ConsoleKey key, char keyChar)
    {
        if (key == ConsoleKey.Enter)
            return true;

        if (keyChar is '\u0004' or '\u001A' && Length == 0)
        {
            EndOfInput = true;
            return true;
        }

        if (key == ConsoleKey.Backspace)
        {
            if (Length > 0)
            {
                Length--;
                if (Length < Capacity)
                    _buffer[Length] = '\0';
            }

            return false;
        }

        // Arrows, Tab, Esc and the like carry no character or a control character (so Ctrl+D or Ctrl+Z after other keys do nothing).
        if (keyChar == '\0' || char.IsControl(keyChar))
            return false;

        if (Length < Capacity)
            _buffer[Length] = keyChar;
        Length++;
        return false;
    }

    /// <summary>True when the answer is the whole of <paramref name="word"/>, in any case.</summary>
    public bool Is(string word) =>
        Length == word.Length && Chars.Equals(word, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Copies the answer to a new pinned byte array the caller owns and zeroes. ASCII is one byte
    /// per character; any other character becomes its UTF-8 bytes, so a later check against the
    /// stored passphrase finds it wrong. Characters beyond the buffer are not copied, which also
    /// makes such an answer wrong. The passphrase never becomes a <see cref="string"/>.
    /// </summary>
    public byte[] ToBytes()
    {
        var chars = Chars;
        var bytes = GC.AllocateArray<byte>(Encoding.UTF8.GetByteCount(chars), pinned: true);
        Encoding.UTF8.GetBytes(chars, bytes);
        return bytes;
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(MemoryMarshalAsBytes());

    private Span<byte> MemoryMarshalAsBytes() => MemoryMarshal.AsBytes(_buffer.AsSpan());
}
