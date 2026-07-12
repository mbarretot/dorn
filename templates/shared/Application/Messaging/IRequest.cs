namespace CleanArchWebApi.Application.Messaging;

public interface IRequest<TResponse>;

public interface IRequest : IRequest<Unit>;
