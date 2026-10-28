using System.Threading.Tasks;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Acadex.Windows.Services;

/// <summary>Stores the access token encrypted for the current Windows user.</summary>
public sealed class SessionStore
{
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Acadex");

    private string TokenPath => Path.Combine(_directory, "session.bin");

    public async Task SaveAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        Directory.CreateDirectory(_directory);
        var plain = Encoding.UTF8.GetBytes(token);
        try
        {
            var protectedBytes = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(TokenPath, protectedBytes, cancellationToken);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(TokenPath)) return null;
        var protectedBytes = await File.ReadAllBytesAsync(TokenPath, cancellationToken);
        try
        {
            var plain = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(plain); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (CryptographicException)
        {
            Delete();
            return null;
        }
    }

    public void Delete()
    {
        try { if (File.Exists(TokenPath)) File.Delete(TokenPath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public Task DeleteAsyncSafe()
    {
        Delete();
        return Task.CompletedTask;
    }
}
