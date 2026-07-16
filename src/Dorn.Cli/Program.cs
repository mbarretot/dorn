using Dorn.Cli.Commands.Coverage;
using Dorn.Cli.Commands.New;
using Dorn.Cli.Commands.Run;
using Dorn.Cli.Commands.Test;
using Dorn.Cli.Coverage;
using Dorn.Cli.Execution;
using Dorn.Cli.Infrastructure;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Dorn.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

if (args.Length == 0)
{
    ShowWelcome();
    return 0;
}

var services = new ServiceCollection();
services.AddDornCore();
services.AddSingleton(AnsiConsole.Console);
services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddSingleton<ISignalRegistration, SignalRegistration>();
services.AddSingleton<IProjectContextResolver, ProjectContextResolver>();
services.AddSingleton<DotnetTestRunner>();
services.AddSingleton<IDotnetTestRunner>(sp => sp.GetRequiredService<DotnetTestRunner>());
services.AddSingleton<CoverageReporter>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("dorn");
    config.AddBranch(
        "new",
        branch =>
        {
            branch
                .AddCommand<NewWebApiCommand>("webapi")
                .WithDescription("Generate a Clean Architecture Web API project.");
        }
    );
    config
        .AddCommand<TestCommand>("test")
        .WithDescription("Run the generated project's test tiers (default: all).");
    config
        .AddCommand<RunCommand>("run")
        .WithDescription("Run the generated project via AppHost, Compose, or plain dotnet run.");
    config
        .AddCommand<CoverageCommand>("coverage")
        .WithDescription("Run tests with coverage and apply the 80% threshold gate.");
});

return await app.RunAsync(args);

static void ShowWelcome()
{
    AnsiConsole.Write(new FigletText("dorn").Color(Color.SteelBlue1));
    AnsiConsole.MarkupLine("[grey]Clean Architecture project scaffolding for .NET[/]");
    AnsiConsole.WriteLine();

    var table = new Table().Border(TableBorder.Rounded).Title("Available commands");
    table.AddColumn("Command");
    table.AddColumn("Description");
    table.AddRow("[green]new webapi[/] <name>", "Generate a Clean Architecture Web API project.");
    table.AddRow("[green]test[/]", "Run the generated project's test tiers.");
    table.AddRow(
        "[green]run[/]",
        "Run the generated project (auto-detects AppHost/Compose/Plain)."
    );
    table.AddRow("[green]coverage[/]", "Run tests with coverage; gate at 80%.");
    AnsiConsole.Write(table);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine(
        "Run [yellow]dorn <command> --help[/] for options on a specific command."
    );
    AnsiConsole.MarkupLine("Run [yellow]dorn --help[/] for the full command reference.");
}
