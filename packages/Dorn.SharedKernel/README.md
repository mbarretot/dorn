# Dorn.SharedKernel

> The minimal DDD building blocks every layer needs. No external dependencies.

Domain base types and primitives shared across every Dorn template: `Entity`, `AggregateRoot`, and `Result`.

## Entity

Base class for all domain entities. Provides identity-based equality — two entities are equal only if they are the same type AND have the same `Id`.

```csharp
public class TodoItem : Entity
{
    public string Title { get; private set; }
    public bool IsComplete { get; private set; }
}

// Two TodoItems with the same Id are the same entity
var a = new TodoItem { Id = guid, Title = "Task" };
var b = new TodoItem { Id = guid, Title = "Different" };
a == b  // true — same type and Id
```

## AggregateRoot

Extends `Entity` and adds a domain event collection. Only aggregates can raise domain events — domain logic lives on the aggregate, not scattered across services.

```csharp
public class TodoItem : AggregateRoot
{
    public string Title { get; private set; }

    public static TodoItem Create(string title)
    {
        var item = new TodoItem { Title = title };
        item.AddDomainEvent(new TodoItemCreatedEvent(item.Id, item.Title));
        return item;
    }

    public void Complete() { IsComplete = true; }
}
```

| Member | Access | Purpose |
|---|---|---|
| `DomainEvents` | `IReadOnlyCollection<INotification>` (read-only) | Events raised by this aggregate |
| `AddDomainEvent()` | `protected` | Raise a domain event |
| `ClearDomainEvents()` | `public` | Clear events after publishing |

## Result and Result\<T\>

Railway-oriented programming without exceptions for expected failures.

```csharp
// Non-generic — for commands or queries that return nothing
public static Result CreateTodoItem(string title)
{
    if (string.IsNullOrWhiteSpace(title))
        return Result.Failure("Title is required.");

    var item = new TodoItem { Title = title };
    return Result.Success();
}

// Generic — for queries or commands that return a value
public static Result<TodoItem> GetById(Guid id)
{
    var item = _db.Items.Find(id);
    if (item is null)
        return Result.FFailure("Todo item not found.");

    return Result<TodoItem>.Success(item);
}
```

```csharp
var result = GetById(id);

if (result.IsFailure)
{
    // handle error
    return Result.NotFound(result.Error);
}

// use result.Value
var item = result.Value;
```

> [!TIP]
> `Result<T>.Value` throws if accessed on a failed result. Always check `IsSuccess` or `IsFailure` first.

## Installation

```
dotnet add package Dorn.SharedKernel
```

Depends on `Dorn.Messaging.Contracts` (only for `INotification` in `AggregateRoot`).
