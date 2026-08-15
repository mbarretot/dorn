using Bunit;
using Dorn.WebUI.Primitives.Interop;
using Microsoft.JSInterop;
using Xunit;

namespace Dorn.WebUI.Primitives.Tests.Interop;

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
