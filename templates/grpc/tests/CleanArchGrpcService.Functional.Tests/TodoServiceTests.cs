namespace CleanArchGrpcService.Functional.Tests;

public sealed class TodoServiceTests : IClassFixture<TodoGrpcApplicationFactory>
{
    private readonly TodoGrpcApplicationFactory _factory;

    public TodoServiceTests(TodoGrpcApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateTodoItem_PersistsAndReturnsId()
    {
        var client = new TodoService.TodoServiceClient(_factory.CreateGrpcChannel());
        var response = await client.CreateTodoItemAsync(
            new CreateTodoItemRequest { Title = "Persist through gRPC" }
        );

        Assert.True(Guid.TryParse(response.Id, out var id));
        Assert.NotEqual(Guid.Empty, id);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.ChangeTracker.Clear();
        var persisted = await dbContext.Items.SingleOrDefaultAsync(item => item.Id == id);

        Assert.NotNull(persisted);
        Assert.Equal("Persist through gRPC", persisted!.Title);
    }

    [Fact]
    public async Task CreateTodoItem_InvalidTitle_ReturnsInvalidArgument()
    {
        var client = new TodoService.TodoServiceClient(_factory.CreateGrpcChannel());

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            client.CreateTodoItemAsync(new CreateTodoItemRequest { Title = "" }).ResponseAsync
        );

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Contains("Title", exception.Status.Detail, StringComparison.Ordinal);
    }
}
