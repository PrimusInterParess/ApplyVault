using ApplyVault.Api.Infrastructure;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.AspNetCore.Http;

namespace ApplyVault.Api.Tests;

public sealed class JobnetJobClientExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_JobnetJobClientException_ReturnsBadGatewayJson()
    {
        var handler = new JobnetJobClientExceptionHandler();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            httpContext,
            new JobnetJobClientException("Jobnet search failed with status 502."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        Assert.Contains("Jobnet integration error", body);
        Assert.Contains("Jobnet search failed with status 502.", body);
    }

    [Fact]
    public async Task TryHandleAsync_OtherException_ReturnsFalse()
    {
        var handler = new JobnetJobClientExceptionHandler();
        var httpContext = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("unexpected"),
            CancellationToken.None);

        Assert.False(handled);
    }
}
