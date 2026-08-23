using System.Runtime.InteropServices;
using Dorn.Cli.Execution;
using Dorn.Cli.Output;
using Dorn.Cli.Projects;
using Dorn.Cli.Templating;
using Dorn.Cli.Theming;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Doctor;

/// <summary>
/// <c>dorn doctor</c> — read-only diagnostic checks reporting whether the local environment
/// is ready for scaffold/build/test/run workflows.
/// </summary>
public sealed class DoctorCommand : AsyncCommand<DoctorSettings>
{
    // Keep in sync with global.json and templates/webapi/global.json.
    internal const string MinimumSdkVersion = "10.0.301";

    // Keep in sync with mbarretot/dorn-templates-blazor's templates/blazor/wasm/build/Tailwind.targets' TailwindVersion.
    private const string TailwindVersion = "4.3.1";
    private const string TailwindPathEnvironmentVariable = "DORN_TAILWIND_PATH";
    private const string ToolsHomeEnvironmentVariable = "DORN_TOOLS_HOME";

    private readonly ITemplatesRootLocator _templatesRootLocator;
    private readonly IProcessRunner _processRunner;
    private readonly IProjectContextResolver _resolver;
    private readonly IAnsiConsole _console;
    private readonly IDornTheme _theme;
    private readonly ICliOutputWriter _writer;

    public DoctorCommand(
        ITemplatesRootLocator templatesRootLocator,
        IProcessRunner processRunner,
        IProjectContextResolver resolver,
        IAnsiConsole console,
        IDornTheme theme,
        ICliOutputWriter writer
    )
    {
        _templatesRootLocator = templatesRootLocator;
        _processRunner = processRunner;
        _resolver = resolver;
        _console = console;
        _theme = theme;
        _writer = writer;
    }

    // Spectre.Console.Cli 0.55.0 moved ExecuteAsync to protected; RunAsync below is the public
    // entry point tests call directly (CommandAppTester was removed in 0.55.0).
    protected override Task<int> ExecuteAsync(
        CommandContext context,
        DoctorSettings settings,
        CancellationToken cancellationToken
    ) => RunAsync(settings, cancellationToken);

    /// <summary>
    /// Runs the doctor command logic. Public so unit tests can drive the command directly
    /// without going through the Spectre.Console.Cli command pipeline.
    /// </summary>
    public async Task<int> RunAsync(DoctorSettings settings, CancellationToken cancellationToken)
    {
        var formatResult = OutputFormatValidator.Validate(settings.Format);
        if (!formatResult.IsValid)
        {
            _theme.Message(Severity.Error, Markup.Escape(formatResult.ErrorMessage!));
            return 1;
        }

        var format = formatResult.Format;
        var root = settings.Project ?? Directory.GetCurrentDirectory();

        // Live status region renders through IAnsiConsole; JSON mode must never touch it.
        var useLive = _theme.LiveRegionsEnabled && format == OutputFormat.Table;
        var results = useLive
            ? await _theme
                .CreateStatus()
                .StartAsync(
                    "Checking environment...",
                    ctx => CollectChecksAsync(root, ctx, cancellationToken)
                )
            : await CollectChecksAsync(root, statusContext: null, cancellationToken);

        var exitCode = results.Any(r => r.Status == CheckStatus.Fail) ? 1 : 0;

        if (format == OutputFormat.Json)
        {
            EmitJson(results, exitCode);
        }
        else
        {
            Render(results);
        }

        return exitCode;
    }

    private void EmitJson(IReadOnlyList<CheckResult> results, int exitCode)
    {
        var report = new DoctorReport(
            results
                .Select(r => new DoctorCheckDto(r.Name, StatusToken(r.Status), r.Detail))
                .ToList()
        );
        var envelope = new CliEnvelope<DoctorReport>(
            SchemaVersion: 1,
            Command: "doctor",
            Success: exitCode == 0,
            ExitCode: exitCode,
            Data: report
        );
        _writer.WriteLine(CliJson.Serialize(envelope));
    }

    private static string StatusToken(CheckStatus status) =>
        status switch
        {
            CheckStatus.Pass => "pass",
            CheckStatus.Fail => "fail",
            CheckStatus.Warn => "warn",
            _ => status.ToString().ToLowerInvariant(),
        };

    private async Task<List<CheckResult>> CollectChecksAsync(
        string root,
        StatusContext? statusContext,
        CancellationToken ct
    )
    {
        statusContext?.Status("Checking templates root...");
        var results = new List<CheckResult> { CheckTemplatesRoot() };

        statusContext?.Status("Checking .NET SDK...");
        results.Add(await CheckDotnetSdkAsync(ct));

        var context = TryResolveProjectContext(root);

        if (context?.Orchestrator == Orchestrator.Compose)
        {
            statusContext?.Status("Checking Docker...");
            results.Add(await CheckDockerAsync(ct));
        }

        if (context?.TailwindProject is not null)
        {
            statusContext?.Status("Checking Tailwind CSS CLI...");
            results.Add(await CheckTailwindAsync(ct));
        }

        return results;
    }

