using System.Runtime.InteropServices;
using Dorn.Cli.Execution;
using Dorn.Cli.Projects;
using Dorn.Cli.Theming;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Run;

/// <summary>
/// <c>dorn run</c> — runs the generated webapi project using the orchestrator detected
/// from file presence (AppHost &gt; Compose &gt; Plain).
/// </summary>
public sealed class RunCommand : AsyncCommand<RunSettings>
{
    private readonly IProjectContextResolver _resolver;
    private readonly IProcessRunner _processRunner;
    private readonly ISignalRegistration _signalRegistration;
    private readonly IDornTheme _theme;

    public RunCommand(
        IProjectContextResolver resolver,
        IProcessRunner processRunner,
        ISignalRegistration signalRegistration,
        IDornTheme theme
    )
    {
        _resolver = resolver;
        _processRunner = processRunner;
        _signalRegistration = signalRegistration;
        _theme = theme;
    }

    // Spectre.Console.Cli 0.55.0 changed ExecuteAsync from public to protected (and added
    // CancellationToken); logic lives in the public RunAsync below, and tests call it
    // directly (CommandAppTester was removed in 0.55.0).
    protected override Task<int> ExecuteAsync(
        CommandContext context,
        RunSettings settings,
        CancellationToken cancellationToken
    ) => RunAsync(settings, cancellationToken);

    /// <summary>
    /// Runs the run command logic. Public so unit tests can drive the command directly
    /// without going through the Spectre.Console.Cli command pipeline.
    /// </summary>
    public async Task<int> RunAsync(RunSettings settings, CancellationToken cancellationToken)
    {
        var root = settings.Project ?? Directory.GetCurrentDirectory();
        var projectContext = _resolver.Resolve(root);

        return projectContext.Orchestrator switch
        {
            Orchestrator.Aspire => await RunAspire(projectContext, cancellationToken),
            Orchestrator.Compose => await RunCompose(projectContext, cancellationToken),
            Orchestrator.Plain => await RunPlain(projectContext, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unknown orchestrator: {projectContext.Orchestrator}"
            ),
        };
    }

    private async Task<int> RunAspire(ProjectContext ctx, CancellationToken cancellationToken)
    {
        var appHost = ResolveAppHost(ctx);
        if (string.IsNullOrEmpty(appHost))
        {
            _theme.Message(
                Severity.Error,
                "Aspire orchestrator detected, but no AppHost project was found."
            );
            return 1;
        }

        _theme.Message(Severity.Success, $"Starting via Aspire AppHost: {Markup.Escape(appHost)}");

        var spec = new ProcessSpec("dotnet", ["run", "--project", appHost], ctx.Root);
        var exitCode = await _processRunner.RunAsync(spec, cancellationToken);
        return exitCode;
    }

    private async Task<int> RunCompose(ProjectContext ctx, CancellationToken cancellationToken)
    {
        _theme.Message(Severity.Success, "Starting via Docker Compose");

        // SIGINT/SIGTERM teardown — only the Compose path gets this treatment.
        // Aspire and plain dotnet run self-handle shutdown via their own hosts.
        // Signal handlers run on a thread-pool thread, so they cannot capture the
        // async cancellationToken — they fire a best-effort teardown independently.
        using var sigintReg = _signalRegistration.Register(
            PosixSignal.SIGINT,
            _ => OnCancel(ctx, "SIGINT")
        );
        using var sigtermReg = _signalRegistration.Register(
            PosixSignal.SIGTERM,
            _ => OnCancel(ctx, "SIGTERM")
        );

        var spec = new ProcessSpec("docker", ["compose", "up"], ctx.Root);
        var exitCode = await _processRunner.RunAsync(spec, cancellationToken);

        // Compose child exited cleanly — defensive down in case any orphan remains.
        await TeardownCompose(ctx, cancellationToken);

        return exitCode;
    }

    private async Task<int> RunPlain(ProjectContext ctx, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(ctx.WebApiProject))
        {
            _theme.Message(Severity.Error, "Plain orchestrator requires a WebApi project under src/.");
            return 1;
        }

        _theme.Message(Severity.Success, $"Starting plain: {Markup.Escape(ctx.WebApiProject)}");

        var spec = new ProcessSpec("dotnet", ["run", "--project", ctx.WebApiProject], ctx.Root);
        var exitCode = await _processRunner.RunAsync(spec, cancellationToken);
        return exitCode;
    }

    private void OnCancel(ProjectContext ctx, string signalName)
    {
        _theme.Message(Severity.Warning, $"{signalName} received. Tearing down compose stack...");
        // Best-effort synchronous shutdown — RunAsync is blocking at this point.
        // Signal handlers run on a thread-pool thread and cannot propagate the
        // async cancellationToken; CancellationToken.None is correct here.
        try
        {
            var downSpec = new ProcessSpec("docker", ["compose", "down"], ctx.Root);
            _processRunner.RunAsync(downSpec, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _theme.Message(Severity.Error, $"Teardown failed: {Markup.Escape(ex.Message)}");
        }
    }

    private async Task TeardownCompose(ProjectContext ctx, CancellationToken cancellationToken)
    {
        try
        {
            var downSpec = new ProcessSpec("docker", ["compose", "down"], ctx.Root);
            await _processRunner.RunAsync(downSpec, cancellationToken);
        }
        catch (Exception ex)
        {
            _theme.Message(Severity.Error, $"Teardown failed: {Markup.Escape(ex.Message)}");
        }
    }

    private static string? ResolveAppHost(ProjectContext ctx)
    {
        var srcDir = Path.Combine(ctx.Root, "src");
        if (!Directory.Exists(srcDir))
            return null;

        return Directory
            .EnumerateDirectories(srcDir, "*")
            .FirstOrDefault(d =>
                Path.GetFileName(d).EndsWith(".AppHost", StringComparison.OrdinalIgnoreCase)
            );
    }
}
