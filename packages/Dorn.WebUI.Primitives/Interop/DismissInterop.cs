using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Dorn.WebUI.Primitives.Interop;

// Wraps ui-dismiss.js: outside-pointerdown + Escape dismiss for non-modal surfaces (DropdownMenu/Select).
public sealed class DismissInterop(IJSRuntime jsRuntime)
    : UiInteropModule(jsRuntime, "./js/ui/ui-dismiss.js")
{
    public async Task ActivateAsync<T>(
        string id,
        ElementReference containerElement,
        DotNetObjectReference<T> dotNetRef
    )
        where T : class
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("activate", id, containerElement, dotNetRef);
    }

    public async Task DeactivateAsync(string id)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("deactivate", id);
    }
}
