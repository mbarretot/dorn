namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives;

/// <summary>
/// Typeahead accumulation buffer (design C5) — pure C#, zero JS. Used by DropdownMenu and
/// Select (both PR6) to jump to the item matching what the user just typed. Time is an explicit
/// <see cref="Append"/> parameter rather than wall-clock, keeping the 1s reset window
/// deterministically testable.
/// </summary>
public sealed class TypeaheadBuffer
{
    private static readonly TimeSpan ResetWindow = TimeSpan.FromSeconds(1);

    private string _buffer = "";
    private DateTimeOffset _lastKeystroke = DateTimeOffset.MinValue;

    public string Buffer => _buffer;

    /// <summary>
    /// Appends <paramref name="character"/> and returns the resulting buffer. Repeating the
    /// same single character (e.g. pressing "B" repeatedly) keeps the buffer at that one
    /// character instead of accumulating "BBB", the standard combobox affordance for cycling
    /// through every item starting with that letter.
    /// </summary>
    public string Append(char character, DateTimeOffset now)
    {
        if (now - _lastKeystroke > ResetWindow)
        {
            _buffer = "";
        }

        _buffer = IsSingleCharacterRepeat(character) ? _buffer : _buffer + character;
        _lastKeystroke = now;
        return _buffer;
    }

    public void Reset()
    {
        _buffer = "";
        _lastKeystroke = DateTimeOffset.MinValue;
    }

    private bool IsSingleCharacterRepeat(char character) =>
        _buffer.Length > 0
        && _buffer.All(c => char.ToLowerInvariant(c) == char.ToLowerInvariant(character));
}
