using Dorn.Abstractions.Generation;
using Dorn.Cli.Execution;
using Dorn.Cli.Theming;
using Dorn.Core.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.New;

public sealed class NewWebApiCommand(
    IGenerationEngine generationEngine,
    IProcessRunner processRunner,
    IAnsiConsole console,
    IDornTheme theme
) : AsyncCommand<NewWebApiSettings>
{
    private const string TemplateShortName = "dorn-webapi";

    private readonly IGenerationEngine _generationEngine = generationEngine;
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly IAnsiConsole _console = console;
    private readonly IDornTheme _theme = theme;

    // Spectre.Console.Cli 0.55.0 changed ExecuteAsync from public to protected (and added
    // CancellationToken); logic lives in the public RunAsync below, and tests call it
    // directly (CommandAppTester was removed in 0.55.0).
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
        string name;
        if (!string.IsNullOrWhiteSpace(settings.Name))
        {
            var validation = ProjectNameValidator.Validate(settings.Name);
            if (!validation.IsValid)
            {
                WriteErrorPanel("Invalid project name", validation.ErrorMessage);
                return 1;
            }

            name = settings.Name;
        }
        else if (_console.Profile.Capabilities.Interactive)
        {
            name = _console.Prompt(
                new TextPrompt<string>("Project name:").Validate(candidate =>
                {
                    var result = ProjectNameValidator.Validate(candidate);
                    return result.IsValid
                        ? ValidationResult.Success()
                        : ValidationResult.Error(result.ErrorMessage);
                })
            );
        }
        else
        {
            WriteErrorPanel(
                "Missing project name",
                "Project name is required. Pass it as an argument (dorn new webapi <name>) or run in an interactive terminal to be prompted."
            );
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

        var authValidation = AuthValidator.Validate(settings.Auth);
        if (!authValidation.IsValid)
        {
            WriteErrorPanel("Invalid auth mode", authValidation.ErrorMessage);
            return 1;
        }

        var outputDirectory = Path.GetFullPath(settings.Output ?? Path.Combine(".", name));

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
                            .AddChoices("sqlite", "sqlserver", "postgres")
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
                            .AddChoices("aspire", "docker-compose", "none")
                            .UseConverter(o =>
                                o switch
                                {
                                    "docker-compose" => "Docker Compose",
                                    "none" => "None (run directly)",
                                    _ => "Aspire",
                                }
                            )
                    )
                    : "aspire"
            );

        var auth =
            settings.Auth?.ToLowerInvariant()
            ?? (
                _console.Profile.Capabilities.Interactive
                    ? _console.Prompt(
                        new SelectionPrompt<string>()
                            .Title(
                                $"Select an [green]authentication scheme[/] (compatible with {orm}):"
                            )
                            .AddChoices(AuthChoiceProvider.ForOrm(orm))
                            .UseConverter(o =>
                                o switch
                                {
                                    "azure-ad" => "Azure AD (validate Entra ID tokens)",
                                    "custom" => "Custom JWT (self-issued, seeded user)",
                                    _ => "None",
                                }
                            )
                    )
                    : "none"
            );

        if (orchestrator == "aspire" && databaseProvider != "sqlite")
        {
            var aspireNameValidation = AspireResourceNameValidator.Validate(name, databaseProvider);
            if (!aspireNameValidation.IsValid)
            {
                WriteErrorPanel("Invalid project name", aspireNameValidation.ErrorMessage);
                return 1;
            }
        }

        var authCompatValidation = AuthOrmCompatibilityValidator.Validate(auth, orm);
        if (!authCompatValidation.IsValid)
        {
            WriteErrorPanel("Incompatible auth/orm combination", authCompatValidation.ErrorMessage);
            return 1;
        }

        var request = new GenerationRequest(
            TemplateShortName: TemplateShortName,
            ProjectName: name,
            OutputDirectory: outputDirectory,
            Parameters: new Dictionary<string, string>
            {
                ["Orm"] = orm,
                ["DatabaseProvider"] = databaseProvider,
                ["Orchestrator"] = orchestrator,
                ["Auth"] = auth,
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

            WriteErrorPanel($"Failed to generate '{name}'", diagnosticsText, escapeMessage: false);
            return 1;
        }

        await TryRestoreLocalToolsAsync(outputDirectory, settings.NoRestore, cancellationToken);

        RenderSuccess(name, result);
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
            _theme.Message(Severity.Info, "--no-restore set: skipping `dotnet tool restore`.");
            return;
        }

        var manifestPath = Path.Combine(outputDirectory, ".config", "dotnet-tools.json");
        if (!File.Exists(manifestPath))
        {
            // No local manifest -> nothing to restore.
            return;
        }

        var spec = new ProcessSpec("dotnet", ["tool", "restore"], outputDirectory);

        try
        {
            var exitCode = _theme.LiveRegionsEnabled
                ? await _theme
                    .CreateStatus()
                    .StartAsync(
                        "Restoring local tools (dotnet tool restore)...",
                        _ => _processRunner.RunAsync(spec, cancellationToken)
                    )
                : await RunRestoreWithMessageAsync(spec, cancellationToken);

            if (exitCode != 0)
            {
                _theme.Message(
                    Severity.Warning,
                    "`dotnet tool restore` failed (exit "
                        + exitCode
                        + "). The generated project is on disk, but local tools may not be available. Run `dotnet tool restore` manually inside the project to fix."
                );
            }
        }
        catch (Exception ex)
        {
            _theme.Message(
                Severity.Warning,
                "`dotnet tool restore` threw: " + Markup.Escape(ex.Message)
            );
        }
    }

    private async Task<int> RunRestoreWithMessageAsync(
        ProcessSpec spec,
        CancellationToken cancellationToken
    )
    {
        _theme.Message(Severity.Info, "Restoring local tools (dotnet tool restore)...");
        return await _processRunner.RunAsync(spec, cancellationToken);
    }

    private void WriteErrorPanel(string header, string? message, bool escapeMessage = true)
    {
        var content = message ?? "An unknown error occurred.";
        _theme.OutcomePanel(Severity.Error, header, content, escapeMessage);
    }

    private void RenderSuccess(string projectName, GenerationResult result)
    {
        if (result.CreatedFiles.Count > 0)
        {
            var table = _theme.CreateTable("Created files");
            table.AddColumn("Path");
            foreach (var file in result.CreatedFiles)
            {
                table.AddRow(Markup.Escape(Path.GetRelativePath(result.OutputDirectory, file)));
            }

            _console.Write(table);
        }

        var nextSteps =
            $"cd {projectName}{Environment.NewLine}dotnet build{Environment.NewLine}dotnet dorn test";
        _theme.OutcomePanel(Severity.Success, "Next steps", nextSteps);
    }
}
