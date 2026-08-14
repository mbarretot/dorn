using Bunit;
using CleanArchBlazorWasm.Web.Components.Ui.Primitives.Interop;
using Xunit;

namespace CleanArchBlazorWasm.Functional.Tests.Ui.Primitives.Interop;

/// <summary>
/// <see cref="AnchorInterop"/> wraps <c>wwwroot/js/ui/ui-anchor.js</c> (design C7) — anchored
/// positioning. No component consumes it yet (DropdownMenu/Select ship in PR6); this locks the
/// wrapper's own call shape so PR6 composes it, not reinvents it.
/// </summary>
public class AnchorInteropTests : UiTestContext
{
    [Fact]
    public async Task PositionAsync_InvokesJsPosition_WithSideAlignOffsetAndCollisionPadding()
    {
        var position = AnchorModule.SetupVoid("position", _ => true).SetVoidResult();
        var sut = new AnchorInterop(JSInterop.JSRuntime);

        await sut.PositionAsync(default, default, "bottom", "start", 4, 8);

        var invocation = Assert.Single(position.Invocations);
        Assert.Equal("bottom", invocation.Arguments[2]);
        Assert.Equal("start", invocation.Arguments[3]);
        Assert.Equal(4d, invocation.Arguments[4]);
        Assert.Equal(8d, invocation.Arguments[5]);
    }

    [Fact]
    public async Task DisposeAsync_InvokesJsDispose_WithFloatingElement()
    {
        var dispose = AnchorModule.SetupVoid("dispose", _ => true).SetVoidResult();
        var sut = new AnchorInterop(JSInterop.JSRuntime);

        await sut.DisposeAsync(default);

        Assert.Single(dispose.Invocations);
    }

    [Fact]
    public async Task ShowAsync_InvokesJsShow_WithFloatingElement()
    {
        var show = AnchorModule.SetupVoid("show", _ => true).SetVoidResult();
        var sut = new AnchorInterop(JSInterop.JSRuntime);

        await sut.ShowAsync(default);

        Assert.Single(show.Invocations);
    }

    [Fact]
    public async Task HideAsync_InvokesJsHide_WithFloatingElement()
    {
        var hide = AnchorModule.SetupVoid("hide", _ => true).SetVoidResult();
        var sut = new AnchorInterop(JSInterop.JSRuntime);

        await sut.HideAsync(default);

        Assert.Single(hide.Invocations);
    }
}
