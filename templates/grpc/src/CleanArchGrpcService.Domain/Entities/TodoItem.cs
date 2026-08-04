using CleanArchGrpcService.Domain.Events;

namespace CleanArchGrpcService.Domain.Entities;

public class TodoItem : AggregateRoot
{
    public string Title { get; private set; } = string.Empty;

    public bool IsComplete { get; private set; }

    private TodoItem() { }

    public static TodoItem Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title must not be empty.", nameof(title));
        }

        var todoItem = new TodoItem { Title = title };
        todoItem.AddDomainEvent(new TodoItemCreatedEvent(todoItem.Id, todoItem.Title));
        return todoItem;
    }
}
