using System.Reflection;
using Dorn.Cli.Projects;
using Xunit;

namespace Dorn.Cli.Tests.Projects;

/// <summary>
/// Matrix tests for <see cref="ProjectContextResolver"/>. Exercises suffix-based project
/// detection against synthesized directory trees without touching the real filesystem.
/// </summary>
public class ProjectContextResolverTests : IDisposable
{
    private readonly string _tempRoot;

    public ProjectContextResolverTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dorn-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Orchestrator detection
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_WithAppHostSubdirectory_ReturnsAspire()
    {
        CreateFile(_tempRoot, "src/MyProject.AppHost/Program.cs");
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");
        CreateSolution(_tempRoot, "MyProject.sln");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Equal(Orchestrator.Aspire, ctx.Orchestrator);
    }

    [Fact]
    public void Resolve_WithDockerComposeFile_ReturnsCompose()
    {
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");
        CreateFile(_tempRoot, "docker-compose.yml", "version: '3.9'");
        CreateSolution(_tempRoot, "MyProject.sln");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Equal(Orchestrator.Compose, ctx.Orchestrator);
    }

    [Fact]
    public void Resolve_WithNeitherAppHostNorComposeFile_ReturnsPlain()
    {
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");
        CreateSolution(_tempRoot, "MyProject.sln");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Equal(Orchestrator.Plain, ctx.Orchestrator);
    }

    [Fact]
    public void Resolve_WithNoSolutionFile_ReturnsPlain()
    {
        // A bare WebApi project with no .slnx at the root still resolves.
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Equal(Orchestrator.Plain, ctx.Orchestrator);
    }

    // -------------------------------------------------------------------------
    // Solution path
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_WithSolutionFile_ReturnsSolutionPath()
    {
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");
        CreateSolution(_tempRoot, "MyProject.sln");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.EndsWith("MyProject.slnx", ctx.SolutionPath);
    }

    [Fact]
    public void Resolve_WithNoSolutionFile_ReturnsEmptySolutionPath()
    {
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Empty(ctx.SolutionPath);
    }

    // -------------------------------------------------------------------------
    // WebApi project
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_WithWebApiSubdirectory_ReturnsWebApiProject()
    {
        CreateFile(_tempRoot, "src/CleanArch.WebApi/Program.cs");
        CreateSolution(_tempRoot, "CleanArch.sln");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.EndsWith("CleanArch.WebApi", ctx.WebApiProject);
    }

    [Fact]
    public void Resolve_WithNoWebApiSubdirectory_ReturnsNullWebApiProject()
    {
        CreateSolution(_tempRoot, "MyProject.sln");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Null(ctx.WebApiProject);
    }

    // -------------------------------------------------------------------------
    // Tier detection
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_WithAllFiveTierProjects_ReturnsAllFiveTiers()
    {
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");
        CreateSolution(_tempRoot, "MyProject.sln");
        CreateFile(_tempRoot, "tests/MyProject.Unit.Tests/UnitTest1.cs");
        CreateFile(_tempRoot, "tests/MyProject.Application.Tests/ApplicationTest1.cs");
        CreateFile(_tempRoot, "tests/MyProject.Integration.Tests/IntegrationTest1.cs");
        CreateFile(_tempRoot, "tests/MyProject.Architecture.Tests/ArchitectureTest1.cs");
        CreateFile(_tempRoot, "tests/MyProject.Functional.Tests/FunctionalTest1.cs");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Equal(5, ctx.Tiers.Count);
        Assert.Contains(TestTier.Unit, ctx.Tiers);
        Assert.Contains(TestTier.Application, ctx.Tiers);
        Assert.Contains(TestTier.Integration, ctx.Tiers);
        Assert.Contains(TestTier.Architecture, ctx.Tiers);
        Assert.Contains(TestTier.Functional, ctx.Tiers);
    }

    [Fact]
    public void Resolve_WithOnlyUnitTier_ReturnsOnlyUnit()
    {
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");
        CreateSolution(_tempRoot, "MyProject.sln");
        CreateFile(_tempRoot, "tests/MyProject.Unit.Tests/UnitTest1.cs");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Single(ctx.Tiers);
        Assert.Contains(TestTier.Unit, ctx.Tiers);
    }

    [Fact]
    public void Resolve_WithNoTestProjects_ReturnsEmptyTiers()
    {
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");
        CreateSolution(_tempRoot, "MyProject.sln");
        // No tests/ subdirectory at all.

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Empty(ctx.Tiers);
    }

    [Fact]
    public void Resolve_WithNonStandardTestSuffix_OmitsThoseTiers()
    {
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");
        CreateSolution(_tempRoot, "MyProject.sln");
        // Only Unit and Integration, no Architecture or Functional.
        CreateFile(_tempRoot, "tests/MyProject.Unit.Tests/UnitTest1.cs");
        CreateFile(_tempRoot, "tests/MyProject.Integration.Tests/IntegrationTest1.cs");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Equal(2, ctx.Tiers.Count);
        Assert.Contains(TestTier.Unit, ctx.Tiers);
        Assert.Contains(TestTier.Integration, ctx.Tiers);
        Assert.DoesNotContain(TestTier.Architecture, ctx.Tiers);
        Assert.DoesNotContain(TestTier.Functional, ctx.Tiers);
    }

    // -------------------------------------------------------------------------
    // Root
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_ReturnsGivenRoot()
    {
        CreateFile(_tempRoot, "src/MyProject.WebApi/Program.cs");

        var resolver = new ProjectContextResolver();
        var ctx = resolver.Resolve(_tempRoot);

        Assert.Equal(_tempRoot, ctx.Root);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void CreateSolution(string root, string name)
    {
        // The template generates *.slnx (Solution Explorer format), not *.sln.
        var baseName = name.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
            ? name
            : Path.GetFileNameWithoutExtension(name) + ".slnx";
        var slnxPath = Path.Combine(root, baseName);
        // A minimal .slnx is just XML with a <Solution> element — enough for glob detection.
        File.WriteAllText(slnxPath, "<Solution />");
    }

    private static void CreateFile(string root, string relativePath, string content = "")
    {
        var fullPath = Path.Combine(root, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
    }
}
