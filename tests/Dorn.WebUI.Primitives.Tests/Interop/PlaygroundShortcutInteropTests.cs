using Bunit;
using Dorn.WebUI.Primitives.Interop;
using Xunit;

namespace Dorn.WebUI.Primitives.Tests.Interop;

public class PlaygroundShortcutInteropTests : UiTestContext
{
    [Fact]
    public async Task ActivateAsync_InvokesJsActivate()
    {
        var activate = PlaygroundShortcutModule.SetupVoid("activate", _ => true).SetVoidResult();
        var sut = new PlaygroundShortcutInterop(JSInterop.JSRuntime);

        await sut.ActivateAsync();

        Assert.Single(activate.Invocations);
    }

    [Fact]
    public async Task DeactivateAsync_InvokesJsDeactivate()
    {
        var deactivate = PlaygroundShortcutModule
            .SetupVoid("deactivate", _ => true)
            .SetVoidResult();
        var sut = new PlaygroundShortcutInterop(JSInterop.JSRuntime);

        await sut.DeactivateAsync();

        Assert.Single(deactivate.Invocations);
    }
}
