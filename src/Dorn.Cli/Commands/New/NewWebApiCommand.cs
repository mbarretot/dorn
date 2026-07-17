using Dorn.Abstractions.Generation;
using Dorn.Cli.Execution;
using Dorn.Core.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.New;

public sealed class NewWebApiCommand(
    IGenerationEngine generationEngine,
    IProcessRunner processRunner,
    IAnsiConsole console
) : AsyncCommand<NewWebApiSettings>
{
    private const string TemplateShortName = "dorn-webapi";

    private readonly IGenerationEngine _generationEngine = generationEngine;
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly IAnsiConsole _console = console;

    // Note: Spectre.Console.Cli 0.55.0 changed this virtual method from `public` to
    // `protected` (and added the CancellationToken parameter). Since C# forbids widening
    // visibility on override, the actual logic lives in the public `RunAsync` method
    // below; the framework's protected override just delegates to it. Unit tests call
    // `RunAsync` directly to avoid invoking the command through the full CommandApp
    // pipeline (CommandAppTester was removed in 0.55.0).
    protected override Task<int> ExecuteAsync(
        CommandContext context,
        NewWebApiSettings settings,
        CancellationToken cancellationToken
    ) => RunAsync(settings, cancellationToken);

    /// <summary>
    /// Runs the new webapi command logic. Public so unit tests can drive the command
    /// directly without going through the Spectre.Console.Cli command pipeline.
    /// </summary>
    public async Task<int> RunAsync(NewWebApiSettings settings, CancellationToken cancellationToken)
    {
        var validation = ProjectNameValidator.Validate(settings.Name);
        if (!validation.IsValid)
        {
            WriteErrorPanel("Invalid project name", validation.ErrorMessage);
            return 1;
        }

        var ormValidation = OrmValidator.Validate(settings.Orm);
        if (!ormValidation.IsValid)
        {
            WriteErrorPanel("Invalid ORM", ormValidation.ErrorMessage);
            return 1;
        }

        var databaseValidation = DatabaseProviderValidator.Validate(settings.Database);
        if (!databaseValidation.IsValid)
        {
            WriteErrorPanel("Invalid database provider", databaseValidation.ErrorMessage);
            return 1;
        }

        var orchestratorValidation = OrchestratorValidator.Validate(settings.Orchestrator);
        if (!orchestratorValidation.IsValid)
        {
            WriteErrorPanel("Invalid orchestrator", orchestratorValidation.ErrorMessage);
            return 1;
        }

        var outputDirectory = Path.GetFullPath(settings.Output ?? Path.Combine(".", settings.Name));

        var orm =
            settings.Orm?.ToLowerInvariant()
            ?? (
                _console.Profile.Capabilities.Interactive
                    ? _console.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select an [green]ORM[/]:")
                            .AddChoices("efcore", "dapper")
                            .UseConverter(o => o == "dapper" ? "Dapper" : "Entity Framework Core")
                    )
                    : "efcore"
            );

        var databaseProvider =
            settings.Database?.ToLowerInvariant()
            ?? (
                _console.Profile.Capabilities.Interactive
                    ? _console.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select a [green]database provider[/]:")
                            .AddChoices("sqlite", "sqlserver")
                    )
                    : "sqlite"
            );

        var orchestrator =
            settings.Orchestrator?.ToLowerInvariant()
            ?? (
                _console.Profile.Capabilities.Interactive
                    ? _console.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select an [green]orchestrator[/]:")
                            .AddChoices("aspire", "docker-compose")
                            .UseConverter(o => o == "docker-compose" ? "Docker Compose" : "Aspire")
                    )
                    : "aspire"
            );

        if (orchestrator == "aspire" && databaseProvider == "sqlserver")
        {
            var aspireNameValidation = AspireResourceNameValidator.Validate(settings.Name);
            if (!aspireNameValidation.IsValid)
            {
                WriteErrorPanel("Invalid project name", aspireNameValidation.ErrorMessage);
                return 1;
            }
        }

        var request = new GenerationRequest(
            TemplateShortName: TemplateShortName,
            ProjectName: settings.Name,
            OutputDirectory: outputDirectory,
            Parameters: new Dictionary<string, string>
            {
                ["Orm"] = orm,
                ["DatabaseProvider"] = databaseProvider,
                ["Orchestrator"] = orchestrator,
            },
            Force: settings.Force
        );

        var result = await _generationEngine.GenerateAsync(request);

        if (!result.Success)
        {
            // Panel content is parsed as Spectre markup, so escape everything that
            // isn't a literal we wrote ourselves (diagnostic messages come from the
            // Template Engine and may legitimately contain "[" / "]").
            var diagnosticsText =
                result.Diagnostics.Count > 0
                    ? string.Join(
                        Environment.NewLine,
                        result.Diagnostics.Select(d => Markup.Escape($"[{d.Severity}] {d.Message}"))
                    )
                    : "Template generation failed for an unknown reason.";

            WriteErrorPanel(
                $"Failed to generate '{settings.Name}'",
                diagnosticsText,
                escapeMessage: false
            );
            return 1;
        }

        await TryRestoreLocalToolsAsync(outputDirectory, settings.NoRestore, cancellationToken);

        RenderSuccess(settings.Name, result);
        return 0;
    }

    private async Task TryRestoreLocalToolsAsync(
        string outputDirectory,
        bool noRestore,
        CancellationToken cancellationToken
    )
    {
        if (noRestore)
        {
            _console.MarkupLine("[grey]--no-restore set: skipping `dotnet tool restore`.[/]");
            return;
        }

        var manifestPath = Path.Combine(outputDirectory, ".config", "dotnet-tools.json");
        if (!File.Exists(manifestPath))
        {
            // No local manifest -> nothing to restore.
            return;
        }

        _console.MarkupLine("[grey]Restoring local tools (dotnet tool restore)...[/]");

        try
        {
            var exitCode = await _processRunner.RunAsync(
                new ProcessSpec("dotnet", ["tool", "restore"], outputDirectory),
                cancellationToken
            );

            if (exitCode != 0)
            {
                _console.MarkupLine(
                    "[yellow]Warning:[/] `dotnet tool restore` failed (exit "
                        + exitCode
                        + "). The generated project is on disk, but local tools may not be available. Run `dotnet tool restore` manually inside the project to fix."
                );
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine(
                "[yellow]Warning:[/] `dotnet tool restore` threw: " + Markup.Escape(ex.Message)
            );
        }
    }

    private void WriteErrorPanel(string header, string? message, bool escapeMessage = true)
    {
        var content = message ?? "An unknown error occurred.";
        _console.Write(
            new Panel(escapeMessage ? Markup.Escape(content) : content)
                .Header(Markup.Escape(header))
                .BorderColor(Color.Red)
        );
    }

    private void RenderSuccess(string projectName, GenerationResult result)
    {
        if (result.CreatedFiles.Count > 0)
        {
            var table = new Table().Border(TableBorder.Rounded).Title("Created files");
            table.AddColumn("Path");
            foreach (var file in result.CreatedFiles)
            {
                table.AddRow(Markup.Escape(Path.GetRelativePath(result.OutputDirectory, file)));
            }

            _console.Write(table);
        }

        var nextSteps = Markup.Escape(
            $"cd {projectName}{Environment.NewLine}dotnet build{Environment.NewLine}dotnet dorn test"
        );
        _console.Write(new Panel(nextSteps).Header("Next steps").BorderColor(Color.Green));
    }
}
