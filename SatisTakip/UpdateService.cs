using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace SatisTakip;

public sealed class UpdateService
{
    private const string ManifestUrl = "https://raw.githubusercontent.com/maholinka3131/SATISCIM/main/update.json";
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            await using var stream = await client.GetStreamAsync(ManifestUrl);
            return await JsonSerializer.DeserializeAsync<UpdateInfo>(stream);
        }
        catch
        {
            return null;
        }
    }

    public static void OpenInstaller(string downloadUrl)
    {
        Process.Start(new ProcessStartInfo(downloadUrl) { UseShellExecute = true });
    }
}

public sealed class UpdateInfo
{
    public string Version { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Notes { get; set; } = "";
}
