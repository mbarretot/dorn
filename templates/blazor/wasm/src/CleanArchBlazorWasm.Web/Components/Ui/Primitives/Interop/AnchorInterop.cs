using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives.Interop;

/// <summary>
/// Wraps <c>wwwroot/js/ui/ui-anchor.js</c> (design C7): anchored positioning with side/align/
/// offset/collision-padding, flip on the main axis, clamp on the cross axis, and scroll/resize
/// re-observation until disposed. No component consumes it yet — DropdownMenu/Select (PR6) are
/// its first consumers.
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
}
