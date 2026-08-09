namespace CleanArchWorkerService.Application.Todos.ProcessPendingTodoItems;

public sealed class ProcessPendingTodoItemsCommandHandler
    : IRequestHandler<ProcessPendingTodoItemsCommand, int>
{
    private readonly ITodoItemRepository _repository;

    public ProcessPendingTodoItemsCommandHandler(ITodoItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(ProcessPendingTodoItemsCommand request, CancellationToken ct)
    {
        var pending = await _repository.GetPendingAsync(ct);

        foreach (var item in pending)
        {
            item.MarkComplete();
        }

        await _repository.SaveChangesAsync(ct);

        return pending.Count;
    }
}
