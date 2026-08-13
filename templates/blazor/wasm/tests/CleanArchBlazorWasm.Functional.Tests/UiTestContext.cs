using Bunit;
using CleanArchBlazorWasm.Web.Components.Ui.Primitives.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchBlazorWasm.Functional.Tests;

/// <summary>
/// Shared bUnit harness (design's Functional-tier description): Strict JS interop mode so an
/// unconfigured call fails the test instead of silently returning a default, the three owned
/// interop modules stubbed via <see cref="BunitJSInterop.SetupModule"/>, and their C# wrappers
/// pre-registered as scoped services (mirrors <c>Program.cs</c>).
/// </summary>
public abstract class UiTestContext : BunitContext
{
    protected UiTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;

        ModalModule = JSInterop.SetupModule("./js/ui/ui-modal.js");
        DismissModule = JSInterop.SetupModule("./js/ui/ui-dismiss.js");
        AnchorModule = JSInterop.SetupModule("./js/ui/ui-anchor.js");

        Services.AddScoped(_ => new ModalInterop(JSInterop.JSRuntime));
        Services.AddScoped(_ => new DismissInterop(JSInterop.JSRuntime));
        Services.AddScoped(_ => new AnchorInterop(JSInterop.JSRuntime));
    }

    protected BunitJSModuleInterop ModalModule { get; }

    protected BunitJSModuleInterop DismissModule { get; }

    protected BunitJSModuleInterop AnchorModule { get; }
}
