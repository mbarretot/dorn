# Dorn.Messaging.Contracts

> Zero-dependency mediator interfaces for CQRS and domain events. Safe to reference from any layer, including Domain.

Contracts that power the mediator pattern: commands, queries, notifications, pipeline behaviors, and the sender/publisher surface.

## Interfaces

| Interface | Purpose |
|---|---|
| `IRequest<TResponse>` | Marker for a request (command or query) that returns `TResponse` |
| `IRequest` | Shorthand for `IRequest<Unit>` — fire-and-forget commands |
| `ISender` | Sends a request to its single handler |
| `INotification` | Marker for domain/integration events |
| `INotificationHandler<T>` | Handles a notification (zero to many per notification type) |
| `IPipelineBehavior<TRequest, TResponse>` | Cross-cutting behavior wrapped around request handling |
| `IPublisher` | Publishes notifications to all registered handlers |

## Usage

```csharp
// Commands and queries are records
public sealed record CreateTodoItemCommand(string Title) : IRequest<Guid>;
public sealed record GetTodoItemsQuery() : IRequest<IReadOnlyList<TodoItemDto>>;

// Notifications are records
public sealed record TodoItemCreatedEvent(Guid Id, string Title) : INotification;

// Handlers implement the corresponding interface
public sealed class CreateTodoItemCommandHandler
    : IRequestHandler<CreateTodoItemCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateTodoItemCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateTodoItemCommand request, CancellationToken ct)
    {
        var item = new TodoItem { Title = request.Title };
        _db.Items.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}
```

## Unit

`Unit` is the C# equivalent of `void`. Use `IRequest` (shorthand for `IRequest<Unit>`) for commands that don't return a value.

```csharp
public sealed record MarkTodoCompleteCommand(Guid Id) : IRequest;
```

## Pipeline Behaviors

Wrap cross-cutting concerns around request handling:

```csharp
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IValidator<TRequest> _validator;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(request, ct);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);

        return await next();
    }
}
```

## Installation

```
dotnet add package Dorn.Messaging.Contracts
```

Part of the Dorn template ecosystem, but usable standalone in any .NET project.
