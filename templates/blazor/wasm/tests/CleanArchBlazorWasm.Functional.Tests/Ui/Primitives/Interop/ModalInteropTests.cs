using Bunit;
using CleanArchBlazorWasm.Web.Components.Ui.Primitives.Interop;
using Microsoft.JSInterop;
using Xunit;

namespace CleanArchBlazorWasm.Functional.Tests.Ui.Primitives.Interop;

/// <summary>
/// <see cref="ModalInterop"/> wraps <c>wwwroot/js/ui/ui-modal.js</c> (design C7/C8) — the module
/// <see cref="Dialog.DialogContent"/> consumes. These tests exercise the wrapper's own call
/// shape directly, independent of any component.
/// </summary>
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
