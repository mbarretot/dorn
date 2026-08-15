using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CleanArchBlazorServer.Web.Components.Ui.Primitives.Interop;

// Wraps ui-modal.js: showModal/close, ref-counted scroll lock, initial focus. Sole consumer: DialogContent.
public sealed class ModalInterop(IJSRuntime jsRuntime)
    : UiInteropModule(jsRuntime, "./js/ui/ui-modal.js")
{
    public async Task OpenAsync<T>(
        ElementReference dialogElement,
        DotNetObjectReference<T> dotNetRef,
        string? initialFocusSelector
    )
        where T : class
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("open", dialogElement, dotNetRef, initialFocusSelector);
    }

    public async Task CloseAsync(ElementReference dialogElement)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("close", dialogElement);
    }
}