    private CheckResult CheckTemplatesRoot()
    {
        try
        {
            var path = _templatesRootLocator.Resolve();
            return new CheckResult("Templates", CheckStatus.Pass, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new CheckResult(
                "Templates",
                CheckStatus.Fail,
                $"{ex.Message} Set DORN_TEMPLATES_PATH to the templates directory, or reinstall the dorn tool."
            );
        }
    }

    private async Task<CheckResult> CheckDotnetSdkAsync(CancellationToken ct)
    {
        var result = await _processRunner.RunCapturedAsync(
            new ProcessSpec("dotnet", ["--version"]),
            ct
        );

        if (result.ExitCode == 127)
        {
            return new CheckResult(".NET SDK", CheckStatus.Fail, "dotnet was not found on PATH");
        }

        if (!TryParseSdkVersion(result.StandardOutput, out var installed))
        {
            return new CheckResult(
                ".NET SDK",
                CheckStatus.Fail,
                $"Could not parse dotnet version from '{result.StandardOutput.Trim()}'"
            );
        }

        var minimum = new Version(MinimumSdkVersion);
        if (installed >= minimum)
        {
            return new CheckResult(
                ".NET SDK",
                CheckStatus.Pass,
                $"{installed} (minimum {MinimumSdkVersion})"
            );
        }

        return new CheckResult(
            ".NET SDK",
            CheckStatus.Fail,
            $"{installed} found, {MinimumSdkVersion} or newer required"
        );
    }

    private ProjectContext? TryResolveProjectContext(string root)
    {
        try
        {
            return _resolver.Resolve(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A bad -p (e.g. missing path) must never break the environment-level mandatory
            // checks (D4) — degrade to "no signal" so the Docker/Tailwind rows are simply hidden.
            return null;
        }
    }

    private async Task<CheckResult> CheckDockerAsync(CancellationToken ct)
    {
        var result = await _processRunner.RunCapturedAsync(
            new ProcessSpec("docker", ["--version"]),
            ct
        );

        if (result.ExitCode == 0)
        {
            return new CheckResult(
                "Docker",
                CheckStatus.Pass,
                $"{result.StandardOutput.Trim()} (CLI only — daemon status not checked)"
            );
        }

        return new CheckResult(
            "Docker",
            CheckStatus.Warn,
            "Docker not found. Needed for Compose orchestration and non-sqlite integration tests."
        );
    }

    // Mirrors build/Tailwind.targets' resolution order; never Fail (a broken pipeline still builds green).
    private async Task<CheckResult> CheckTailwindAsync(CancellationToken ct)
    {
        var overridePath = Environment.GetEnvironmentVariable(TailwindPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return File.Exists(overridePath)
                ? new CheckResult("Tailwind CSS CLI", CheckStatus.Pass, overridePath)
                : new CheckResult(
                    "Tailwind CSS CLI",
                    CheckStatus.Warn,
                    $"{TailwindPathEnvironmentVariable} is set to '{overridePath}' but no file exists there."
                );
        }

        var cachedPath = ResolveCachedTailwindPath();
        if (cachedPath is not null && File.Exists(cachedPath))
        {
            return new CheckResult("Tailwind CSS CLI", CheckStatus.Pass, cachedPath);
        }

        var result = await _processRunner.RunCapturedAsync(
            new ProcessSpec("tailwindcss", ["--help"]),
            ct
        );

        if (result.ExitCode == 0)
        {
            var firstLine = result.StandardOutput.Split('\n')[0].Trim();
            return new CheckResult("Tailwind CSS CLI", CheckStatus.Pass, firstLine);
        }

        return new CheckResult(
            "Tailwind CSS CLI",
            CheckStatus.Warn,
            "Tailwind CSS CLI not found. The build downloads a pinned copy on first build; set DORN_TAILWIND_PATH to use a local binary or build offline."
        );
    }

    private static string? ResolveCachedTailwindPath()
    {
        var rid = ResolveCachedTailwindRid();
        if (rid is null)
            return null;

        var exeName = rid == "windows-x64" ? "tailwindcss.exe" : "tailwindcss";
        return Path.Combine(ResolveDornToolsHome(), "tailwindcss", TailwindVersion, rid, exeName);
    }

    private static string ResolveDornToolsHome()
    {
        var overrideHome = Environment.GetEnvironmentVariable(ToolsHomeEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideHome))
            return overrideHome;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".dorn", "tools");
    }

    // Best-effort subset of Tailwind.targets' full RID map — the build is the authoritative gate.
    internal static string? ResolveCachedTailwindRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.OSArchitecture == Architecture.X64 ? "windows-x64" : null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "macos-arm64",
                Architecture.X64 => "macos-x64",
                _ => null,
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "linux-arm64",
                Architecture.X64 => "linux-x64",
                _ => null,
            };
        }

        return null;
    }

    /// <summary>
    /// Parses a <c>dotnet --version</c> style string (e.g. "10.0.301" or
    /// "10.0.100-preview.5.25277.114") by taking the first line and stripping any
    /// prerelease suffix at the first '-' before handing off to <see cref="Version.TryParse"/>.
    /// </summary>
    internal static bool TryParseSdkVersion(string raw, out Version version)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        var firstLine = trimmed.Split('\n')[0].Trim();
        var dashIndex = firstLine.IndexOf('-');
        var core = dashIndex >= 0 ? firstLine[..dashIndex] : firstLine;

        if (Version.TryParse(core, out var parsed))
        {
            version = parsed;
            return true;
        }

        version = new Version(0, 0);
        return false;
    }

    private void Render(IReadOnlyList<CheckResult> results)
    {
        var table = _theme.CreateTable("Environment checks");
        table.AddColumn("Check");
        table.AddColumn("Status");
        table.AddColumn("Detail");

        foreach (var r in results)
        {
            table.AddRow(Markup.Escape(r.Name), StatusLabel(r.Status), Markup.Escape(r.Detail));
        }

        _console.Write(table);
    }

    private string StatusLabel(CheckStatus status) =>
        status switch
        {
            CheckStatus.Pass => _theme.Label(Severity.Success, "PASS"),
            CheckStatus.Fail => _theme.Label(Severity.Error, "FAIL"),
            CheckStatus.Warn => _theme.Label(Severity.Warning, "WARN"),
            _ => status.ToString(),
        };
}
