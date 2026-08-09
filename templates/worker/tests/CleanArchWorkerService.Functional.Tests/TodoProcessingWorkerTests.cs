namespace CleanArchWorkerService.Functional.Tests;

public sealed class TodoProcessingWorkerTests : IClassFixture<WorkerHostFixture>
{
    private readonly WorkerHostFixture _fixture;

    public TodoProcessingWorkerTests(WorkerHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessOnceAsync_CompletesPendingItems_ThroughTheRealHost()
    {
        var todoItem = TodoItem.Create("Complete me through the real host");

        await using (var seedScope = _fixture.Host.Services.CreateAsyncScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seedContext.Items.Add(todoItem);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        var worker = _fixture
            .Host.Services.GetServices<IHostedService>()
            .OfType<TodoProcessingWorker>()
            .Single();

        await worker.ProcessOnceAsync(CancellationToken.None);

        // A fresh scope, not the one used to seed — this is what actually proves the per-tick
        // scope committed instead of merely change-tracking (the D3 captive-dependency regression gate).
        await using var assertScope = _fixture.Host.Services.CreateAsyncScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await assertContext.Items.FindAsync(todoItem.Id);

        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsComplete);
    }

    [Fact]
    public async Task HostedService_ProcessesPendingItems_WhenTheTimerTicks()
    {
        var todoItem = TodoItem.Create("Complete me when the timer ticks");

        await using (var seedScope = _fixture.Host.Services.CreateAsyncScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seedContext.Items.Add(todoItem);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        var interval = _fixture
            .Host.Services.GetRequiredService<IOptions<WorkerOptions>>()
            .Value.Interval;

        await _fixture.Host.StartAsync();

        // A single Advance() right after StartAsync races the worker's ExecuteAsync task, which may not
        // have registered its PeriodicTimer with the fake clock yet. Re-advancing inside the bounded poll
        // is what makes this deterministic.
        await WaitFor.UntilAsync(
            async () =>
            {
                _fixture.TimeProvider.Advance(interval);
                await using var scope = _fixture.Host.Services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var current = await dbContext.Items.FindAsync(todoItem.Id);
                return current is { IsComplete: true };
            },
            TimeSpan.FromSeconds(5)
        );

        await _fixture.Host.StopAsync();
    }
}
