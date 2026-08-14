using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives.Interop;

/// <summary>
/// Wraps <c>wwwroot/js/ui/ui-modal.js</c> (design C7): <c>showModal</c>/<c>close</c>, ref-counted
/// body scroll lock, explicit initial-focus placement, and the <c>cancel</c>-event bridge. The
/// sole consumer is <see cref="Dialog.DialogContent"/>.
/// </summary>
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
