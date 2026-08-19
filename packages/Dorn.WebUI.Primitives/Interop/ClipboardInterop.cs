using Microsoft.JSInterop;

namespace Dorn.WebUI.Primitives.Interop;

// Wraps ui-clipboard.js: write-only clipboard copy, with a non-secure-context fallback.
public sealed class ClipboardInterop(IJSRuntime jsRuntime)
    : UiInteropModule(jsRuntime, "./js/ui/ui-clipboard.js")
{
    public async Task CopyAsync(string text)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("copy", text);
    }
}
