using Dorn.Abstractions.Generation;
using Dorn.Cli.Execution;
using Dorn.Cli.Theming;
using Dorn.Core.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.New;

public sealed class NewWorkerCommand(
    IGenerationEngine generationEngine,
    IProcessRunner processRunner,
    IAnsiConsole console,
    IDornTheme theme
) : AsyncCommand<NewWorkerSettings>
{
    private const string TemplateShortName = "dorn-worker";

    private readonly IGenerationEngine _generationEngine = generationEngine;
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly IAnsiConsole _console = console;
    private readonly IDornTheme _theme = theme;

    // Spectre.Console.Cli 0.55.0 changed ExecuteAsync from public to protected (and added
    // CancellationToken); logic lives in the public RunAsync below, and tests call it
    // directly (CommandAppTester was removed in 0.55.0). Mirrors NewWebApiCommand.
    protected override Task<int> ExecuteAsync(
        CommandContext context,
        NewWorkerSettings settings,
        CancellationToken cancellationToken
    ) => RunAsync(settings, cancellationToken);

    /// <summary>
    /// Runs the new worker command logic. Public so unit tests can drive the command
    /// directly without going through the Spectre.Console.Cli command pipeline.
    /// </summary>
    public async Task<int> RunAsync(NewWorkerSettings settings, CancellationToken cancellationToken)
    {
        var validation = ProjectNameValidator.Validate(settings.Name);
        if (!validation.IsValid)
        {
            WriteErrorPanel("Invalid project name", validation.ErrorMessage);
            return 1;
        }

        var outputDirectory = Path.GetFullPath(settings.Output ?? Path.Combine(".", settings.Name));

        var request = new GenerationRequest(
            TemplateShortName: TemplateShortName,
            ProjectName: settings.Name,
            OutputDirectory: outputDirectory,
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
            _theme.Message(Severity.Info, "--no-restore set: skipping `dotnet tool restore`.");
            return;
        }

        var manifestPath = Path.Combine(outputDirectory, ".config", "dotnet-tools.json");
        if (!File.Exists(manifestPath))
        {
            // No local manifest -> nothing to restore.
            return;
        }

        _theme.Message(Severity.Info, "Restoring local tools (dotnet tool restore)...");

        try
        {
            var exitCode = await _processRunner.RunAsync(
                new ProcessSpec("dotnet", ["tool", "restore"], outputDirectory),
                cancellationToken
            );

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
