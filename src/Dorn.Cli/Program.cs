using Dorn.Cli.Commands.New;
using Dorn.Cli.Infrastructure;
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
    AnsiConsole.Write(table);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine(
        "Run [yellow]dorn <command> --help[/] for options on a specific command."
    );
    AnsiConsole.MarkupLine("Run [yellow]dorn --help[/] for the full command reference.");
}
