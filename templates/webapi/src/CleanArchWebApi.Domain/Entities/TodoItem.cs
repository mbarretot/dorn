namespace CleanArchWebApi.Domain.Entities;

public class TodoItem : BaseEntity
{
    public required string Title { get; set; }

    public bool IsComplete { get; set; }
}
