using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acadex.Windows.Models;

namespace Acadex.Windows.Services;

public sealed class AcadexApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _http;
    private string? _token;

    public Uri BaseAddress { get; private set; }
    public string? Token => _token;
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_token);

    public AcadexApiClient(Uri? baseAddress = null, HttpMessageHandler? handler = null)
    {
        BaseAddress = ValidateBaseAddress(baseAddress ?? new Uri("https://acadex.onrender.com/"));
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        _http.Timeout = Timeout.InfiniteTimeSpan; // Per-request timeout/cancellation is controlled explicitly.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Acadex-Windows/0.1");
    }

    public void SetBaseAddress(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            throw new ArgumentException("Enter a valid absolute API URL.", nameof(value));
        BaseAddress = ValidateBaseAddress(uri);
    }

    private static Uri ValidateBaseAddress(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback)
            throw new ArgumentException("The Acadex API must use HTTPS, except for localhost testing.");
        return new Uri(uri.AbsoluteUri.EndsWith('/') ? uri.AbsoluteUri : uri.AbsoluteUri + "/");
    }

    public void SetToken(string? token)
    {
        _token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    public async Task<HealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/health", authenticated: false,
            timeout: TimeSpan.FromSeconds(8), cancellationToken: cancellationToken);
        return await ReadJsonAsync<HealthResponse>(response, cancellationToken);
    }

    public async Task<HealthResponse> WaitForServiceAsync(
        IProgress<string>? status = null,
        int maxAttempts = 6,
        CancellationToken cancellationToken = default)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            status?.Report(attempt == 1
                ? "Connecting to Acadex…"
                : $"Acadex is waking up. Retry {attempt} of {maxAttempts}…");

            try { return await CheckHealthAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ApiException or JsonException)
            {
                lastError = ex;
            }

            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(2 + attempt * 2, 8)), cancellationToken);
        }

        throw new ApiException(0,
            "Acadex could not be reached after several attempts. Check your connection and try again.", inner: lastError);
    }

    public async Task<AuthResponse> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var body = new { email, password };
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/login", body, false,
            TimeSpan.FromSeconds(25), cancellationToken);
        return await ReadJsonAsync<AuthResponse>(response, cancellationToken);
    }

    public async Task<AuthResponse> RegisterAsync(string role, string email, string password,
        string displayName, string? studentId, CancellationToken cancellationToken = default)
    {
        var body = new { role, email, password, displayName, studentId };
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/register", body, false,
            TimeSpan.FromSeconds(25), cancellationToken);
        return await ReadJsonAsync<AuthResponse>(response, cancellationToken);
    }

    public async Task<MeResponse> GetMeAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/auth/me", true, TimeSpan.FromSeconds(20), cancellationToken);
        return await ReadJsonAsync<MeResponse>(response, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Post, "api/auth/logout", true,
                TimeSpan.FromSeconds(15), cancellationToken);
        }
        finally { _token = null; }
    }

    public Task<T> GetJsonAsync<T>(string relativePath, bool authenticated = true, CancellationToken cancellationToken = default) =>
        SendForJsonAsync<T>(HttpMethod.Get, relativePath, null, authenticated, cancellationToken);

    public Task<T> SendJsonAsync<T>(HttpMethod method, string relativePath, object body, bool authenticated = true,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<T>(method, relativePath, body, authenticated, cancellationToken);

    private async Task<T> SendForJsonAsync<T>(HttpMethod method, string path, object? body, bool authenticated,
        CancellationToken cancellationToken)
    {
        using var response = await SendJsonAsync(method, path, body, authenticated,
            TimeSpan.FromSeconds(30), cancellationToken);
        return await ReadJsonAsync<T>(response, cancellationToken);
    }

    private Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, object? body,
        bool authenticated, TimeSpan timeout, CancellationToken cancellationToken) =>
        SendAsync(method, path, authenticated, timeout, cancellationToken, body);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, bool authenticated,
        TimeSpan timeout, CancellationToken cancellationToken, object? jsonBody = null)
    {
        var uri = new Uri(BaseAddress, relativePath.TrimStart('/'));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        using var request = new HttpRequestMessage(method, uri);
        if (jsonBody is not null)
            request.Content = JsonContent.Create(jsonBody, options: JsonOptions);
        if (authenticated)
        {
            if (string.IsNullOrWhiteSpace(_token))
                throw new ApiException((int)HttpStatusCode.Unauthorized, "Please sign in to continue.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiException(0, "The request timed out. Acadex may still be starting; please retry.");
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(0, "Could not connect to Acadex. Check the network and retry.", inner: ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var (message, code) = await ReadErrorAsync(response, cancellationToken);
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new ApiException(status, message, code);
        }

        return response;
    }

    private static async Task<(string Message, string? Code)> ReadErrorAsync(HttpResponseMessage response, CancellationToken token)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync(token);
            var error = JsonSerializer.Deserialize<ApiError>(raw, JsonOptions);
            if (!string.IsNullOrWhiteSpace(error?.Error)) return (error.Error!, error.Code);
        }
        catch (JsonException) { }
        catch (HttpRequestException) { }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ("Your session expired. Please sign in again.", null),
            HttpStatusCode.Forbidden => ("You don't have permission to do that.", null),
            HttpStatusCode.NotFound => ("The requested Acadex item was not found.", null),
            HttpStatusCode.Conflict => ("This action conflicts with the current exam state.", null),
            _ => ($"Acadex returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).", null)
        };
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new ApiException((int)response.StatusCode, "Acadex returned an empty response.");
        }
        catch (JsonException ex)
        {
            throw new ApiException((int)response.StatusCode, "Acadex returned an unexpected response format.", inner: ex);
        }
    }

    public void Dispose() => _http.Dispose();
}
