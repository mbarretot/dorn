using Microsoft.AspNetCore.Components.Forms;

namespace CleanArchBlazorWasm.Web.Components.Ui.Form;

/// <summary>
/// Cascaded by <see cref="FormField"/> so <see cref="Label"/>, <see cref="Input"/> and
/// <see cref="FormMessage"/> share one generated id without a child-to-parent round trip
/// (design D, Input+Label part; design D5 for the message wiring) — the same shape as
/// <c>DropdownMenuContext</c>.
/// </summary>
public sealed class FieldContext
{
    public required string Id { get; init; }

    public string MessageId => $"{Id}-message";

    public FieldIdentifier FieldIdentifier { get; internal set; }

    public bool HasMessage { get; internal set; }

    /// <summary>
    /// Re-renders <see cref="FormField"/>'s subtree — <see cref="HasMessage"/> flips inside
    /// <see cref="FormMessage"/> but is read by its sibling <see cref="Input"/>.
    /// </summary>
    public required Action NotifyStateChanged { get; init; }

    internal void SetField(FieldIdentifier fieldIdentifier) => FieldIdentifier = fieldIdentifier;

    internal void SetHasMessage(bool hasMessage)
    {
        if (HasMessage == hasMessage)
        {
            return;
        }

        HasMessage = hasMessage;
        NotifyStateChanged();
    }
}
