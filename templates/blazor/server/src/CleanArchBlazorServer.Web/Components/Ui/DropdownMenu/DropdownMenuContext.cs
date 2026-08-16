using Dorn.WebUI.Primitives;
using Microsoft.AspNetCore.Components;

namespace CleanArchBlazorServer.Web.Components.Ui.DropdownMenu;

// Same cascading-context shape as DialogContext/TabsContext: open state, roving-tabindex state, element registry.
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

    // Re-renders the whole menu subtree — roving-tabindex movement mutates Focus from whichever item's own keydown handler ran, and Blazor only auto-rerenders that one component, not its siblings.
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
