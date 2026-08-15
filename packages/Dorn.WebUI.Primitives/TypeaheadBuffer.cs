namespace Dorn.WebUI.Primitives;

// Typeahead buffer (design C5); time is an explicit param, not wall-clock, so the 1s reset window is deterministically testable.
public sealed class TypeaheadBuffer
{
    private static readonly TimeSpan ResetWindow = TimeSpan.FromSeconds(1);

    private string _buffer = "";
    private DateTimeOffset _lastKeystroke = DateTimeOffset.MinValue;

    public string Buffer => _buffer;

    // Repeating the same character cycles instead of accumulating — the standard combobox affordance.
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
