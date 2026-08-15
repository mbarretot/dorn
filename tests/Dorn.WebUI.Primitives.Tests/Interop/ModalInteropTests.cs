using Bunit;
using Dorn.WebUI.Primitives.Interop;
using Microsoft.JSInterop;
using Xunit;

namespace Dorn.WebUI.Primitives.Tests.Interop;

public class ModalInteropTests : UiTestContext
{
    [Fact]
    public async Task OpenAsync_InvokesJsOpen_WithDialogElementDotNetRefAndInitialFocus()
    {
        var open = ModalModule.SetupVoid("open", _ => true).SetVoidResult();
        var sut = new ModalInterop(JSInterop.JSRuntime);
        var dotNetRef = DotNetObjectReference.Create(new object());

        await sut.OpenAsync(default, dotNetRef, "#confirm");

        var invocation = Assert.Single(open.Invocations);
        Assert.Equal("#confirm", invocation.Arguments[2]);
    }

    [Fact]
    public async Task CloseAsync_InvokesJsClose_WithDialogElement()
    {
        var close = ModalModule.SetupVoid("close", _ => true).SetVoidResult();
        var sut = new ModalInterop(JSInterop.JSRuntime);

        await sut.CloseAsync(default);

        Assert.Single(close.Invocations);
    }
}
