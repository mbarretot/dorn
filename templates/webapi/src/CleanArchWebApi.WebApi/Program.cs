using CleanArchWebApi.Application.Todos.CreateTodoItem;
using CleanArchWebApi.Infrastructure.DependencyInjection;
using CleanArchWebApi.WebApi;
using CleanArchWebApi.WebApi.Endpoints;
using Dorn.Messaging;
using FluentValidation;
#if (UseEfCore)
using CleanArchWebApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
#endif

var builder = WebApplication.CreateBuilder(args);

#if (UseAspire)
builder.AddServiceDefaults();
#endif

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediator(typeof(CreateTodoItemCommand).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(CreateTodoItemCommand).Assembly);
builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

#if (UseEfCore)
// Applies pending migrations on startup so `dotnet run` works against a fresh SQLite
// file with zero manual setup. Fine for this scaffold's default (SQLite, single instance);
// swap for a startup migration job or manual `dotnet ef database update` in production setups
// with concurrent instances.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}
#endif

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.MapTodoEndpoints();
#if (UseAspire)
app.MapDefaultEndpoints();
#endif

app.Run();

// Top-level statement Program is internal by default; WebApplicationFactory<Program> needs
// a public type it can reference from CleanArchWebApi.Functional.Tests.
public partial class Program;
