using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acadex.Windows.Models;

namespace Acadex.Windows.Services;

public sealed class NativeExamService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly AcadexApiClient _api;
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public NativeExamService(AcadexApiClient api) => _api = api;

    public async Task<ExamSessionResponse> StartOrResumeAsync(
        string examId, string password, string? sessionToken = null, string? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        var existingDeviceId = deviceId ?? Guid.NewGuid().ToString("N");
        return await _api.SendJsonAsync<ExamSessionResponse>(HttpMethod.Post,
            $"api/exam/{Uri.EscapeDataString(examId)}/session",
            new { password, sessionToken, deviceId = existingDeviceId },
            cancellationToken: cancellationToken);
    }

    public async Task SaveProgressAsync(string examId, string sessionToken, object progress,
        CancellationToken cancellationToken = default)
    {
        await _api.SendJsonAsync<JsonElement>(HttpMethod.Post,
            $"api/exam/{Uri.EscapeDataString(examId)}/progress",
            new { sessionToken, progress }, cancellationToken: cancellationToken);
    }

    public async Task<JsonElement> FinishAsync(string examId, string sessionToken, object answers,
        CancellationToken cancellationToken = default)
    {
        return await _api.SendJsonAsync<JsonElement>(HttpMethod.Post,
            $"api/exam/{Uri.EscapeDataString(examId)}/finish",
            new { sessionToken, answers }, cancellationToken: cancellationToken);
    }

    public async Task<Stream> DownloadPdfAsync(string examId, string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_api.Token))
            throw new ApiException((int)HttpStatusCode.Unauthorized, "Sign in to download this exam PDF.");

        var uri = new Uri(_api.BaseAddress, $"api/exam/{Uri.EscapeDataString(examId)}/pdf");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Token);
        request.Headers.Add("X-Exam-Session", sessionToken);
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = $"PDF request failed with HTTP {(int)response.StatusCode}.";
            try
            {
                var json = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, cancellationToken);
                if (!string.IsNullOrWhiteSpace(json?.Error)) message = json.Error;
            }
            catch { /* Keep status message when the server body isn't JSON. */ }
            response.Dispose();
            throw new ApiException((int)response.StatusCode, message);
        }

        // The caller owns and must dispose the stream; keep the response alive until it is disposed.
        return new ResponseOwnedStream(await response.Content.ReadAsStreamAsync(cancellationToken), response);
    }

    public void Dispose() => _http.Dispose();

    private sealed class ResponseOwnedStream(Stream inner, HttpResponseMessage response) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) { inner.Dispose(); response.Dispose(); } base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); response.Dispose(); GC.SuppressFinalize(this); }
    }
}
