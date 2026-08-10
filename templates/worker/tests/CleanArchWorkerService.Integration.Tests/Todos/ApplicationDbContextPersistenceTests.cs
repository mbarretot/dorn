using CleanArchWorkerService.Domain.Events;

namespace CleanArchWorkerService.Integration.Tests.Todos;

/// <summary>Exercises ApplicationDbContext.SaveChangesAsync's event-publishing override (see PersistenceTestFixture).</summary>
public sealed class ApplicationDbContextPersistenceTests : IClassFixture<PersistenceTestFixture>
{
    private readonly PersistenceTestFixture _fixture;

    public ApplicationDbContextPersistenceTests(PersistenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MarkComplete_AfterSaveChanges_PersistsAcrossAReloadedContext()
    {
        var todoItem = TodoItem.Create("Reload me after completion");
        _fixture.DbContext.Items.Add(todoItem);
        await _fixture.DbContext.SaveChangesAsync(CancellationToken.None);

        todoItem.MarkComplete();
        await _fixture.DbContext.SaveChangesAsync(CancellationToken.None);

        await using var reloadedContext = _fixture.CreateContext(Substitute.For<IPublisher>());
        var reloaded = await reloadedContext.Items.FindAsync(todoItem.Id);

        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsComplete);
    }

    [Fact]
    public async Task SaveChangesAsync_PublishesDomainEventAndClearsIt()
    {
        var publisher = Substitute.For<IPublisher>();
        await using var context = _fixture.CreateContext(publisher);
        var todoItem = TodoItem.Create("Publish me");
        context.Items.Add(todoItem);
        await context.SaveChangesAsync(CancellationToken.None);
        publisher.ClearReceivedCalls();

        todoItem.MarkComplete();
        await context.SaveChangesAsync(CancellationToken.None);

        await publisher
            .Received(1)
            .Publish(
                Arg.Is<TodoItemCompletedEvent>(e => e.TodoItemId == todoItem.Id),
                Arg.Any<CancellationToken>()
            );
        Assert.Empty(todoItem.DomainEvents);
    }
}
