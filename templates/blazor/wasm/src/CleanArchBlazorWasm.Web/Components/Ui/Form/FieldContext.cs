namespace CleanArchBlazorWasm.Web.Components.Ui.Form;

/// <summary>
/// Cascaded by <see cref="FormField"/> so <see cref="Label"/> and <see cref="Input"/> share one
/// generated id without a child-to-parent round trip (design D, Input+Label part) — the same
/// shape as <c>Dialog</c>'s <c>DialogContext</c>.
/// </summary>
public sealed class FieldContext
{
    public required string Id { get; init; }
}
