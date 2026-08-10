using Dorn.Cli.Theming;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Xunit;

namespace Dorn.Cli.Tests.Theming;

///<summary>
/// Mirrors the exact DI registration <c>Program.cs</c> performs
/// (<c>services.AddSingleton(AnsiConsole.Console); services.AddSingleton&lt;IDornTheme, DornTheme&gt;();</c>)
/// so a break in that wiring surfaces as a unit-test failure instead of only at CLI startup.
///</summary>
public class DornThemeRegistrationTests
{
    [Fact]
    public void ServiceCollection_RegistersDornThemeAsSingletonAlongsideAnsiConsole()
    {
        var services = new ServiceCollection();
        services.AddSingleton(AnsiConsole.Console);
        services.AddSingleton<IDornTheme, DornTheme>();

        using var provider = services.BuildServiceProvider();

        var console = provider.GetRequiredService<IAnsiConsole>();
        var theme1 = provider.GetRequiredService<IDornTheme>();
        var theme2 = provider.GetRequiredService<IDornTheme>();

        Assert.NotNull(console);
        Assert.NotNull(theme1);
        Assert.Same(theme1, theme2);
    }
}
