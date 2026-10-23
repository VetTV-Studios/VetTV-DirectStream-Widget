using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VetTVDirectStream;

public class AppSettings
{
    public string Platform { get; set; } = "Twitch";
    public string ServerUrl { get; set; } = "";
    public string KeyProtected { get; set; } = "";   // DPAPI-encrypted, current user only
    public int Resolution { get; set; } = 1;          // 0=720p 1=1080p 2=1440p
    public int Fps { get; set; } = 1;                 // 0=30 1=60
    public int BitrateKbps { get; set; } = 0;         // 0 = auto
    public string Encoder { get; set; } = "auto";
    public string? MicDevice { get; set; }
    public double GameVolume { get; set; } = 0.8;
    public double MicVolume { get; set; } = 0.8;
    public bool GameOn { get; set; } = true;
    public bool MicOn { get; set; } = true;
    public int Monitor { get; set; } = 0;

    static string PathFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VetTVDirectStream", "settings.json");

    public string StreamKey
    {
        get
        {
            if (string.IsNullOrEmpty(KeyProtected)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(KeyProtected), null, DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }
        set => KeyProtected = string.IsNullOrEmpty(value) ? "" :
            Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    }

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PathFile)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathFile)!);
        File.WriteAllText(PathFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
