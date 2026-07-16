using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Test;

/// <summary>
/// <c>dorn test</c> — runs all (or a filtered subset of) test tiers in the generated project.
/// </summary>
public sealed class TestCommand : AsyncCommand<TestSettings>
{
    private readonly IProjectContextResolver _resolver;
    private readonly IDotnetTestRunner _runner;
    private readonly IAnsiConsole _console;

    public TestCommand(
        IProjectContextResolver resolver,
        IDotnetTestRunner runner,
        IAnsiConsole console
    )
    {
        _resolver = resolver;
        _runner = runner;
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, TestSettings settings)
    {
        var root = settings.Project ?? Directory.GetCurrentDirectory();
        var projectContext = _resolver.Resolve(root);

        // Empty tiers = IncludeTests=false — surface a clear non-crash message instead of
        // silently exiting 0.
        if (projectContext.Tiers.Count == 0)
        {
            _console.MarkupLine(
                "[yellow]No test tiers found.[/] This project was generated with [bold]IncludeTests=false[/]; nothing to test."
            );
            return 0;
        }

        var tiers = ResolveTiers(settings.Tier, projectContext.Tiers);

        var result = await _runner.RunAsync(
            projectContext,
            DatabaseProvider.Sqlite,
            tiers,
            CancellationToken.None
        );

        if (!result.AllSucceeded)
        {
            _console.MarkupLine("[red]One or more tier runs failed.[/]");
            return 1;
        }

        return 0;
    }

    private static IReadOnlyList<TestTier> ResolveTiers(
        string? tierFilter,
        IReadOnlyList<TestTier> all
    )
    {
        if (string.IsNullOrWhiteSpace(tierFilter))
            return all;

        return tierFilter.ToLowerInvariant() switch
        {
            "unit" => [TestTier.Unit],
            "integration" => [TestTier.Integration],
            "architecture" => [TestTier.Architecture],
            "functional" => [TestTier.Functional],
            _ => all, // unknown values fall back to all tiers (defensive)
        };
    }
}
