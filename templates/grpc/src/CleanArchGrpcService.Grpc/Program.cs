var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediator(typeof(CreateTodoItemCommand).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(CreateTodoItemCommand).Assembly);
builder.Services.AddGrpc(options => options.Interceptors.Add<ValidationInterceptor>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapGrpcService<TodoGrpcService>();
app.Run();

public partial class Program;
