using Bunit;
using CleanArchBlazorWasm.Web.Components.Ui.Primitives.Interop;
using Microsoft.JSInterop;
using Xunit;

namespace CleanArchBlazorWasm.Functional.Tests.Ui.Primitives.Interop;

/// <summary>
/// <see cref="DismissInterop"/> wraps <c>wwwroot/js/ui/ui-dismiss.js</c> (design C7) — outside
/// pointerdown + Escape for non-modal surfaces. No component consumes it yet (DropdownMenu/
/// Select ship in PR6); this locks the wrapper's own call shape so PR6 composes it, not
/// reinvents it.
/// </summary>
public class DismissInteropTests : UiTestContext
{
    [Fact]
    public async Task ActivateAsync_InvokesJsActivate_WithIdContainerAndDotNetRef()
    {
        var activate = DismissModule.SetupVoid("activate", _ => true).SetVoidResult();
        var sut = new DismissInterop(JSInterop.JSRuntime);
        var dotNetRef = DotNetObjectReference.Create(new object());

        await sut.ActivateAsync("menu-1", default, dotNetRef);

        var invocation = Assert.Single(activate.Invocations);
        Assert.Equal("menu-1", invocation.Arguments[0]);
    }

    [Fact]
    public async Task DeactivateAsync_InvokesJsDeactivate_WithId()
    {
        var deactivate = DismissModule.SetupVoid("deactivate", _ => true).SetVoidResult();
        var sut = new DismissInterop(JSInterop.JSRuntime);

        await sut.DeactivateAsync("menu-1");

        var invocation = Assert.Single(deactivate.Invocations);
        Assert.Equal("menu-1", invocation.Arguments[0]);
    }
}
