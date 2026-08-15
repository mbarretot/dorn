using System.Diagnostics;
using System.Text.RegularExpressions;
using Dorn.Abstractions.Generation;
using Dorn.Core.DependencyInjection;
using Dorn.Core.Templating;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TemplateGenerationTests;

/// <summary>
/// Phase 1 go/no-go: proves the generated Blazor Server project builds on Sdk.Web, its Tailwind
/// pipeline produces real CSS through the fingerprinted static-asset pipeline, and the Tailwind
/// CLI acquisition mechanism (shared with blazor-wasm-template) still holds for this second
/// template. Threat-matrix cases build <c>CleanArchBlazorServer.Web.csproj</c> directly (not
/// through the generation engine) since they exercise <c>build/Tailwind.targets</c> itself.
/// </summary>
[Trait("Category", "Integration")]
public class BlazorServerTemplateGenerationTests
{
    private const string LocalNuGetFeedEnvironmentVariableName = "DORN_LOCAL_NUGET_FEED";
    private const string DornToolsHomeEnvironmentVariableName = "DORN_TOOLS_HOME";
    private const string DornTailwindPathEnvironmentVariableName = "DORN_TAILWIND_PATH";

    /// <summary>See <see cref="TemplateGenerationTests.BlazorWasmTemplateGenerationTests"/> for why this
    /// real chdir/getcwd round-trip is needed on macOS (symlinked /var -> /private/var).</summary>
    private static readonly string RealTempRoot = ResolveRealPath(Path.GetTempPath());

