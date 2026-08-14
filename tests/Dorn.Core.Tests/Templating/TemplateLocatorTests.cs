using Dorn.Core.Templating;
using Xunit;

namespace Dorn.Core.Tests.Templating;

// TemplateLocator reads DORN_TEMPLATES_PATH directly from the process environment (no
// constructor/method injection), so tests mutate the real env var and restore it in a
// finally block. Safe because assembly-level parallelization is disabled (AssemblyInfo.cs).
public class TemplateLocatorTests
{
    private const string EnvironmentVariableName = "DORN_TEMPLATES_PATH";

    [Fact]
    public void ResolveTemplatesRoot_WithEnvironmentVariableSet_ReturnsThatDirectory()
    {
        var tempRoot = Directory.CreateTempSubdirectory("dorn-locator-test-");
        var original = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, tempRoot.FullName);

            var resolved = TemplateLocator.ResolveTemplatesRoot();

            Assert.Equal(Path.GetFullPath(tempRoot.FullName), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, original);
            tempRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolveTemplatesRoot_WithEnvironmentVariablePointingToMissingDirectory_Throws()
    {
        var original = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"dorn-does-not-exist-{Guid.NewGuid():N}"
        );
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, missingPath);

            Assert.Throws<DirectoryNotFoundException>(() => TemplateLocator.ResolveTemplatesRoot());
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, original);
        }
    }

    [Fact]
    public void ResolveTemplatesRoot_WithBlazorGroupingSubfolder_StillResolvesRoot()
    {
        // templates/blazor/ is a non-template grouping folder; templates/blazor/wasm/ is the real one.
        var original = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, null);

            var resolved = TemplateLocator.ResolveTemplatesRoot();

            Assert.True(
                Directory.Exists(Path.Combine(resolved, "blazor", "wasm", ".template.config"))
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, original);
        }
    }

    [Fact]
    public void ResolveTemplatesRoot_WithEnvironmentVariableUnset_FallsBackToDirectoryWalk()
    {
        var original = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, null);

            // Test host runs from tests/Dorn.Core.Tests/bin/<config>/net10.0, several levels
            // below the repo root which contains a real templates/ folder (templates/webapi
            // has .template.config). The directory-walk fallback should find it without
            // DORN_TEMPLATES_PATH set.
            var resolved = TemplateLocator.ResolveTemplatesRoot();

            Assert.True(Directory.Exists(resolved));
            Assert.Equal("templates", Path.GetFileName(resolved));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, original);
        }
    }
}
