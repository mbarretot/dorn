using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanArchWorkerService.Worker;

public sealed class TodoProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<TodoProcessingWorker> _logger;

    public TodoProcessingWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<WorkerOptions> options,
        ILogger<TodoProcessingWorker> logger
    )
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Interval, _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessOnceAsync(stoppingToken);
        }
    }

    /// <summary>Runs exactly one tick. Public so tests can drive the work without waiting on the timer.</summary>
    public async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        // BackgroundService is a singleton; ISender/DbContext/repository are scoped. One scope per tick
        // avoids capturing a single DbContext for the process lifetime.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            var processed = await sender.Send(
                new ProcessPendingTodoItemsCommand(),
                cancellationToken
            );
            _logger.LogInformation("Processed {Count} pending todo item(s).", processed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Default BackgroundServiceExceptionBehavior.StopHost would kill the host on one bad tick.
            _logger.LogError(ex, "Tick failed; the loop continues on the next interval.");
        }
    }
}
