using System.Text.Json;
using Dorn.Core.Templating;
using Dorn.Core.Validation;
using Xunit;

namespace Dorn.Templates.Tests;

// Cross-repo drift guard (ADR 0028, mirroring ADR 0027's design D6): the webapi template source
// lives in mbarretot/dorn-templates-webapi and is vendored back into templates/webapi at build
// time. This is the one dorn-side check that catches the Auth/DatabaseProvider/Orchestrator/Orm
// allow-lists ever diverging between their validators and the vendored template.json manifest.
public class VendoredWebApiTemplateTests
{
    private static JsonElement ResolveTemplateJson()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        var templateJsonPath = Path.Combine(
            templatesRoot,
            "webapi",
            ".template.config",
            "template.json"
        );
        Assert.True(File.Exists(templateJsonPath), $"Expected {templateJsonPath} to exist.");

        return JsonDocument.Parse(File.ReadAllText(templateJsonPath)).RootElement;
    }

    private static string[] ReadChoices(JsonElement templateJson, string symbolName) =>
        templateJson
            .GetProperty("symbols")
            .GetProperty(symbolName)
            .GetProperty("choices")
            .EnumerateArray()
            .Select(choice => choice.GetProperty("choice").GetString()!)
            .ToArray();

    [Fact]
    public void AuthChoices_ExactlyMatch_AuthValidatorAllowList()
    {
        var choices = ReadChoices(ResolveTemplateJson(), "Auth");
        Assert.Equal(AuthValidator.ValidAuthModes, choices);
    }

    [Fact]
    public void DatabaseProviderChoices_ExactlyMatch_DatabaseProviderValidatorAllowList()
    {
        var choices = ReadChoices(ResolveTemplateJson(), "DatabaseProvider");
        Assert.Equal(DatabaseProviderValidator.ValidProviders, choices);
    }

    [Fact]
    public void OrchestratorChoices_ExactlyMatch_OrchestratorValidatorAllowList()
    {
        var choices = ReadChoices(ResolveTemplateJson(), "Orchestrator");
        Assert.Equal(OrchestratorValidator.ValidOrchestrators, choices);
    }

    [Fact]
    public void OrmChoices_ExactlyMatch_OrmValidatorAllowList()
    {
        var choices = ReadChoices(ResolveTemplateJson(), "Orm");
        Assert.Equal(OrmValidator.ValidOrms, choices);
    }
}
