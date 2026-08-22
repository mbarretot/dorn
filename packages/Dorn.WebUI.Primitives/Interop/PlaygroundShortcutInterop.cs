using Microsoft.JSInterop;

namespace Dorn.WebUI.Primitives.Interop;

public sealed class PlaygroundShortcutInterop(IJSRuntime jsRuntime)
    : UiInteropModule(jsRuntime, "./js/playground/playground-shortcut.js")
{
    public async Task ActivateAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("activate");
    }

    public async Task DeactivateAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("deactivate");
    }
}
