using Dorn.Abstractions.Generation;
using Dorn.Cli.Execution;
using Dorn.Cli.Theming;
using Dorn.Core.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.New;

public sealed class NewBlazorWasmCommand(
    IGenerationEngine generationEngine,
    IProcessRunner processRunner,
    IAnsiConsole console,
    IDornTheme theme
) : AsyncCommand<NewBlazorWasmSettings>
{
    private const string TemplateShortName = "dorn-blazor-wasm";

    private readonly IGenerationEngine _generationEngine = generationEngine;
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly IAnsiConsole _console = console;
    private readonly IDornTheme _theme = theme;

    protected override Task<int> ExecuteAsync(
        CommandContext context,
        NewBlazorWasmSettings settings,
        CancellationToken cancellationToken
    ) => RunAsync(settings, cancellationToken);

    /// <summary>
    /// Runs the new blazor wasm command logic. Public so unit tests can drive the command
    /// directly without going through the Spectre.Console.Cli command pipeline.
    /// </summary>
    public async Task<int> RunAsync(
        NewBlazorWasmSettings settings,
        CancellationToken cancellationToken
    )
    {
        var nameValidation = ProjectNameValidator.Validate(settings.Name);
        if (!nameValidation.IsValid)
        {
            WriteErrorPanel("Invalid project name", nameValidation.ErrorMessage);
            return 1;
        }

        var themeValidation = ThemeValidator.Validate(settings.Theme);
        if (!themeValidation.IsValid)
        {
            WriteErrorPanel("Invalid theme", themeValidation.ErrorMessage);
            return 1;
        }

        var outputDirectory = Path.GetFullPath(settings.Output ?? Path.Combine(".", settings.Name));

        var theme =
            settings.Theme?.ToLowerInvariant()
            ?? (
                _console.Profile.Capabilities.Interactive
                    ? _console.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select a [green]theme[/]:")
                            .AddChoices(ThemeValidator.ValidThemes)
                    )
                    : "slate"
            );

        var request = new GenerationRequest(
            TemplateShortName: TemplateShortName,
            ProjectName: settings.Name,
            OutputDirectory: outputDirectory,
            Parameters: new Dictionary<string, string>
            {
                ["Theme"] = theme,
                ["IncludePlayground"] = settings.NoPlayground ? "false" : "true",
            },
            Force: settings.Force
        );

        var result = await _generationEngine.GenerateAsync(request);

        if (!result.Success)
        {
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
