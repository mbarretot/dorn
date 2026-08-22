namespace Dorn.WebUI.Primitives.Toast;

public sealed class ToastStore : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly ToastStoreOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly List<ToastMessage> _items = [];
    private readonly Dictionary<Guid, ITimer> _expiryTimers = [];
    private bool _disposed;

    public ToastStore(ToastStoreOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? new ToastStoreOptions();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.Capacity, 0);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public event Action<IReadOnlyList<ToastMessage>>? Changed;

    public IReadOnlyList<ToastMessage> Items
    {
        get
        {
            lock (_sync)
            {
                return Snapshot();
            }
        }
    }

    public Guid Publish(ToastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ITimer? evictedTimer = null;
        IReadOnlyList<ToastMessage> snapshot;
        Guid id;

        lock (_sync)
        {
            ThrowIfDisposed();

            if (_items.Count == _options.Capacity)
            {
                var evicted = _items[0];
                _items.RemoveAt(0);
                _expiryTimers.Remove(evicted.Id, out evictedTimer);
            }

            id = Guid.NewGuid();
            _items.Add(new ToastMessage(id, request, _timeProvider.GetUtcNow()));
            ScheduleExpiry(id, request.Duration ?? _options.DefaultDuration);
            snapshot = Snapshot();
        }

        evictedTimer?.Dispose();
        Notify(snapshot);
        return id;
    }

    public bool Dismiss(Guid id)
    {
        ITimer? timer;
        IReadOnlyList<ToastMessage> snapshot;

        lock (_sync)
        {
            var index = _items.FindIndex(item => item.Id == id);

            if (index < 0)
            {
                return false;
            }

            _items.RemoveAt(index);
            _expiryTimers.Remove(id, out timer);
            snapshot = Snapshot();
        }

        timer?.Dispose();
        Notify(snapshot);
        return true;
    }

    public async Task InvokeActionAsync(Guid id)
    {
        ToastActionDescriptor? action;

        lock (_sync)
        {
            action = _items.Find(item => item.Id == id)?.Request.Action;
        }

        if (action is null)
        {
            return;
        }

        await action.ExecuteAsync();
        Dismiss(id);
    }

    public ValueTask DisposeAsync()
    {
        ITimer[] timers;

        lock (_sync)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            timers = [.. _expiryTimers.Values];
            _expiryTimers.Clear();
            _items.Clear();
            Changed = null;
        }

        foreach (var timer in timers)
        {
            timer.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private void ScheduleExpiry(Guid id, TimeSpan? duration)
    {
        if (duration is not { } value)
        {
            return;
        }

        _expiryTimers[id] = _timeProvider.CreateTimer(
            static state =>
            {
                var (store, toastId) = ((ToastStore Store, Guid Id))state!;
                store.Dismiss(toastId);
            },
            (this, id),
            value,
            Timeout.InfiniteTimeSpan
        );
    }

    private IReadOnlyList<ToastMessage> Snapshot() => Array.AsReadOnly(_items.ToArray());

    private void Notify(IReadOnlyList<ToastMessage> snapshot)
    {
        foreach (
            var subscriber in Changed
                ?.GetInvocationList()
                .Cast<Action<IReadOnlyList<ToastMessage>>>()
                ?? []
        )
        {
            try
            {
                subscriber(snapshot);
            }
            catch { }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
