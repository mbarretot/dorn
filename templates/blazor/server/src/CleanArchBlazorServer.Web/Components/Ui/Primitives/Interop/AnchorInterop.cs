using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CleanArchBlazorServer.Web.Components.Ui.Primitives.Interop;

// Wraps ui-anchor.js: anchored positioning and owns visibility of the popover="manual" surface.
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
