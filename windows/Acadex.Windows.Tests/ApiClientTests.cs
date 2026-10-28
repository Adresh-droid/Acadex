using System.Net;
using System.Text;
using Acadex.Windows.Services;
using Xunit;

namespace Acadex.Windows.Tests;

public sealed class ApiClientTests
{
    [Fact]
    public void RejectsInsecureNonLoopbackApiUrl()
    {
        using var client = new AcadexApiClient();
        Assert.Throws<ArgumentException>(() => client.SetBaseAddress("http://example.com/"));
    }

    [Fact]
    public void AllowsLocalhostForDevelopment()
    {
        using var client = new AcadexApiClient();
        client.SetBaseAddress("http://localhost:3000");
        Assert.Equal("http://localhost:3000/", client.BaseAddress.AbsoluteUri);
    }

    [Fact]
    public async Task HealthCheckDoesNotRequireBearerToken()
    {
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("/api/health", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true,\"ts\":123,\"version\":\"test\"}", Encoding.UTF8, "application/json")
            });
        });

        using var client = new AcadexApiClient(new Uri("https://example.invalid/"), handler);
        var health = await client.CheckHealthAsync();
        Assert.True(health.Ok);
        Assert.Equal("test", health.Version);
    }

    [Fact]
    public async Task AuthenticatedCallsSendBearerToken()
    {
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"user\":{\"id\":\"u1\",\"role\":\"student\",\"email\":\"s@example.com\",\"displayName\":\"Student\",\"studentId\":\"8A\",\"className\":\"8A\"}}", Encoding.UTF8, "application/json")
            });
        });

        using var client = new AcadexApiClient(new Uri("https://example.invalid/"), handler);
        client.SetToken("test-token");
        var me = await client.GetMeAsync();
        Assert.Equal("u1", me.User.Id);
        Assert.Equal("student", me.User.Role);
    }

    [Fact]
    public async Task ApiUnauthorizedStatusBecomesTypedError()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Not logged in.\"}", Encoding.UTF8, "application/json")
        }));

        using var client = new AcadexApiClient(new Uri("https://example.invalid/"), handler);
        client.SetToken("expired-token");
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetMeAsync());
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("Not logged in.", ex.Message);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
