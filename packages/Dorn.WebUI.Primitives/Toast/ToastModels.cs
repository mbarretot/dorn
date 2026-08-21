namespace Dorn.WebUI.Primitives.Toast;

public sealed record ToastStoreOptions(int Capacity = 5, TimeSpan? DefaultDuration = null);

public sealed record ToastActionDescriptor(string Label, Func<Task> ExecuteAsync);

public sealed record ToastRequest(
    string Title,
    string? Description = null,
    TimeSpan? Duration = null,
    ToastActionDescriptor? Action = null,
    bool Assertive = false
);

public sealed record ToastMessage(Guid Id, ToastRequest Request, DateTimeOffset PublishedAt);
