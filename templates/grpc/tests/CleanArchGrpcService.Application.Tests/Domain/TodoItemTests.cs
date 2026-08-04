using CleanArchGrpcService.Domain.Entities;

namespace CleanArchGrpcService.Application.Tests.Domain;

public sealed class TodoItemTests
{
    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TodoItem.Create(string.Empty));
    }

    [Fact]
    public void Create_WithWhitespaceOnlyTitle_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TodoItem.Create("   "));
    }

    [Fact]
    public void Create_WithValidTitle_ReturnsEntityAndRaisesTodoItemCreatedEvent()
    {
        var todoItem = TodoItem.Create("Write the Dorn scaffolding");

        Assert.Equal("Write the Dorn scaffolding", todoItem.Title);
        Assert.False(todoItem.IsComplete);

        var domainEvent = Assert.Single(todoItem.DomainEvents);
        var createdEvent = Assert.IsType<TodoItemCreatedEvent>(domainEvent);
        Assert.Equal(todoItem.Id, createdEvent.TodoItemId);
        Assert.Equal("Write the Dorn scaffolding", createdEvent.Title);
    }
}
