using Dorn.Abstractions.Generation;
using Dorn.Core.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.New;

public sealed class NewWebApiCommand(IGenerationEngine generationEngine, IAnsiConsole console)
    : AsyncCommand<NewWebApiSettings>
{
    private const string TemplateShortName = "dorn-webapi";

    private readonly IGenerationEngine _generationEngine = generationEngine;
    private readonly IAnsiConsole _console = console;

    public override async Task<int> ExecuteAsync(CommandContext context, NewWebApiSettings settings)
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

        RenderSuccess(settings.Name, result);
        return 0;
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

        var nextSteps = Markup.Escape($"cd {projectName}{Environment.NewLine}dotnet build");
        _console.Write(new Panel(nextSteps).Header("Next steps").BorderColor(Color.Green));
    }
}
