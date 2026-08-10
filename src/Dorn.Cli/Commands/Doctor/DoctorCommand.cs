using Dorn.Cli.Execution;
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

    private readonly ITemplatesRootLocator _templatesRootLocator;
    private readonly IProcessRunner _processRunner;
    private readonly IProjectContextResolver _resolver;
    private readonly IAnsiConsole _console;
    private readonly IDornTheme _theme;

    public DoctorCommand(
        ITemplatesRootLocator templatesRootLocator,
        IProcessRunner processRunner,
        IProjectContextResolver resolver,
        IAnsiConsole console,
        IDornTheme theme
    )
    {
        _templatesRootLocator = templatesRootLocator;
        _processRunner = processRunner;
        _resolver = resolver;
        _console = console;
        _theme = theme;
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
        var root = settings.Project ?? Directory.GetCurrentDirectory();

        var results = _theme.LiveRegionsEnabled
            ? await _theme
                .CreateStatus()
                .StartAsync(
                    "Checking environment...",
                    ctx => CollectChecksAsync(root, ctx, cancellationToken)
                )
            : await CollectChecksAsync(root, statusContext: null, cancellationToken);

        Render(results);

        return results.Any(r => r.Status == CheckStatus.Fail) ? 1 : 0;
    }

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

        var orchestrator = TryResolveOrchestrator(root);
        if (orchestrator == Orchestrator.Compose)
        {
            statusContext?.Status("Checking Docker...");
            results.Add(await CheckDockerAsync(ct));
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

    private Orchestrator? TryResolveOrchestrator(string root)
    {
        try
        {
            return _resolver.Resolve(root).Orchestrator;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A bad -p (e.g. missing path) must never break the environment-level mandatory
            // checks (D4) — degrade to "not Compose" so the Docker row is simply hidden.
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

    private enum CheckStatus
    {
        Pass,
        Fail,
        Warn,
    }

    private sealed record CheckResult(string Name, CheckStatus Status, string Detail);
}
