var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediator(typeof(ProcessPendingTodoItemsCommand).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(ProcessPendingTodoItemsCommand).Assembly);
builder.Services.AddWorker(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();

    // A worker has no inbound API, so an empty database means an invisible loop. Seed only in
    // Development so `dotnet run --project src/<Name>.AppHost` shows the loop doing real work.
    if (app.Environment.IsDevelopment() && !await dbContext.Items.AnyAsync())
    {
        dbContext.Items.AddRange(
            TodoItem.Create("Write the design"),
            TodoItem.Create("Ship the worker")
        );
        await dbContext.SaveChangesAsync();
    }
}

app.MapDefaultEndpoints();
await app.RunAsync();

public partial class Program;
