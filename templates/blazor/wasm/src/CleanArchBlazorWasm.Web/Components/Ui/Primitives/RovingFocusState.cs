namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives;

public enum RovingFocusOrientation
{
    Vertical,
    Horizontal,
}

/// <summary>
/// Roving-tabindex state machine (design C5) — pure C#, zero JS. Exactly one registered item is
/// active (<c>tabindex="0"</c>) at a time; every other item is <c>tabindex="-1"</c>. Used by
/// DropdownMenu and Select (both PR6).
/// </summary>
public sealed class RovingFocusState(
    RovingFocusOrientation orientation = RovingFocusOrientation.Vertical,
    bool loop = true
)
{
    private List<(string Id, bool Disabled)> _items = [];

    public RovingFocusOrientation Orientation { get; } = orientation;

    public bool Loop { get; } = loop;

    public string? ActiveId { get; private set; }

    public RovingFocusState WithItems(params string[] ids) =>
        WithItems(ids.Select(id => (id, false)).ToArray());

    public RovingFocusState WithItems(params (string Id, bool Disabled)[] items)
    {
        _items = [.. items];

        if (ActiveId is null || _items.All(i => i.Id != ActiveId))
        {
            ActiveId = _items.FirstOrDefault(i => !i.Disabled).Id;
        }

        return this;
    }

    public bool IsActive(string id) => id == ActiveId;

    public int TabIndexFor(string id) => IsActive(id) ? 0 : -1;

    /// <summary>Returns true when this key was a navigation key this state machine handles.</summary>
    public bool HandleKey(string key)
    {
        var isForward =
            Orientation == RovingFocusOrientation.Vertical
                ? key == "ArrowDown"
                : key == "ArrowRight";
        var isBackward =
            Orientation == RovingFocusOrientation.Vertical ? key == "ArrowUp" : key == "ArrowLeft";

        if (isForward)
        {
            Move(1);
            return true;
        }

        if (isBackward)
        {
            Move(-1);
            return true;
        }

        switch (key)
        {
            case "Home":
                ActivateAt(_items.FindIndex(i => !i.Disabled));
                return true;
            case "End":
                ActivateAt(_items.FindLastIndex(i => !i.Disabled));
                return true;
            default:
                return false;
        }
    }

    private void Move(int direction)
    {
        if (_items.Count == 0)
        {
            return;
        }

        var currentIndex = Math.Max(_items.FindIndex(i => i.Id == ActiveId), 0);

        for (var offset = 1; offset <= _items.Count; offset++)
        {
            var rawIndex = currentIndex + direction * offset;

            if (!Loop && (rawIndex < 0 || rawIndex >= _items.Count))
            {
                return;
            }

            var index = ((rawIndex % _items.Count) + _items.Count) % _items.Count;

            if (!_items[index].Disabled)
            {
                ActiveId = _items[index].Id;
                return;
            }
        }
    }

    private void ActivateAt(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            ActiveId = _items[index].Id;
        }
    }
}
