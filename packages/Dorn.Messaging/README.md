# Dorn.Messaging

> MIT-licensed in-process mediator. No commercial licensing, no surprises.

Implementation of `Dorn.Messaging.Contracts` — dispatches commands and queries to their handlers, and notifications to all registered handlers.

## Registration

```csharp
// One line to rule them all
builder.Services.AddMediator(typeof(CreateTodoItemCommand).Assembly);
```

`AddMediator` registers:
- `ISender` and `IPublisher` as scoped services
- All `IRequestHandler<,>` implementations
- All `INotificationHandler<>` implementations
- All `IPipelineBehavior<,>` implementations

Auto-discovers everything in the provided assembly via reflection.

## Send a command or query

```csharp
public sealed record CreateTodoItemCommand(string Title) : IRequest<Guid>;

// In your endpoint
var id = await sender.Send(new CreateTodoItemCommand("My task"), ct);
```

## Publish domain events

```csharp
// In your aggregate
public class TodoItem : AggregateRoot
{
    public static TodoItem Create(string title)
    {
        var item = new TodoItem { Title = title };
        item.AddDomainEvent(new TodoItemCreatedEvent(item.Id));
        return item;
    }
}

// In your DbContext — dispatches after SaveChanges
public override async Task<int> SaveChangesAsync(CancellationToken ct)
{
    var events = ChangeTracker
        .Entries<AggregateRoot>()
        .Select(e => e.Entity)
        .Where(e => e.DomainEvents.Count > 0)
        .ToList();

    var result = await base.SaveChangesAsync(ct);

    foreach (var entity in events)
    {
        foreach (var domainEvent in entity.DomainEvents.ToArray())
        {
            await _publisher.Publish(domainEvent, ct);
            entity.ClearDomainEvents();
        }
    }

    return result;
}
```

## Pipeline behaviors

Behaviors execute in reverse registration order — the last registered behavior is the outermost wrapper.

```csharp
// Registered first → runs first (innermost)
services.AddMediator(typeof(CreateTodoItemCommand).Assembly);
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

> [!NOTE]
> The `ValidationBehavior` above requires `FluentValidation`. If your project doesn't use FluentValidation, remove or replace it.

## Installation

```
dotnet add package Dorn.Messaging
```

Depends on `Dorn.Messaging.Contracts` and `Microsoft.Extensions.DependencyInjection.Abstractions`.
