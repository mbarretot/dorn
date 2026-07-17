namespace Dorn.Cli.Projects;

/// <summary>
/// Default implementation of <see cref="IProjectContextResolver"/> that uses
/// suffix-based glob patterns to detect the project layout produced by
/// <c>templates/webapi</c>.
/// </summary>
public sealed class ProjectContextResolver : IProjectContextResolver
{
    private static readonly string[] SolutionExtensions = [".slnx"];

    public ProjectContext Resolve(string rootPath)
    {
        var root = Path.GetFullPath(rootPath);

        var solutionPath = ResolveSolutionPath(root);
        var orchestrator = ResolveOrchestrator(root);
        var webApiProject = ResolveWebApiProject(root);
        var tiers = ResolveTiers(root);

        return new ProjectContext(root, solutionPath, orchestrator, webApiProject, tiers);
    }

    private static string ResolveSolutionPath(string root)
    {
        return Directory
                .EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = false })
                .Where(f =>
                    SolutionExtensions.Contains(
                        Path.GetExtension(f),
                        StringComparer.OrdinalIgnoreCase
                    )
                )
                .Select(f => Path.GetFullPath(f))
                .FirstOrDefault()
            ?? string.Empty;
    }

    private static Orchestrator ResolveOrchestrator(string root)
    {
        var srcDir = Path.Combine(root, "src");

        // AppHost presence takes precedence over docker-compose.yml.
        // Matches both "AppHost" and "*.AppHost" (e.g. "CleanArchWebApi.AppHost").
        if (Directory.Exists(srcDir))
        {
            var hasAppHost = Directory
                .EnumerateDirectories(srcDir, "*")
                .Any(d =>
                    Path.GetFileName(d).EndsWith(".AppHost", StringComparison.OrdinalIgnoreCase)
                );

            if (hasAppHost)
                return Orchestrator.Aspire;
        }

        var hasComposeFile = File.Exists(Path.Combine(root, "docker-compose.yml"));

        return hasComposeFile ? Orchestrator.Compose : Orchestrator.Plain;
    }

    private static string? ResolveWebApiProject(string root)
    {
        var srcDir = Path.Combine(root, "src");
        if (!Directory.Exists(srcDir))
            return null;

        return Directory
            .EnumerateDirectories(srcDir)
            .Select(d => Path.GetFullPath(d))
            .FirstOrDefault(d =>
                string.Equals(Path.GetFileName(d), "WebApi", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(d).EndsWith(".WebApi", StringComparison.OrdinalIgnoreCase)
            );
    }

    private static IReadOnlyList<TestTier> ResolveTiers(string root)
    {
        var testsDir = Path.Combine(root, "tests");
        if (!Directory.Exists(testsDir))
            return [];

        var tiers = new List<TestTier>();

        foreach (var subDir in Directory.EnumerateDirectories(testsDir))
        {
            var name = Path.GetFileName(subDir);

            // Matches *.Unit.Tests, *.Application.Tests, *.Integration.Tests,
            //          *.Architecture.Tests, *.Functional.Tests
            if (name.EndsWith(".Unit.Tests", StringComparison.OrdinalIgnoreCase))
                tiers.Add(TestTier.Unit);
            else if (name.EndsWith(".Application.Tests", StringComparison.OrdinalIgnoreCase))
                tiers.Add(TestTier.Application);
            else if (name.EndsWith(".Integration.Tests", StringComparison.OrdinalIgnoreCase))
                tiers.Add(TestTier.Integration);
            else if (name.EndsWith(".Architecture.Tests", StringComparison.OrdinalIgnoreCase))
                tiers.Add(TestTier.Architecture);
            else if (name.EndsWith(".Functional.Tests", StringComparison.OrdinalIgnoreCase))
                tiers.Add(TestTier.Functional);
        }

        return tiers;
    }
}
