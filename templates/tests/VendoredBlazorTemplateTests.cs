using System.Text.Json;
using Dorn.Core.Templating;
using Dorn.Core.Validation;
using Xunit;

namespace Dorn.Templates.Tests;

// Cross-repo drift guard (ADR 0027, design D6): the blazor template sources live in
// mbarretot/dorn-templates-blazor and are vendored back into templates/blazor/{wasm,server}
// at build time. This is the one dorn-side check that catches the Theme allow-list ever
// diverging between ThemeValidator and the vendored template.json manifests.
public class VendoredBlazorTemplateTests
{
    [Theory]
    [InlineData("wasm")]
    [InlineData("server")]
    public void ThemeChoices_ExactlyMatch_ThemeValidatorAllowList(string family)
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        var templateJsonPath = Path.Combine(
            templatesRoot,
            "blazor",
            family,
            ".template.config",
            "template.json"
        );
        Assert.True(File.Exists(templateJsonPath), $"Expected {templateJsonPath} to exist.");

        using var templateJson = JsonDocument.Parse(File.ReadAllText(templateJsonPath));
        var choices = templateJson
            .RootElement.GetProperty("symbols")
            .GetProperty("Theme")
            .GetProperty("choices")
            .EnumerateArray()
            .Select(choice => choice.GetProperty("choice").GetString()!)
            .ToArray();

        Assert.Equal(ThemeValidator.ValidThemes, choices);
    }
}
