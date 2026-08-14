using CleanArchBlazorServer.Web.Components.Theme;
using Xunit;

namespace CleanArchBlazorServer.Application.Tests.Theme;

public class ThemeStateTests
{
    [Theory]
    [InlineData(ThemeMode.Light, true, ThemeMode.Light)]
    [InlineData(ThemeMode.Light, false, ThemeMode.Light)]
    [InlineData(ThemeMode.Dark, true, ThemeMode.Dark)]
    [InlineData(ThemeMode.Dark, false, ThemeMode.Dark)]
    [InlineData(ThemeMode.System, true, ThemeMode.Dark)]
    [InlineData(ThemeMode.System, false, ThemeMode.Light)]
    public void ResolveMode_ReturnsExpectedConcreteMode(
        ThemeMode mode,
        bool systemPrefersDark,
        ThemeMode expected
    )
    {
        var resolved = ThemeState.ResolveMode(mode, systemPrefersDark);

        Assert.Equal(expected, resolved);
    }
}
