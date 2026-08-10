using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Dorn.Cli.Theming;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Test;

/// <summary>
/// <c>dorn test</c> — runs all (or a filtered subset of) test tiers in the generated project.
/// </summary>
public sealed class TestCommand : AsyncCommand<TestSettings>
{
    private readonly IProjectContextResolver _resolver;
    private readonly IDotnetTestRunner _runner;
    private readonly IDornTheme _theme;

    public TestCommand(IProjectContextResolver resolver, IDotnetTestRunner runner, IDornTheme theme)
    {
        _resolver = resolver;
        _runner = runner;
        _theme = theme;
    }

    // Spectre.Console.Cli 0.55.0 changed ExecuteAsync from public to protected (and added
    // CancellationToken); logic lives in the public RunAsync below, and tests call it
    // directly (CommandAppTester was removed in 0.55.0).
    protected override Task<int> ExecuteAsync(
        CommandContext context,
        TestSettings settings,
        CancellationToken cancellationToken
    ) => RunAsync(settings, cancellationToken);

    /// <summary>
    /// Runs the test command logic. Public so unit tests can drive the command directly
    /// without going through the Spectre.Console.Cli command pipeline.
    /// </summary>
    public async Task<int> RunAsync(TestSettings settings, CancellationToken cancellationToken)
    {
        var root = settings.Project ?? Directory.GetCurrentDirectory();
        var projectContext = _resolver.Resolve(root);

        // Empty tiers = IncludeTests=false — surface a clear non-crash message instead of
        // silently exiting 0.
        if (projectContext.Tiers.Count == 0)
        {
            _theme.Message(
                Severity.Warning,
                "No test tiers found. This project was generated with [bold]IncludeTests=false[/]; nothing to test."
            );
            return 0;
        }

        var tiers = ResolveTiers(settings.Tier, projectContext.Tiers);

        var result = await _runner.RunAsync(
            projectContext,
            DatabaseProvider.Sqlite,
            tiers,
            cancellationToken
        );

        if (!result.AllSucceeded)
        {
            _theme.Message(Severity.Error, "One or more tier runs failed.");
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
            // ADR 0012: Application.Tests IS the unit-level tier; "unit" is the documented alias.
            "unit" or "application" => [TestTier.Application],
            "integration" => [TestTier.Integration],
            "architecture" => [TestTier.Architecture],
            "functional" => [TestTier.Functional],
            _ => all, // unknown values fall back to all tiers (defensive)
        };
    }
}
