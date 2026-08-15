using Microsoft.JSInterop;

namespace Dorn.WebUI.Primitives.Interop;

// GetModuleAsync must only be called from OnAfterRenderAsync/DisposeAsync — under Server, OnAfterRenderAsync never fires during prerender, so this gate is load-bearing, not hypothetical.
// See the package README for the JS module path contract consumers must satisfy.
public abstract class UiInteropModule(IJSRuntime jsRuntime, string modulePath)
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", modulePath).AsTask()
    );

    protected Task<IJSObjectReference> GetModuleAsync() => _moduleTask.Value;
}
