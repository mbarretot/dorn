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
                "*.sln",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);
            Assert.Equal("DornIntegrationTestApp.sln", Path.GetFileName(slnFiles[0]));

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
    /// Runs restore and build as two separate dotnet invocations rather than a single
    /// `dotnet build` (which does an implicit restore). A brand-new solution graph like the
    /// one just generated here has no project.assets.json yet, and MSBuild's parallel
    /// project build nodes can each trigger the Restore target on a shared dependency (e.g.
    /// every layer references Domain) at the same time, racing to write the same generated
    /// `*.csproj.nuget.g.props` file and failing with "file already exists". Restoring the
    /// whole solution up front as one coordinated NuGet operation avoids that race.
    /// </summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotnetBuildAsync(
        string solutionPath
    )
    {
        var restoreResult = await RunProcessAsync(solutionPath, "restore", solutionPath);
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
            "--no-restore"
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
