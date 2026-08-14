using Microsoft.JSInterop;

namespace CleanArchBlazorServer.Web.Components.Ui.Primitives.Interop;

// GetModuleAsync must only be called from OnAfterRenderAsync/DisposeAsync — under Server, OnAfterRenderAsync never fires during prerender, so this gate is load-bearing, not hypothetical.
public abstract class UiInteropModule(IJSRuntime jsRuntime, string modulePath)
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", modulePath).AsTask()
    );

    protected Task<IJSObjectReference> GetModuleAsync() => _moduleTask.Value;
}
