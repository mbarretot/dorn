using Dorn.Cli.Commands.Coverage;
using Dorn.Cli.Commands.Doctor;
using Dorn.Cli.Commands.New;
using Dorn.Cli.Commands.Run;
using Dorn.Cli.Commands.Test;
using Dorn.Cli.Coverage;
using Dorn.Cli.Execution;
using Dorn.Cli.Infrastructure;
using Dorn.Cli.Output;
using Dorn.Cli.Projects;
using Dorn.Cli.Templating;
using Dorn.Cli.Testing;
using Dorn.Cli.Theming;
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
services.AddSingleton<IDornTheme, DornTheme>();
services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddSingleton<ICliOutputWriter, ConsoleCliOutputWriter>();
services.AddSingleton<ISignalRegistration, SignalRegistration>();
services.AddSingleton<IProjectContextResolver, ProjectContextResolver>();
services.AddSingleton<ITemplatesRootLocator, TemplatesRootLocator>();
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
            branch
                .AddCommand<NewGrpcCommand>("grpc")
                .WithDescription(
                    "Generate a Clean Architecture gRPC service (sqlite + EF Core + Aspire)."
                );
            branch
                .AddCommand<NewWorkerCommand>("worker")
                .WithDescription(
                    "Generate a Clean Architecture worker service (sqlite + EF Core + Aspire)."
                );
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
    config
        .AddCommand<DoctorCommand>("doctor")
        .WithDescription("Check that the local environment is ready to run dorn.");
});

return await app.RunAsync(args);

static void ShowWelcome()
{
    // Runs before the DI container exists, so the theme is constructed directly here
    // (design: "ShowWelcome() ... can use new DornTheme(AnsiConsole.Console)").
    new DornTheme(AnsiConsole.Console).Banner();
}
