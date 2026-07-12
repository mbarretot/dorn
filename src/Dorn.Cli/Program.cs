using Dorn.Cli.Commands.New;
using Dorn.Cli.Infrastructure;
using Dorn.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

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
