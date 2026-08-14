using CleanArchBlazorWasm.Web.Components.Ui.Primitives;
using Microsoft.AspNetCore.Components;

namespace CleanArchBlazorWasm.Web.Components.Ui.DropdownMenu;

/// <summary>
/// Cascades open state, the roving-tabindex state machine (design C5), and the trigger/item
/// element registry so <see cref="DropdownMenuContent"/>/<see cref="DropdownMenuItem"/> can
/// drive real focus movement without a child-to-parent round trip — the same shape as
/// <c>DialogContext</c>/<c>TabsContext</c>.
/// </summary>
public sealed class DropdownMenuContext
{
    private readonly List<(string Id, ElementReference Element, bool Disabled)> _items = [];

    public RovingFocusState Focus { get; } = new(RovingFocusOrientation.Vertical, loop: true);

    public bool IsOpen { get; internal set; }

    public ElementReference TriggerElement { get; internal set; }

    public required string TriggerId { get; init; }

    public required string ContentId { get; init; }

    public required Func<Task> RequestOpen { get; init; }

    public required Func<Task> RequestClose { get; init; }

    /// <summary>
    /// Re-renders the whole menu subtree. Roving-tabindex movement mutates <see cref="Focus"/>
    /// from whichever item's own keydown handler ran — Blazor only auto-rerenders that one
    /// component, not its sibling items whose <c>tabindex</c> also needs to change.
    /// </summary>
    public required Action NotifyStateChanged { get; init; }

    internal void RegisterItem(string id, ElementReference element, bool disabled)
    {
        var index = _items.FindIndex(i => i.Id == id);
        if (index >= 0)
        {
            _items[index] = (id, element, disabled);
        }
        else
        {
            _items.Add((id, element, disabled));
        }

        Focus.WithItems([.. _items.Select(i => (i.Id, i.Disabled))]);
    }

    internal void UnregisterItem(string id)
    {
        _items.RemoveAll(i => i.Id == id);
        Focus.WithItems([.. _items.Select(i => (i.Id, i.Disabled))]);
    }

    internal async Task MoveAsync(string key)
    {
        if (!Focus.HandleKey(key))
        {
            return;
        }

        NotifyStateChanged();
        await FocusActiveItemAsync();
    }

    internal async Task FocusActiveItemAsync()
    {
        if (Focus.ActiveId is null)
        {
            return;
        }

        var item = _items.Find(i => i.Id == Focus.ActiveId);
        await item.Element.FocusAsync();
    }

    internal async Task FocusTriggerAsync() => await TriggerElement.FocusAsync();

    internal async Task SelectItemAsync(EventCallback onClick)
    {
        if (onClick.HasDelegate)
        {
            await onClick.InvokeAsync();
        }

        await RequestClose();
    }
}
