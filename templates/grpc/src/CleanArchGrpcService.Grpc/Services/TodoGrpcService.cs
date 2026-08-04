using CleanArchGrpcService.Application.Todos.CreateTodoItem;
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
}
