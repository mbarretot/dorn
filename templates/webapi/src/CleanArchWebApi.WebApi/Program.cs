using CleanArchWebApi.Application.Todos.CreateTodoItem;
using CleanArchWebApi.Infrastructure.DependencyInjection;
using CleanArchWebApi.Infrastructure.Persistence;
using CleanArchWebApi.WebApi;
using CleanArchWebApi.WebApi.Endpoints;
using Dorn.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediator(typeof(CreateTodoItemCommand).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(CreateTodoItemCommand).Assembly);
builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Applies pending migrations on startup so `dotnet run` works against a fresh SQLite
// file with zero manual setup. Fine for this scaffold's default (SQLite, single instance);
// swap for a startup migration job or manual `dotnet ef database update` in production setups
// with concurrent instances.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.MapTodoEndpoints();

app.Run();