    [Fact]
    public async Task GenerateAndBuild_DornBlazorServerTemplate_ProducesRealTailwindCss()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-server-{Guid.NewGuid():N}"
        );
        var toolsHome = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-server-tools-{Guid.NewGuid():N}"
        );
        try
        {
            var request = new GenerationRequest(
                "dorn-blazor-server",
                "DornIntegrationTestBlazorServerApp",
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
            Assert.Equal("DornIntegrationTestBlazorServerApp.slnx", Path.GetFileName(slnFiles[0]));

            var buildResult = await RunDotnetBuildAsync(slnFiles[0], toolsHome);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );

            var appCssPath = Path.Combine(
                outputDirectory,
                "src",
                "DornIntegrationTestBlazorServerApp.Web",
                "wwwroot",
                "app.css"
            );
            Assert.True(File.Exists(appCssPath), $"Expected generated CSS at '{appCssPath}'.");

            var appCss = await File.ReadAllTextAsync(appCssPath);
            Assert.False(string.IsNullOrWhiteSpace(appCss));
            Assert.Contains("bg-primary", appCss, StringComparison.Ordinal);
            Assert.Contains("--ui-primary", appCss, StringComparison.Ordinal);
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("DORN_TEST_KEEP_TEMP") != "true")
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, recursive: true);
                }
                if (Directory.Exists(toolsHome))
                {
                    Directory.Delete(toolsHome, recursive: true);
                }
            }
            else
            {
                Console.WriteLine("KEPT: " + outputDirectory);
            }
        }
    }

    /// <summary>
    /// Phase 1 also ships the boot-default-theme mechanism (unlike blazor-wasm-template, which
    /// deferred it to its own theming phase) — design S-B found the mechanism hosting-agnostic
    /// and zero-risk, so there is no reason to gate it behind a later phase here.
    /// </summary>
    [Fact]
    public async Task GenerateWithThemeRose_ReplacesBootDefaultThemeLiteral_WithoutCorruptingSlateCss()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-server-theme-{Guid.NewGuid():N}"
        );
        try
        {
            var request = new GenerationRequest(
                "dorn-blazor-server",
                "DornServerThemeRoseApp",
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["Theme"] = "rose" }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );

            var themeBootPath = Path.Combine(
                outputDirectory,
                "src",
                "DornServerThemeRoseApp.Web",
                "wwwroot",
                "theme-boot.js"
            );
            Assert.True(File.Exists(themeBootPath), $"Expected boot script at '{themeBootPath}'.");

            var themeBoot = await File.ReadAllTextAsync(themeBootPath);
            Assert.Contains("DEFAULT_THEME = \"rose\"", themeBoot, StringComparison.Ordinal);
            Assert.DoesNotContain("DEFAULT_THEME = \"slate\"", themeBoot, StringComparison.Ordinal);

            var slateThemePath = Path.Combine(
                outputDirectory,
                "src",
                "DornServerThemeRoseApp.Web",
                "Styles",
                "themes",
                "slate.css"
            );
            var slateTheme = await File.ReadAllTextAsync(slateThemePath);
            Assert.Contains("[data-ui-theme='slate']", slateTheme, StringComparison.Ordinal);
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
    /// Threat matrix: a wrong expected checksum must fail the build with the mismatch message
    /// and must never leave an executable behind in the tool cache (A4).
    /// </summary>
    [Fact]
    public async Task Build_WithWrongExpectedTailwindChecksum_FailsWithMismatchError_AndLeavesCacheEmpty()
    {
        var toolsHome = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-server-tools-{Guid.NewGuid():N}"
        );
        try
        {
            var webCsprojPath = ResolveWebCsprojPath();
            var buildResult = await RunProcessAsync(
                Path.GetDirectoryName(webCsprojPath)!,
                new Dictionary<string, string?>
                {
                    [DornToolsHomeEnvironmentVariableName] = toolsHome,
                },
                "build",
                webCsprojPath,
                "-c",
                "Release",
                "-nodeReuse:false",
                "-p:TailwindSha256=0000000000000000000000000000000000000000000000000000000000000000"
            );

            Assert.NotEqual(0, buildResult.ExitCode);
            Assert.Contains(
                "checksum mismatch",
                buildResult.StdOut,
                StringComparison.OrdinalIgnoreCase
            );

            if (Directory.Exists(toolsHome))
            {
                var leftoverExecutables = Directory.GetFiles(
                    toolsHome,
                    "tailwindcss*",
                    SearchOption.AllDirectories
                );
                Assert.Empty(leftoverExecutables);
            }
        }
        finally
        {
            if (Directory.Exists(toolsHome))
            {
                Directory.Delete(toolsHome, recursive: true);
            }
        }
    }

    /// <summary>
    /// Threat matrix: an unmapped RID must fail with the override instruction instead of
    /// silently downloading an arbitrary asset (A7).
    /// </summary>
    [Fact]
    public async Task Build_WithUnmappedTailwindRid_FailsWithOverrideInstruction()
    {
        var toolsHome = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-server-tools-{Guid.NewGuid():N}"
        );
        try
        {
            var webCsprojPath = ResolveWebCsprojPath();
            var buildResult = await RunProcessAsync(
                Path.GetDirectoryName(webCsprojPath)!,
                new Dictionary<string, string?>
                {
                    [DornToolsHomeEnvironmentVariableName] = toolsHome,
                },
                "build",
                webCsprojPath,
                "-c",
                "Release",
                "-nodeReuse:false",
                "-p:DornTailwindRidOverride=bogus-unmapped-rid"
            );

            Assert.NotEqual(0, buildResult.ExitCode);
            Assert.Contains("DORN_TAILWIND_PATH", buildResult.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(toolsHome))
            {
                Directory.Delete(toolsHome, recursive: true);
            }
        }
    }

    /// <summary>
    /// Threat matrix: <c>DORN_TAILWIND_PATH</c> pointing at a missing file must fail the build
    /// with an actionable message, not an opaque exec error.
    /// </summary>
    [Fact]
    public async Task Build_WithDornTailwindPathPointingAtMissingFile_FailsWithActionableMessage()
    {
        var missingPath = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-server-missing-{Guid.NewGuid():N}.exe"
        );

        var webCsprojPath = ResolveWebCsprojPath();
        var buildResult = await RunProcessAsync(
            Path.GetDirectoryName(webCsprojPath)!,
            new Dictionary<string, string?>
            {
                [DornTailwindPathEnvironmentVariableName] = missingPath,
            },
            "build",
            webCsprojPath,
            "-c",
            "Release",
            "-nodeReuse:false"
        );

        Assert.NotEqual(0, buildResult.ExitCode);
        Assert.Contains("DORN_TAILWIND_PATH", buildResult.StdOut, StringComparison.Ordinal);
        Assert.Contains(missingPath, buildResult.StdOut, StringComparison.Ordinal);
    }

    /// <summary>Drift guard (extends blazor-wasm-template's, per S-I/threat matrix): every RID mapped
    /// in this template's own <c>Tailwind.targets</c> copy carries a real, non-placeholder SHA-256.</summary>
    [Fact]
    public void TailwindTargets_EveryMappedRid_HasNonPlaceholderChecksum()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        var targetsPath = Path.Combine(
            templatesRoot,
            "blazor",
            "server",
            "build",
            "Tailwind.targets"
        );
        Assert.True(File.Exists(targetsPath), $"Expected {targetsPath} to exist.");

        var contents = File.ReadAllText(targetsPath);
        var assetNameMatches = Regex.Matches(
            contents,
            @"<TailwindAssetName Condition=""'\$\(TailwindRid\)' == '([^']+)'"""
        );
        Assert.NotEmpty(assetNameMatches);

        foreach (Match match in assetNameMatches)
        {
            var rid = match.Groups[1].Value;
            var shaMatch = Regex.Match(
                contents,
                $@"<TailwindSha256 Condition=""'\$\(TailwindRid\)' == '{Regex.Escape(rid)}'""\s*>\s*([0-9a-fA-F]+)\s*</TailwindSha256>"
            );
            Assert.True(shaMatch.Success, $"Expected a TailwindSha256 entry for RID '{rid}'.");
            var hash = shaMatch.Groups[1].Value;
            Assert.Equal(64, hash.Length);
            Assert.False(
                hash.All(c => c == '0'),
                $"RID '{rid}' has a placeholder (all-zero) checksum."
            );
        }
    }

    /// <summary>No template file may hardcode a raw Tailwind palette class; theming flows through <c>--ui-*</c> tokens only.</summary>
    [Fact]
    public void TemplateFiles_ContainNoRawTailwindPaletteClass()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        var serverRoot = Path.Combine(templatesRoot, "blazor", "server");
        Assert.True(Directory.Exists(serverRoot));

        var paletteClassPattern = new Regex(
            @"\b(?:bg|text|border|ring|from|via|to|fill|stroke|divide|outline|decoration|caret|accent)-(?:slate|rose)-\d{2,3}\b"
        );
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".razor",
            ".css",
            ".cs",
            ".html",
        };

        var offendingFiles = new List<string>();
        foreach (var file in Directory.EnumerateFiles(serverRoot, "*", SearchOption.AllDirectories))
        {
            if (!extensions.Contains(Path.GetExtension(file)))
                continue;

            var text = File.ReadAllText(file);
            if (paletteClassPattern.IsMatch(text))
            {
                offendingFiles.Add(Path.GetRelativePath(serverRoot, file));
            }
        }

        Assert.Empty(offendingFiles);
    }

    private static string ResolveRealPath(string path)
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(path);
            return Directory.GetCurrentDirectory();
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    private static string ResolveWebCsprojPath()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        return Path.Combine(
            templatesRoot,
            "blazor",
            "server",
            "src",
            "CleanArchBlazorServer.Web",
            "CleanArchBlazorServer.Web.csproj"
        );
    }

    /// <summary>
    /// Restores before building to avoid MSBuild restore races; retries the known race signature and uses the local feed for generated projects.
    /// </summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotnetBuildAsync(
        string solutionPath,
        string toolsHome
    )
    {
        var environment = new Dictionary<string, string?>
        {
            [DornToolsHomeEnvironmentVariableName] = toolsHome,
        };

        var restoreResult = await RestoreWithRetryAsync(solutionPath, environment);
        if (restoreResult.ExitCode != 0)
        {
            return restoreResult;
        }

        return await RunProcessAsync(
            Path.GetDirectoryName(solutionPath)!,
            environment,
            "build",
            solutionPath,
            "-c",
            "Release",
            "--no-restore",
            "-nodeReuse:false"
        );
    }

    /// <summary>
    /// Retries restore only for the known concurrent generated-file race; other failures return immediately.
    /// </summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RestoreWithRetryAsync(
        string solutionPath,
        Dictionary<string, string?> environment,
        int maxAttempts = 3
    )
    {
        var localNuGetFeed = ResolveLocalNuGetFeed();
        (int ExitCode, string StdOut, string StdErr) result = (1, "", "");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            result = await RunProcessAsync(
                Path.GetDirectoryName(solutionPath)!,
                environment,
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
    /// Resolves dorn's local NuGet feed (./artifacts) via env var or directory walk
    /// fallback — same pattern as TemplateLocator.ResolveTemplatesRoot.
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
        string workingDirectory,
        Dictionary<string, string?> environment,
        params string[] arguments
    )
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
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
