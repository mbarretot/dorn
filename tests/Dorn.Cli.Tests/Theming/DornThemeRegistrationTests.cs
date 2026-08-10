using Dorn.Cli.Theming;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Xunit;

namespace Dorn.Cli.Tests.Theming;

// Mirrors Program.cs's exact registration lines so a wiring break fails here, not only
// at CLI startup.
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
