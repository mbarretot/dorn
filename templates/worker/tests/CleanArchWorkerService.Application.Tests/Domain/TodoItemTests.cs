using CleanArchWorkerService.Domain.Entities;

namespace CleanArchWorkerService.Application.Tests.Domain;

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

    [Fact]
    public void MarkComplete_OnIncompleteItem_SetsIsCompleteAndRaisesEvent()
    {
        var todoItem = TodoItem.Create("Ship the worker");
        todoItem.ClearDomainEvents();

        todoItem.MarkComplete();

        Assert.True(todoItem.IsComplete);
        var domainEvent = Assert.Single(todoItem.DomainEvents);
        var completedEvent = Assert.IsType<TodoItemCompletedEvent>(domainEvent);
        Assert.Equal(todoItem.Id, completedEvent.TodoItemId);
        Assert.Equal("Ship the worker", completedEvent.Title);
    }

    [Fact]
    public void MarkComplete_CalledTwice_IsIdempotentAndRaisesNothingOnSecondCall()
    {
        var todoItem = TodoItem.Create("Ship the worker");
        todoItem.MarkComplete();
        todoItem.ClearDomainEvents();

        todoItem.MarkComplete();

        Assert.True(todoItem.IsComplete);
        Assert.Empty(todoItem.DomainEvents);
    }
}
