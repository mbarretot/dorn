using Bunit;
using Dorn.WebUI.Primitives.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace Dorn.WebUI.Primitives.Tests;

// Strict JS interop (unconfigured call fails, not silently defaults) + the owned modules stubbed and wired like Program.cs.
public abstract class UiTestContext : BunitContext
{
    protected UiTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;

        ModalModule = JSInterop.SetupModule("./js/ui/ui-modal.js");
        DismissModule = JSInterop.SetupModule("./js/ui/ui-dismiss.js");
        AnchorModule = JSInterop.SetupModule("./js/ui/ui-anchor.js");
        JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true).SetVoidResult();

        Services.AddScoped(_ => new ModalInterop(JSInterop.JSRuntime));
        Services.AddScoped(_ => new DismissInterop(JSInterop.JSRuntime));
        Services.AddScoped(_ => new AnchorInterop(JSInterop.JSRuntime));
    }

    protected BunitJSModuleInterop ModalModule { get; }

    protected BunitJSModuleInterop DismissModule { get; }

    protected BunitJSModuleInterop AnchorModule { get; }
}
