using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives.Interop;

/// <summary>
/// Wraps <c>wwwroot/js/ui/ui-anchor.js</c> (design C7): anchored positioning with side/align/
/// offset/collision-padding, flip on the main axis, clamp on the cross axis, and scroll/resize
/// re-observation until disposed. <see cref="ShowAsync"/>/<see cref="HideAsync"/> drive the
/// native <c>popover="manual"</c> surface (design C6) DropdownMenu/Select content renders into —
/// the anchor module owns the floating element's visibility because positioning is meaningless
/// until it is shown.
/// </summary>
public sealed class AnchorInterop(IJSRuntime jsRuntime)
    : UiInteropModule(jsRuntime, "./js/ui/ui-anchor.js")
{
    public async Task PositionAsync(
        ElementReference anchorElement,
        ElementReference floatingElement,
        string side,
        string align,
        double offset,
        double collisionPadding
    )
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync(
            "position",
            anchorElement,
            floatingElement,
            side,
            align,
            offset,
            collisionPadding
        );
    }

    public async Task DisposeAsync(ElementReference floatingElement)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("dispose", floatingElement);
    }

    public async Task ShowAsync(ElementReference floatingElement)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("show", floatingElement);
    }

    public async Task HideAsync(ElementReference floatingElement)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("hide", floatingElement);
    }
}
