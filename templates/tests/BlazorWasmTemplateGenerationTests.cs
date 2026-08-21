using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dorn.Abstractions.Generation;
using Dorn.Core.DependencyInjection;
using Dorn.Core.Templating;
using Dorn.Core.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TemplateGenerationTests;

/// <summary>
/// Phase 1 go/no-go: proves the Tailwind standalone CLI can be acquired, checksum-verified,
/// and invoked from MSBuild to produce real CSS output, on every CI runner. Threat-matrix
/// cases build <c>CleanArchBlazorWasm.Web.csproj</c> directly (not through the generation
/// engine) since they exercise <c>build/Tailwind.targets</c> itself, not template substitution.
/// </summary>
[Trait("Category", "Integration")]
public class BlazorWasmTemplateGenerationTests
{
    private const string LocalNuGetFeedEnvironmentVariableName = "DORN_LOCAL_NUGET_FEED";
    private const string DornToolsHomeEnvironmentVariableName = "DORN_TOOLS_HOME";
    private const string DornTailwindPathEnvironmentVariableName = "DORN_TAILWIND_PATH";

    /// <summary>
    /// On macOS, <see cref="Path.GetTempPath"/> returns a path through the <c>/var</c> -&gt;
    /// <c>/private/var</c> symlink. Passing that symlinked absolute path as a nested
    /// <c>dotnet build</c> project argument makes the Razor component source generator resolve
    /// it to the canonical <c>/private/...</c> form while restore artifacts stay keyed to the
    /// symlinked form, breaking folder-based namespace computation for <c>Components/**</c>
    /// (manifests as a spurious CS0234 on generated <c>_Imports.razor</c>). Resolving once via
    /// an actual chdir/getcwd round-trip keeps every path used by this test canonical. Other
    /// generation tests (webapi/grpc/worker) never observed this because their SDKs have no
    /// path-sensitive Razor codegen step.
    /// </summary>
    private static readonly string RealTempRoot = ResolveRealPath(Path.GetTempPath());

