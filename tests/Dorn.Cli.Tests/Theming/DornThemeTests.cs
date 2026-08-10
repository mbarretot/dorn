using Dorn.Cli.Theming;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Theming;

// Capabilities are set explicitly per test — no test relies on TestConsole's defaults.
public class DornThemeTests
{
    [Theory]
    [InlineData(Severity.Success, "✔")]
    [InlineData(Severity.Error, "✖")]
    [InlineData(Severity.Warning, "▲")]
    [InlineData(Severity.Info, "•")]
    public void Message_UnicodeEnabled_UsesUnicodeGlyph(Severity severity, string expectedGlyph)
    {
        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = true;
        var theme = new DornTheme(console);

        theme.Message(severity, "hello");

        Assert.Contains(expectedGlyph, console.Output);
    }

    [Theory]
    [InlineData(Severity.Success, "+")]
    [InlineData(Severity.Error, "x")]
    [InlineData(Severity.Warning, "!")]
    [InlineData(Severity.Info, "-")]
    public void Message_UnicodeDisabled_FallsBackToAsciiGlyphWithNoNonAsciiOutput(
        Severity severity,
        string expectedAscii
    )
    {
        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = false;
        var theme = new DornTheme(console);

        theme.Message(severity, "hello");

        Assert.Contains(expectedAscii, console.Output);
        Assert.All(console.Output, c => Assert.True(c <= 127, $"Non-ASCII char '{c}' found."));
    }

    [Theory]
    [InlineData(Severity.Success)]
    [InlineData(Severity.Error)]
    [InlineData(Severity.Warning)]
    [InlineData(Severity.Info)]
    public void Message_GlyphResolvedPerCall_NotCachedAtConstruction(Severity severity)
    {
        // Flips Unicode after construction — would still render unicode if cached in ctor.
        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = true;
        var theme = new DornTheme(console);

        console.Profile.Capabilities.Unicode = false;
        theme.Message(severity, "hello");

        Assert.All(console.Output, c => Assert.True(c <= 127, $"Non-ASCII char '{c}' found."));
    }

    [Fact]
    public void Label_KeepsLiteralTextAlongsideGlyph()
    {
        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = true;
        var theme = new DornTheme(console);

        var label = theme.Label(Severity.Success, "PASS");

        Assert.Contains("PASS", label);
    }

    [Fact]
    public void OutcomePanel_RendersHeaderAndContent()
    {
        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = true;
        var theme = new DornTheme(console);

        theme.OutcomePanel(Severity.Error, "Invalid project name", "must not start with a digit");

        Assert.Contains("Invalid project name", console.Output);
        Assert.Contains("must not start with a digit", console.Output);
    }

    [Fact]
    public void CreateTable_UsesRoundedBorderAndTitle()
    {
        var console = new TestConsole().Width(int.MaxValue);
        var theme = new DornTheme(console);

        var table = theme.CreateTable("Environment checks");

        Assert.Equal(TableBorder.Rounded, table.Border);
        Assert.Equal("Environment checks", table.Title?.Text);
    }

    [Fact]
    public void LiveRegionsEnabled_MirrorsProfileInteractiveCapability()
    {
        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Interactive = true;
        var theme = new DornTheme(console);

        Assert.True(theme.LiveRegionsEnabled);

        console.Profile.Capabilities.Interactive = false;

        Assert.False(theme.LiveRegionsEnabled);
    }
}
