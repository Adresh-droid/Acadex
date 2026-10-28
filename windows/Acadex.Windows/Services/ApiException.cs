namespace Acadex.Windows.Services;

public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorCode { get; }

    public ApiException(int statusCode, string message, string? errorCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
