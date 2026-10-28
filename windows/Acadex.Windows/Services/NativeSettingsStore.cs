using System;
using System.IO;
using System.Text.Json;

namespace Acadex.Windows.Services;

public sealed class NativeSettingsStore
{
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Acadex");
    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public string LoadApiUrl(string fallback)
    {
        try
        {
            if (!File.Exists(SettingsPath)) return fallback;
            using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            if (document.RootElement.TryGetProperty("apiUrl", out var prop) &&
                prop.ValueKind == JsonValueKind.String &&
                Uri.TryCreate(prop.GetString(), UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback))
                return uri.AbsoluteUri.EndsWith('/') ? uri.AbsoluteUri : uri.AbsoluteUri + "/";
        }
        catch (IOException) { }
        catch (JsonException) { }
        return fallback;
    }

    public void SaveApiUrl(string apiUrl)
    {
        Directory.CreateDirectory(_directory);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new { apiUrl }));
        File.Move(temp, SettingsPath, overwrite: true);
    }
}
