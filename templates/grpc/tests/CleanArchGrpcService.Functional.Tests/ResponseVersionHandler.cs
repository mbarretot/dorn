namespace CleanArchGrpcService.Functional.Tests;

internal sealed class ResponseVersionHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var response = await base.SendAsync(request, cancellationToken);
        response.Version = request.Version;
        return response;
    }
}
