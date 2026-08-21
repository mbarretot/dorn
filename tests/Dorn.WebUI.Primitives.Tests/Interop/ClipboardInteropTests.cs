using Bunit;
using Dorn.WebUI.Primitives.Interop;
using Xunit;

namespace Dorn.WebUI.Primitives.Tests.Interop;

public class ClipboardInteropTests : UiTestContext
{
    [Fact]
    public async Task CopyAsync_InvokesJsCopy_WithText()
    {
        var copy = ClipboardModule.SetupVoid("copy", _ => true).SetVoidResult();
        var sut = new ClipboardInterop(JSInterop.JSRuntime);

        await sut.CopyAsync("const x = 1;");

        var invocation = Assert.Single(copy.Invocations);
        Assert.Equal("const x = 1;", invocation.Arguments[0]);
    }
}
