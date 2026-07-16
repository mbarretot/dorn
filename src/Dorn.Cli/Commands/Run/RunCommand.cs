using System.Runtime.InteropServices;
using Dorn.Cli.Execution;
using Dorn.Cli.Projects;
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
    private readonly IAnsiConsole _console;

    public RunCommand(
        IProjectContextResolver resolver,
        IProcessRunner processRunner,
        ISignalRegistration signalRegistration,
        IAnsiConsole console
    )
    {
        _resolver = resolver;
        _processRunner = processRunner;
        _signalRegistration = signalRegistration;
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RunSettings settings)
    {
        var root = settings.Project ?? Directory.GetCurrentDirectory();
        var projectContext = _resolver.Resolve(root);

        return projectContext.Orchestrator switch
        {
            Orchestrator.Aspire => await RunAspire(projectContext),
            Orchestrator.Compose => await RunCompose(projectContext),
            Orchestrator.Plain => await RunPlain(projectContext),
            _ => throw new InvalidOperationException(
                $"Unknown orchestrator: {projectContext.Orchestrator}"
            ),
        };
    }

    private async Task<int> RunAspire(ProjectContext ctx)
    {
        var appHost = ResolveAppHost(ctx);
        if (string.IsNullOrEmpty(appHost))
        {
            _console.MarkupLine(
                "[red]Aspire orchestrator detected, but no AppHost project was found.[/]"
            );
            return 1;
        }

        _console.MarkupLine($"[green]Starting[/] via Aspire AppHost: {Markup.Escape(appHost)}");

        var spec = new ProcessSpec("dotnet", ["run", "--project", appHost], ctx.Root);
        var exitCode = await _processRunner.RunAsync(spec, CancellationToken.None);
        return exitCode;
    }

    private async Task<int> RunCompose(ProjectContext ctx)
    {
        _console.MarkupLine("[green]Starting[/] via Docker Compose");

        // SIGINT/SIGTERM teardown — only the Compose path gets this treatment.
        // Aspire and plain dotnet run self-handle shutdown via their own hosts.
        using var sigintReg = _signalRegistration.Register(
            PosixSignal.SIGINT,
            _ => OnCancel(ctx, "SIGINT")
        );
        using var sigtermReg = _signalRegistration.Register(
            PosixSignal.SIGTERM,
            _ => OnCancel(ctx, "SIGTERM")
        );

        var spec = new ProcessSpec("docker", ["compose", "up"], ctx.Root);
        var exitCode = await _processRunner.RunAsync(spec, CancellationToken.None);

        // Compose child exited cleanly — defensive down in case any orphan remains.
        await TeardownCompose(ctx);

        return exitCode;
    }

    private async Task<int> RunPlain(ProjectContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.WebApiProject))
        {
            _console.MarkupLine("[red]Plain orchestrator requires a WebApi project under src/.[/]");
            return 1;
        }

        _console.MarkupLine($"[green]Starting[/] plain: {Markup.Escape(ctx.WebApiProject)}");

        var spec = new ProcessSpec("dotnet", ["run", "--project", ctx.WebApiProject], ctx.Root);
        var exitCode = await _processRunner.RunAsync(spec, CancellationToken.None);
        return exitCode;
    }

    private void OnCancel(ProjectContext ctx, string signalName)
    {
        _console.MarkupLine($"[yellow]{signalName} received.[/] Tearing down compose stack...");
        // Best-effort synchronous shutdown — RunAsync is blocking at this point.
        try
        {
            var downSpec = new ProcessSpec("docker", ["compose", "down"], ctx.Root);
            _processRunner.RunAsync(downSpec, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Teardown failed:[/] {Markup.Escape(ex.Message)}");
        }
    }

    private async Task TeardownCompose(ProjectContext ctx)
    {
        try
        {
            var downSpec = new ProcessSpec("docker", ["compose", "down"], ctx.Root);
            await _processRunner.RunAsync(downSpec, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Teardown failed:[/] {Markup.Escape(ex.Message)}");
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
