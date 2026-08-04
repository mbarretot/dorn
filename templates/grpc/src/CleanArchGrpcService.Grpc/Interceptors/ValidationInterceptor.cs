using FluentValidation;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace CleanArchGrpcService.Grpc.Interceptors;

public sealed class ValidationInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation
    )
    {
        try
        {
            return await continuation(request, context);
        }
        catch (ValidationException exception)
        {
            var detail = string.Join(
                "; ",
                exception.Errors.Select(failure =>
                    $"{failure.PropertyName}: {failure.ErrorMessage}"
                )
            );

            throw new RpcException(new Status(StatusCode.InvalidArgument, detail));
        }
    }
}
