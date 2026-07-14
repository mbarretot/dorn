using System.Diagnostics;
using Dorn.Abstractions.Generation;
using Dorn.Core.DependencyInjection;
using Dorn.Core.Templating;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Templates.Tests;

/// <summary>
/// Confidence-building integration test (plan section 5): generates the real dorn-webapi
/// template into a temp directory deliberately OUTSIDE the repo checkout, then runs a real
/// nested `dotnet build` on the generated solution as a subprocess. This is the only test
/// that proves templates/webapi's Directory.Build.props/Directory.Packages.props are truly
/// self-contained (see the MSBuild note in the plan) rather than only building because it
/// happens to sit inside the repo tree.
/// </summary>
[Trait("Category", "Integration")]
public class WebApiTemplateGenerationTests
{
    private const string LocalNuGetFeedEnvironmentVariableName = "DORN_LOCAL_NUGET_FEED";

    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplate_ProducesBuildableSolution()
    {
        // Reuses Dorn.Core's own TemplateLocator instead of re-implementing the
        // DORN_TEMPLATES_PATH / directory-walk resolution logic here, so this test finds
        // templates/ the exact same way the real engine does in production and in CI.
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-tests-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestApp",
                outputDirectory
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);
            Assert.Equal("DornIntegrationTestApp.slnx", Path.GetFileName(slnFiles[0]));

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Generates with DatabaseProvider = "sqlserver" and builds the result. This is what
    /// actually catches a migration namespace collision or a bad #if/Condition/rename
    /// modifier: if both provider-specific migration folders ever landed in the same
    /// output (or neither did), this build would fail with a duplicate/missing
    /// ApplicationDbContextModelSnapshot, a missing Aspire.Hosting.SqlServer reference, or
    /// a stray "//#if" left in appsettings.json.
    /// </summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithSqlServer_ProducesBuildableSolution()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-tests-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestSqlServerApp",
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["DatabaseProvider"] = "sqlserver" }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            var migrationsDirectory = Path.Combine(
                outputDirectory,
                "src",
                "DornIntegrationTestSqlServerApp.Infrastructure",
                "Persistence",
                "Migrations"
            );
            Assert.True(Directory.Exists(migrationsDirectory));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "Sqlite")));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "SqlServer")));
            Assert.Single(
                Directory.GetFiles(migrationsDirectory, "*ModelSnapshot.cs"),
                path => Path.GetFileName(path) == "ApplicationDbContextModelSnapshot.cs"
            );

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );

            // The generated .slnx never references the AppHost project (pre-existing, not
            // specific to SQL Server), so the build above doesn't touch the Aspire.Hosting.SqlServer
            // reference or the #if (UseSqlServer) wiring in AppHost.cs/.csproj — build it directly.
            var appHostCsproj = Directory
                .GetFiles(outputDirectory, "*.AppHost.csproj", SearchOption.AllDirectories)
                .Single();
            var appHostBuildResult = await RunDotnetBuildAsync(appHostCsproj);

            Assert.True(
                appHostBuildResult.ExitCode == 0,
                $"AppHost build exited with {appHostBuildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{appHostBuildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{appHostBuildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Runs restore and build as two separate dotnet invocations rather than a single
    /// `dotnet build` (which does an implicit restore). A brand-new solution graph like the
    /// one just generated here has no project.assets.json yet, and MSBuild's parallel
    /// project build nodes can each trigger the Restore target on a shared dependency (e.g.
    /// every layer references Domain) at the same time, racing to write the same generated
    /// `*.csproj.nuget.g.props` file and failing with "file already exists". Restoring the
    /// whole solution up front as one coordinated NuGet operation avoids that race.
    ///
    /// The generated solution lives outside the repo (Path.GetTempPath()), so it doesn't see
    /// the repo root's nuget.config "dorn-local" source that resolves Dorn.Messaging.Contracts/
    /// Dorn.Messaging/Dorn.SharedKernel. RestoreAdditionalProjectSources points restore at the
    /// same local feed explicitly, mirroring how TemplateLocator resolves the templates root.
    ///
    /// `-nodeReuse:false` on both invocations: a persisted MSBuild worker node from one nested
    /// build can be reused by the next one (even across sequential test runs) and hang once its
    /// cached state points at a since-deleted temp directory. These solutions are always deleted
    /// right after the build, so there's no warm-cache benefit worth keeping node reuse for.
    /// </summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotnetBuildAsync(
        string solutionPath
    )
    {
        var localNuGetFeed = ResolveLocalNuGetFeed();

        var restoreResult = await RunProcessAsync(
            solutionPath,
            "restore",
            solutionPath,
            $"-p:RestoreAdditionalProjectSources={localNuGetFeed}",
            "-nodeReuse:false"
        );
        if (restoreResult.ExitCode != 0)
        {
            return restoreResult;
        }

        return await RunProcessAsync(
            solutionPath,
            "build",
            solutionPath,
            "-c",
            "Release",
            "--no-restore",
            "-nodeReuse:false"
        );
    }

    /// <summary>
    /// Resolves the absolute path of Dorn's local NuGet feed (./artifacts, populated by
    /// eng/scripts/pack-packages.ps1), the same way TemplateLocator.ResolveTemplatesRoot
    /// resolves the templates root: an environment variable first, then a directory walk
    /// fallback from this test assembly's own location.
    /// </summary>
    private static string ResolveLocalNuGetFeed()
    {
        var envOverride = Environment.GetEnvironmentVariable(LocalNuGetFeedEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return Path.GetFullPath(envOverride);
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "artifacts");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Dorn's local NuGet feed. Set the {LocalNuGetFeedEnvironmentVariableName} "
                + "environment variable to point at the repo's 'artifacts' directory (see "
                + "eng/scripts/pack-packages.ps1), or run the tests from a repo checkout that already has one."
        );
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
        string solutionPath,
        params string[] arguments
    )
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(solutionPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the nested dotnet process.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await stdOutTask, await stdErrTask);
    }
}
