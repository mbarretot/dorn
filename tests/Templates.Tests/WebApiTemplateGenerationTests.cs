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

            var formatResult = await RunDotnetFormatVerifyAsync(slnFiles[0]);

            Assert.True(
                formatResult.ExitCode == 0,
                "dotnet format --verify-no-changes reported unformatted files."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{formatResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{formatResult.StdErr}"
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

            // The aspire .slnx now correctly references AppHost (Orchestrator symbol fix), so a
            // single build via the solution already compiles AppHost + Aspire.Hosting.SqlServer +
            // the #if (UseSqlServer) wiring in AppHost.cs/.csproj — no separate direct-csproj
            // build is needed anymore.
            Assert.Contains("AppHost", await File.ReadAllTextAsync(slnFiles[0]));

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );

            var formatResult = await RunDotnetFormatVerifyAsync(slnFiles[0]);

            Assert.True(
                formatResult.ExitCode == 0,
                "dotnet format --verify-no-changes reported unformatted files."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{formatResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{formatResult.StdErr}"
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
    /// Completes the 2x2 {aspire, docker-compose} x {sqlite, sqlserver} matrix at the template
    /// level (design section 8): docker-compose + sqlite has no AppHost/ServiceDefaults, gets a
    /// Dockerfile + base docker-compose.yml, and its `.slnx` doesn't reference AppHost.
    /// </summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithDockerComposeAndSqlite_ProducesBuildableSolution()
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
                "DornIntegrationTestComposeApp",
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["Orchestrator"] = "docker-compose" }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            Assert.False(
                Directory.Exists(
                    Path.Combine(outputDirectory, "src", "DornIntegrationTestComposeApp.AppHost")
                )
            );
            Assert.False(
                Directory.Exists(
                    Path.Combine(
                        outputDirectory,
                        "src",
                        "DornIntegrationTestComposeApp.ServiceDefaults"
                    )
                )
            );
            Assert.True(
                File.Exists(
                    Path.Combine(
                        outputDirectory,
                        "src",
                        "DornIntegrationTestComposeApp.WebApi",
                        "Dockerfile"
                    )
                )
            );
            Assert.True(File.Exists(Path.Combine(outputDirectory, "docker-compose.yml")));

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);
            Assert.DoesNotContain("AppHost", await File.ReadAllTextAsync(slnFiles[0]));

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );

            var formatResult = await RunDotnetFormatVerifyAsync(slnFiles[0]);

            Assert.True(
                formatResult.ExitCode == 0,
                "dotnet format --verify-no-changes reported unformatted files."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{formatResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{formatResult.StdErr}"
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
    /// Completes the 2x2 matrix's remaining cell: docker-compose + sqlserver. Asserts the
    /// generated docker-compose.yml carries the `sqlserver` service and the
    /// `ConnectionStrings__` environment override (design section 5/ADR-3), and that the
    /// generated appsettings.json has no stray `//#if` marker (proves the SQL Server branch of
    /// the `.cs`/`.json` conditional processing was selected cleanly).
    /// </summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithDockerComposeAndSqlServer_ProducesBuildableSolution()
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
                "DornIntegrationTestComposeSqlServerApp",
                outputDirectory,
                Parameters: new Dictionary<string, string>
                {
                    ["Orchestrator"] = "docker-compose",
                    ["DatabaseProvider"] = "sqlserver",
                }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            var composeFile = Path.Combine(outputDirectory, "docker-compose.yml");
            Assert.True(File.Exists(composeFile));
            var composeContent = await File.ReadAllTextAsync(composeFile);
            Assert.Contains("sqlserver:", composeContent);
            Assert.Contains("ConnectionStrings__", composeContent);

            var migrationsDirectory = Path.Combine(
                outputDirectory,
                "src",
                "DornIntegrationTestComposeSqlServerApp.Infrastructure",
                "Persistence",
                "Migrations"
            );
            Assert.True(Directory.Exists(migrationsDirectory));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "Sqlite")));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "SqlServer")));

            var appSettingsContent = await File.ReadAllTextAsync(
                Path.Combine(
                    outputDirectory,
                    "src",
                    "DornIntegrationTestComposeSqlServerApp.WebApi",
                    "appsettings.json"
                )
            );
            Assert.DoesNotContain("//#if", appSettingsContent);

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

            var formatResult = await RunDotnetFormatVerifyAsync(slnFiles[0]);

            Assert.True(
                formatResult.ExitCode == 0,
                "dotnet format --verify-no-changes reported unformatted files."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{formatResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{formatResult.StdErr}"
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
    ///
    /// Now that the aspire `.slnx` correctly references AppHost/ServiceDefaults (Orchestrator
    /// symbol fix), the restore graph has more entry points that can transitively re-evaluate the
    /// same shared project (e.g. AppHost -> WebApi -> ServiceDefaults vs. WebApi -> ServiceDefaults
    /// directly), which can still race on a shared generated file even with `-maxCpuCount:1` on the
    /// coordinating `dotnet restore` invocation. RestoreWithRetryAsync retries the restore step a
    /// bounded number of times on that specific known-flaky failure signature.
    /// </summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotnetBuildAsync(
        string solutionPath
    )
    {
        var restoreResult = await RestoreWithRetryAsync(solutionPath);
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
    /// Retries `dotnet restore` on the known-flaky "file already exists" NuGet race (a shared
    /// generated file, e.g. *.nuget.g.props/project.assets.json, written concurrently by two
    /// restore graph entry points that transitively reach the same project — see the remarks on
    /// <see cref="RunDotnetBuildAsync"/>). Only that specific signature is retried; any other
    /// restore failure returns immediately on the first attempt.
    /// </summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RestoreWithRetryAsync(
        string solutionPath,
        int maxAttempts = 3
    )
    {
        var localNuGetFeed = ResolveLocalNuGetFeed();
        (int ExitCode, string StdOut, string StdErr) result = (1, "", "");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            result = await RunProcessAsync(
                solutionPath,
                "restore",
                solutionPath,
                $"-p:RestoreAdditionalProjectSources={localNuGetFeed}",
                "-nodeReuse:false",
                "-maxCpuCount:1"
            );

            if (result.ExitCode == 0)
            {
                return result;
            }

            // MSBuild writes this specific error to stdout (its console logger), not stderr.
            var isKnownRace =
                result.StdOut.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || result.StdErr.Contains("already exists", StringComparison.OrdinalIgnoreCase);
            if (!isKnownRace || attempt == maxAttempts)
            {
                return result;
            }
        }

        return result;
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

    /// <summary>
    /// Runs `dotnet format --verify-no-changes` against a generated project's solution.
    /// Reuses <see cref="RunProcessAsync"/> the same way <see cref="RunDotnetBuildAsync"/>
    /// does. `--no-restore` is safe here because the preceding <see cref="RunDotnetBuildAsync"/>
    /// call already restored the solution (project.assets.json is present), so `dotnet format`
    /// doesn't need to run its own implicit restore. `dotnet format` reads the generated
    /// project's own renamed `.editorconfig` (the template engine renames `CleanArchWebApi` ->
    /// `&lt;Name&gt;` inside it), so this also validates the `generated_code = true` EF Core
    /// Migrations exclusion per generated project, not just against the raw template.
    ///
    /// NOTE: The `.editorconfig` deliberately does NOT specify `dotnet_sort_import_directives_alphabetically`
    /// or `dotnet_sort_system_directives_first`. The template's import order (project namespaces first,
    /// then third-party) is preserved as-is. Specifying alphabetical sorting would break the generated
    /// output because `dotnet format` sorts lexicographically after substitution, and the resulting
    /// order depends on the actual project name (e.g. `DornIntegrationTestApp.Application` sorts
    /// differently than the literal `CleanArchWebApi.Application`).
    /// </summary>
    private static Task<(int ExitCode, string StdOut, string StdErr)> RunDotnetFormatVerifyAsync(
        string solutionPath
    ) =>
        RunProcessAsync(
            solutionPath,
            "format",
            solutionPath,
            "--verify-no-changes",
            "--no-restore"
        );

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
