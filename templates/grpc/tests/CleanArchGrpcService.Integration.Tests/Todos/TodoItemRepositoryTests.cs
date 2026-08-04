namespace CleanArchGrpcService.Integration.Tests.Todos;

/// <summary>Persists and reloads through the real DbContext (see PersistenceTestFixture).</summary>
public sealed class TodoItemRepositoryTests : IClassFixture<PersistenceTestFixture>
{
    private readonly PersistenceTestFixture _fixture;

    public TodoItemRepositoryTests(PersistenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MigrateAsync_ThenAddAndSaveChanges_PersistsTodoItemAgainstRealProvider()
    {
        var todoItem = TodoItem.Create("Prove migrations apply against the real provider");

        _fixture.DbContext.Items.Add(todoItem);
        await _fixture.DbContext.SaveChangesAsync(CancellationToken.None);

        // Forces the next read to hit the database, not the change tracker's cache.
        _fixture.DbContext.ChangeTracker.Clear();

        var reloaded = await _fixture.DbContext.Items.FindAsync(todoItem.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(todoItem.Title, reloaded!.Title);
        Assert.False(reloaded.IsComplete);
    }

    [Fact]
    public async Task AddAsync_ThenGetAllAsync_ReturnsPersistedItemViaRepository()
    {
        var repository = new TodoItemRepository(_fixture.DbContext);
        var todoItem = TodoItem.Create("Persisted via TodoItemRepository");

        await repository.AddAsync(todoItem, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // Forces the next read to hit the database, not the change tracker's cache.
        _fixture.DbContext.ChangeTracker.Clear();

        var all = await repository.GetAllAsync(CancellationToken.None);

        Assert.Contains(all, item => item.Id == todoItem.Id && item.Title == todoItem.Title);
    }
}
