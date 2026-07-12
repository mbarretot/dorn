using Dorn.Core.Templating;
using Xunit;

namespace Dorn.Core.Tests.Templating;

// TemplateLocator is a static class with a single resolution entry point that reads
// DORN_TEMPLATES_PATH directly from the process environment — there is no constructor or
// method parameter to inject an override, so these tests mutate the real environment
// variable and restore it in a finally block per test. That is safe here because assembly-
// level parallelization is disabled (see AssemblyInfo.cs), so no other test class can read
// or write DORN_TEMPLATES_PATH concurrently with these.
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
    public void ResolveTemplatesRoot_WithEnvironmentVariableUnset_FallsBackToDirectoryWalk()
    {
        var original = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, null);

            // The test host runs from tests/Dorn.Core.Tests/bin/<config>/net10.0, several
            // levels below this repo's root, which itself contains a real templates/
            // folder (templates/webapi has a .template.config). The directory-walk
            // fallback should find it without needing DORN_TEMPLATES_PATH set — this is
            // exactly the "sensible fallback" the fallback path is meant to provide.
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
