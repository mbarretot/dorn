# Dorn.Messaging

An MIT-licensed in-process mediator for [`Dorn.Messaging.Contracts`](../Dorn.Messaging.Contracts/README.md).

## ⚡ Install and register

```bash
dotnet add package Dorn.Messaging
```

```csharp
builder.Services.AddMediator(typeof(CreateTodoCommand).Assembly);
```

`AddMediator` discovers request handlers, notification handlers, and pipeline behaviors in that assembly. `ISender` and `IPublisher` are scoped.

## 🚀 Send and publish

```csharp
var id = await sender.Send(new CreateTodoCommand("Ship README"), ct);
await publisher.Publish(new TodoCreated(id), ct);
```

| Operation | Dispatch |
| --- | --- |
| Request | One matching handler |
| Notification | Every matching handler, sequentially |

## 🔁 Pipeline behaviors

Register cross-cutting concerns after the mediator:

```csharp
services.AddMediator(typeof(CreateTodoCommand).Assembly);
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

Behaviors execute in registration order. The first registered behavior is the outermost wrapper.

> [!NOTE]
> Validation is not built in. Add your preferred validation library inside a behavior.
