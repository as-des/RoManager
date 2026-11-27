using System.Text.Json;
using System.Text.RegularExpressions;

namespace RoManager.Core;

/**
 * Interface appearance: accent colour and background blur.
 *
 * Stored unencrypted in appearance.json next to the exe, like shader.json, so
 * the look applies before the vault is unlocked. The UI layer (Appearance.cs)
 * applies it to theme brushes; the core only reads and validates the file.
 */
public sealed class AppearanceSettings
{
    public string Accent { get; set; } = "#3b82f6";
    public double Blur { get; set; } = 14;
}

public static class AppearanceConfig
{
    public const double BlurMin = 0;
    public const double BlurMax = 36;
    public const string DefaultAccent = "#3b82f6";

    static readonly Regex Hex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public static AppearanceSettings Current { get; private set; } = new();

    /** Settings applied; windows can refresh computed elements. */
    public static event Action Changed;

    static string FilePath => Path.Combine(AppContext.BaseDirectory, "appearance.json");

    public static AppearanceSettings Load()
    {
        var s = new AppearanceSettings();
        try
        {
            if (File.Exists(FilePath))
            {
                var parsed = JsonSerializer.Deserialize<AppearanceSettings>(
                    File.ReadAllText(FilePath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null)
                {
                    if (parsed.Accent != null) s.Accent = parsed.Accent;
                    s.Blur = parsed.Blur;
                }
            }
        }
        catch { /* corrupted file: use defaults */ }

        Normalize(s);
        Current = s;
        return s;
    }

    public static void Save(AppearanceSettings s)
    {
        Normalize(s);
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(s));
        }
        catch (Exception ex)
        {
            CoreHost.Log("SaveAppearance EX: " + ex.Message);
        }
        Current = s;
        Changed?.Invoke();
    }

    public static void Normalize(AppearanceSettings s)
    {
        s.Accent = Hex.IsMatch(s.Accent ?? "") ? s.Accent.ToLowerInvariant() : DefaultAccent;
        if (!double.IsFinite(s.Blur)) s.Blur = 14;
        s.Blur = Math.Clamp(s.Blur, BlurMin, BlurMax);
    }

    public static bool IsHex(string value) => Hex.IsMatch(value ?? "");

    public static (byte r, byte g, byte b) Rgb(string hex)
    {
        if (!Hex.IsMatch(hex ?? "")) hex = DefaultAccent;
        return (Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16));
    }
}
