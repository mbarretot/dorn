using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives.Interop;

/// <summary>
/// Wraps <c>wwwroot/js/ui/ui-dismiss.js</c> (design C7): capture-phase outside pointerdown +
/// Escape for non-modal surfaces. No component consumes it yet — DropdownMenu/Select (PR6) are
/// its first consumers; Dialog uses <see cref="ModalInterop"/> instead (native
/// <c>cancel</c>/document-inertness already cover its dismissal needs).
/// </summary>
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
