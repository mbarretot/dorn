namespace CleanArchWorkerService.Integration.Tests.Todos;

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

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyIncompleteItems()
    {
        var repository = new TodoItemRepository(_fixture.DbContext);
        var incomplete = TodoItem.Create("Still pending");
        var complete = TodoItem.Create("Already done");
        complete.MarkComplete();

        await repository.AddAsync(incomplete, CancellationToken.None);
        await repository.AddAsync(complete, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // Forces the next read to hit the database, not the change tracker's cache.
        _fixture.DbContext.ChangeTracker.Clear();

        var pending = await repository.GetPendingAsync(CancellationToken.None);

        Assert.Contains(pending, item => item.Id == incomplete.Id);
        Assert.DoesNotContain(pending, item => item.Id == complete.Id);
        Assert.All(pending, item => Assert.False(item.IsComplete));
    }
}
