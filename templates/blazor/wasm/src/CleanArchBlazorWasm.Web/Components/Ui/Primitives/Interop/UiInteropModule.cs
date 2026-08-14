using Microsoft.JSInterop;

namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives.Interop;

/// <summary>
/// Shared lazy-import base for the three owned JS modules (design C8). The import only starts
/// on first access to <see cref="GetModuleAsync"/> — constructing this class (or a subclass)
/// never touches <see cref="IJSRuntime"/>, so it is safe to inject into a component's
/// constructor. Consumers must still only call <see cref="GetModuleAsync"/> from
/// <c>OnAfterRenderAsync</c> (design C9): eager import works on WASM but would throw on a
/// Blazor Server prerender, which this template intentionally stays free of assuming.
/// </summary>
public abstract class UiInteropModule(IJSRuntime jsRuntime, string modulePath)
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", modulePath).AsTask()
    );

    protected Task<IJSObjectReference> GetModuleAsync() => _moduleTask.Value;
}
