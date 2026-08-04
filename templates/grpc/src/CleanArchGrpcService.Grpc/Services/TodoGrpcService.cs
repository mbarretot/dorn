using CleanArchGrpcService.Application.Todos.CreateTodoItem;
using CleanArchGrpcService.Application.Todos.GetTodoItems;
using CleanArchGrpcService.Grpc.Protos;
using Dorn.Messaging.Contracts;
using Grpc.Core;

namespace CleanArchGrpcService.Grpc.Services;

public sealed class TodoGrpcService(ISender sender) : TodoService.TodoServiceBase
{
    public override async Task<CreateTodoItemResponse> CreateTodoItem(
        CreateTodoItemRequest request,
        ServerCallContext context
    )
    {
        var id = await sender.Send(
            new CreateTodoItemCommand(request.Title),
            context.CancellationToken
        );

        return new CreateTodoItemResponse { Id = id.ToString() };
    }

    public override async Task<GetTodoItemsResponse> GetTodoItems(
        GetTodoItemsRequest request,
        ServerCallContext context
    )
    {
        var items = await sender.Send(new GetTodoItemsQuery(), context.CancellationToken);

        var response = new GetTodoItemsResponse();
        response.Items.AddRange(
            items.Select(item => new Protos.TodoItem
            {
                Id = item.Id.ToString(),
                Title = item.Title,
                IsComplete = item.IsComplete,
            })
        );
        return response;
    }
}