    [Fact]
    public async Task GenerateAndBuild_DornBlazorWasmTemplate_ProducesRealTailwindCss()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-wasm-{Guid.NewGuid():N}"
        );
        var toolsHome = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-wasm-tools-{Guid.NewGuid():N}"
        );
        try
        {
            var request = new GenerationRequest(
                "dorn-blazor-wasm",
                "DornIntegrationTestBlazorWasmApp",
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
            Assert.Equal("DornIntegrationTestBlazorWasmApp.slnx", Path.GetFileName(slnFiles[0]));

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
                "DornIntegrationTestBlazorWasmApp.Web",
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
                    await DeleteDirectoryWithRetryAsync(outputDirectory);
                }
                if (Directory.Exists(toolsHome))
                {
                    await DeleteDirectoryWithRetryAsync(toolsHome);
                }
            }
            else
            {
                Console.WriteLine("KEPT: " + outputDirectory);
            }
        }
    }

    /// <summary>
    /// Phase 2 go/no-go for the theming slice: <c>--theme rose</c> must replace the single
    /// literal <c>DEFAULT_THEME</c> seed in <c>theme-boot.js</c> (design B6) and must not
    /// corrupt <c>Styles/themes/slate.css</c>'s own <c>data-ui-theme='slate'</c> selector,
    /// proving the single-quote convention keeps the two from colliding.
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
            $"dorn-tests-blazor-wasm-theme-{Guid.NewGuid():N}"
        );
        try
        {
            var request = new GenerationRequest(
                "dorn-blazor-wasm",
                "DornThemeRoseApp",
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
                "DornThemeRoseApp.Web",
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
                "DornThemeRoseApp.Web",
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
                await DeleteDirectoryWithRetryAsync(outputDirectory);
            }
        }
    }

    /// <summary>
    /// C4 go/no-go for the 4 new themes: each of <c>--theme neutral|linear|primer|lightning</c>
    /// must replace <c>theme-boot.js</c>'s literal (mirrors <c>GenerateWithThemeRose...</c> above)
    /// and must not corrupt the other 5 themes' own <c>data-ui-theme</c> selectors.
    /// </summary>
    [Theory]
    [InlineData("neutral")]
    [InlineData("linear")]
    [InlineData("primer")]
    [InlineData("lightning")]
    public async Task GenerateWithNewTheme_ReplacesBootDefaultThemeLiteral_WithoutCorruptingOtherThemeCss(
        string theme
    )
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var projectName = $"DornTheme{theme}App";
        var outputDirectory = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-wasm-theme-{theme}-{Guid.NewGuid():N}"
        );
        try
        {
            var request = new GenerationRequest(
                "dorn-blazor-wasm",
                projectName,
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["Theme"] = theme }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );

            var webRoot = Path.Combine(outputDirectory, "src", $"{projectName}.Web");
            var themeBootPath = Path.Combine(webRoot, "wwwroot", "theme-boot.js");
            Assert.True(File.Exists(themeBootPath), $"Expected boot script at '{themeBootPath}'.");

            var themeBoot = await File.ReadAllTextAsync(themeBootPath);
            Assert.Contains($"DEFAULT_THEME = \"{theme}\"", themeBoot, StringComparison.Ordinal);
            Assert.DoesNotContain("DEFAULT_THEME = \"slate\"", themeBoot, StringComparison.Ordinal);

            foreach (var otherTheme in ThemeValidator.ValidThemes)
            {
                var otherThemeCssPath = Path.Combine(
                    webRoot,
                    "Styles",
                    "themes",
                    $"{otherTheme}.css"
                );
                Assert.True(
                    File.Exists(otherThemeCssPath),
                    $"Expected theme stylesheet at '{otherThemeCssPath}'."
                );
                var otherThemeCss = await File.ReadAllTextAsync(otherThemeCssPath);
                Assert.Contains(
                    $"[data-ui-theme='{otherTheme}']",
                    otherThemeCss,
                    StringComparison.Ordinal
                );
            }
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                await DeleteDirectoryWithRetryAsync(outputDirectory);
            }
        }
    }

    /// <summary>
    /// Drift guard (C4.5): <c>template.json</c>'s <c>Theme</c> choices must exactly match
    /// <c>ThemeValidator.ValidThemes</c> so the CLI and the generator manifest can never diverge.
    /// </summary>
    [Fact]
    public void TemplateJsonThemeChoices_ExactlyMatch_ThemeValidatorAllowList()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        var templateJsonPath = Path.Combine(
            templatesRoot,
            "blazor",
            "wasm",
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

    /// <summary>
    /// Phase 7 go/no-go for the playground toggle (design E2): <c>IncludePlayground=false</c>
    /// must exclude <c>Features/Playground/**</c> entirely, rename
    /// <c>NavMenu.Lean.razor</c> (not <c>NavMenu.Playground.razor</c>) to the canonical
    /// <c>NavMenu.razor</c>, and still build cleanly — proving the lean project is a real,
    /// buildable output, not just an excluded-files assertion.
    /// </summary>
    [Fact]
    public async Task GenerateWithIncludePlaygroundFalse_ExcludesPlaygroundAndRenamesLeanNavMenu_AndBuilds()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-wasm-lean-{Guid.NewGuid():N}"
        );
        var toolsHome = Path.Combine(
            RealTempRoot,
            $"dorn-tests-blazor-wasm-lean-tools-{Guid.NewGuid():N}"
        );
        try
        {
            var request = new GenerationRequest(
                "dorn-blazor-wasm",
                "DornLeanBlazorWasmApp",
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["IncludePlayground"] = "false" }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );

            var webRoot = Path.Combine(outputDirectory, "src", "DornLeanBlazorWasmApp.Web");
            var playgroundDir = Path.Combine(webRoot, "Features", "Playground");
            Assert.False(
                Directory.Exists(playgroundDir),
                $"Expected no playground directory at '{playgroundDir}'."
            );

            var layoutDir = Path.Combine(webRoot, "Components", "Layout");
            var navMenuPath = Path.Combine(layoutDir, "NavMenu.razor");
            Assert.True(File.Exists(navMenuPath), $"Expected renamed NavMenu at '{navMenuPath}'.");
            Assert.False(
                File.Exists(Path.Combine(layoutDir, "NavMenu.Lean.razor")),
                "NavMenu.Lean.razor must be renamed away, not left alongside NavMenu.razor."
            );
            Assert.False(
                File.Exists(Path.Combine(layoutDir, "NavMenu.Playground.razor")),
                "NavMenu.Playground.razor must be excluded when IncludePlayground=false."
            );

            var navMenu = await File.ReadAllTextAsync(navMenuPath);
            Assert.DoesNotContain("/playground", navMenu, StringComparison.Ordinal);

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);

            var buildResult = await RunDotnetBuildAsync(slnFiles[0], toolsHome);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
            Assert.DoesNotContain("RZ10012", buildResult.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("DORN_TEST_KEEP_TEMP") != "true")
            {
                if (Directory.Exists(outputDirectory))
                {
                    await DeleteDirectoryWithRetryAsync(outputDirectory);
                }
                if (Directory.Exists(toolsHome))
                {
                    await DeleteDirectoryWithRetryAsync(toolsHome);
                }
            }
            else
            {
                Console.WriteLine("KEPT: " + outputDirectory);
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
            $"dorn-tests-blazor-wasm-tools-{Guid.NewGuid():N}"
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
                await DeleteDirectoryWithRetryAsync(toolsHome);
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
            $"dorn-tests-blazor-wasm-tools-{Guid.NewGuid():N}"
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
                await DeleteDirectoryWithRetryAsync(toolsHome);
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
            $"dorn-tests-blazor-wasm-missing-{Guid.NewGuid():N}.exe"
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

    /// <summary>Drift guard: every RID mapped in <c>Tailwind.targets</c> carries a real, non-placeholder SHA-256.</summary>
    [Fact]
    public void TailwindTargets_EveryMappedRid_HasNonPlaceholderChecksum()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        var targetsPath = Path.Combine(
            templatesRoot,
            "blazor",
            "wasm",
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
        var wasmRoot = Path.Combine(templatesRoot, "blazor", "wasm");
        Assert.True(Directory.Exists(wasmRoot));

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
        foreach (var file in Directory.EnumerateFiles(wasmRoot, "*", SearchOption.AllDirectories))
        {
            if (!extensions.Contains(Path.GetExtension(file)))
                continue;

            var text = File.ReadAllText(file);
            if (paletteClassPattern.IsMatch(text))
            {
                offendingFiles.Add(Path.GetRelativePath(wasmRoot, file));
            }
        }

        Assert.Empty(offendingFiles);
    }

    /// <summary>
    /// Resolves symlink components (e.g. macOS's <c>/var</c> -&gt; <c>/private/var</c>) via an
    /// actual chdir/getcwd round-trip, since <see cref="Path.GetFullPath(string)"/> never
    /// dereferences symlinks. No-op on platforms/paths with no symlinked ancestor.
    /// </summary>
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

    // Windows can briefly hold a handle on the just-exited Tailwind CLI process.
    private static async Task DeleteDirectoryWithRetryAsync(string path)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(200);
            }
        }
    }

    private static string ResolveWebCsprojPath()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        return Path.Combine(
            templatesRoot,
            "blazor",
            "wasm",
            "src",
            "CleanArchBlazorWasm.Web",
            "CleanArchBlazorWasm.Web.csproj"
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
