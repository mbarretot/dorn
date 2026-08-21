using Dorn.WebUI.Primitives.Toast;
using Xunit;

namespace Dorn.WebUI.Primitives.Tests.Toast;

public class ToastStoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Publish_EmitsOrderedSnapshotsWithStableIds()
    {
        var store = new ToastStore();
        var snapshots = new List<IReadOnlyList<ToastMessage>>();
        store.Changed += snapshots.Add;

        var firstId = store.Publish(new ToastRequest("First"));
        var secondId = store.Publish(new ToastRequest("Second"));

        Assert.Equal([firstId, secondId], store.Items.Select(item => item.Id));
        Assert.Equal([firstId, secondId], snapshots[^1].Select(item => item.Id));
    }

    [Fact]
    public void Publish_WhenAtCapacity_EvictsTheOldestToast()
    {
        var store = new ToastStore(new ToastStoreOptions(Capacity: 2));
        var firstId = store.Publish(new ToastRequest("First"));
        var secondId = store.Publish(new ToastRequest("Second"));

        var thirdId = store.Publish(new ToastRequest("Third"));

        Assert.Equal([secondId, thirdId], store.Items.Select(item => item.Id));
        Assert.DoesNotContain(store.Items, item => item.Id == firstId);
    }

    [Fact]
    public void Dismiss_OldIdentity_DoesNotRemoveNewerToast()
    {
        var store = new ToastStore();
        var firstId = store.Publish(new ToastRequest("First"));
        var secondId = store.Publish(new ToastRequest("Second"));

        var dismissed = store.Dismiss(firstId);

        Assert.True(dismissed);
        Assert.Equal([secondId], store.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Expiry_RemovesOnlyTheScheduledToast()
    {
        var timeProvider = new ManualTimeProvider(T0);
        await using var store = new ToastStore(timeProvider: timeProvider);
        var expiringId = store.Publish(
            new ToastRequest("First", Duration: TimeSpan.FromSeconds(5))
        );
        var retainedId = store.Publish(new ToastRequest("Second"));

        timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal([retainedId], store.Items.Select(item => item.Id));
        Assert.DoesNotContain(store.Items, item => item.Id == expiringId);
    }

    [Fact]
    public async Task InvokeActionAsync_DismissesAfterSuccessAndPreservesAfterFailure()
    {
        await using var store = new ToastStore();
        var successfulId = store.Publish(
            new ToastRequest(
                "Success",
                Action: new ToastActionDescriptor("Run", () => Task.CompletedTask)
            )
        );
        var failingId = store.Publish(
            new ToastRequest(
                "Failure",
                Action: new ToastActionDescriptor(
                    "Run",
                    () => Task.FromException(new InvalidOperationException())
                )
            )
        );

        await store.InvokeActionAsync(successfulId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.InvokeActionAsync(failingId)
        );

        Assert.Equal([failingId], store.Items.Select(item => item.Id));
    }

    [Fact]
    public void Publish_ContinuesNotifyingSubscribersAfterOneThrows()
    {
        var store = new ToastStore();
        var notifications = 0;
        store.Changed += _ => throw new InvalidOperationException();
        store.Changed += _ => notifications++;

        store.Publish(new ToastRequest("First"));

        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task DisposeAsync_RejectsPublicationAndCancelsPendingExpiry()
    {
        var timeProvider = new ManualTimeProvider(T0);
        var store = new ToastStore(timeProvider: timeProvider);
        store.Publish(new ToastRequest("First", Duration: TimeSpan.FromSeconds(5)));

        await store.DisposeAsync();
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.Throws<ObjectDisposedException>(() => store.Publish(new ToastRequest("Second")));
        Assert.Empty(store.Items);
    }
}

file sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        var timer = new ManualTimer(callback, state, _now + dueTime);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan duration)
    {
        _now += duration;

        foreach (var timer in _timers.Where(timer => timer.DueAt <= _now).ToArray())
        {
            timer.Fire();
        }
    }
}

file sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
{
    private bool _disposed;

    public DateTimeOffset DueAt { get; private set; } = dueAt;

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        DueAt = DateTimeOffset.MinValue.Add(dueTime);
        return true;
    }

    public void Dispose() => _disposed = true;

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Fire()
    {
        if (!_disposed)
        {
            callback(state);
        }
    }
}
