namespace CleanArchWorkerService.Worker;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);
}
