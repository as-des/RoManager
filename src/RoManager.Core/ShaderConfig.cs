using System.Text.Json;
using System.Text.RegularExpressions;

namespace RoManager.Core;

/**
 * Настройки анимированного фона. Как и в 2.1.0, хранятся открытым
 * shader.json рядом с exe — фон не зависит от разблокировки хранилища.
 */
public sealed class ShaderSettings
{
    public bool Enabled { get; set; } = true;
    public string Color1 { get; set; } = "#a1d1ff";
    public string Color2 { get; set; } = "#c7abff";
    public string Color3 { get; set; } = "#7863b3";
    public double Speed { get; set; } = 1.0;
    /**
     * auto — GPU (Win2D/HLSL), при сбое CPU-порт;
     * gpu  — только GPU; cpu — только CPU (например, на RDP/старых драйверах).
     */
    public string Renderer { get; set; } = "auto";
}

public static class ShaderConfig
{
    public const double SpeedMin = 0.5;
    public const double SpeedMax = 2.0;

    static readonly Regex Hex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public static ShaderSettings Current { get; private set; } = new();

    /** Фон изменился — окна перерисовывают слои. */
    public static event Action Changed;

    static string FilePath => Path.Combine(AppContext.BaseDirectory, "shader.json");

    public static ShaderSettings Load()
    {
        var s = new ShaderSettings();
        try
        {
            if (File.Exists(FilePath))
            {
                var parsed = JsonSerializer.Deserialize<ShaderSettings>(
                    File.ReadAllText(FilePath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null)
                {
                    if (parsed.Color1 != null) s.Color1 = parsed.Color1;
                    if (parsed.Color2 != null) s.Color2 = parsed.Color2;
                    if (parsed.Color3 != null) s.Color3 = parsed.Color3;
                    s.Enabled = parsed.Enabled;
                    s.Speed = parsed.Speed;
                    if (parsed.Renderer != null) s.Renderer = parsed.Renderer;
                }
            }
        }
        catch { /* повреждённый файл — значения по умолчанию */ }

        Normalize(s);
        Current = s;
        return s;
    }

    public static void Save(ShaderSettings s)
    {
        Normalize(s);
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(s));
        }
        catch (Exception ex)
        {
            CoreHost.Log("SaveShaderSettings EX: " + ex.Message);
        }
        Current = s;
        Changed?.Invoke();
    }

    public static void Normalize(ShaderSettings s)
    {
        s.Color1 = Hex.IsMatch(s.Color1 ?? "") ? s.Color1.ToLowerInvariant() : "#a1d1ff";
        s.Color2 = Hex.IsMatch(s.Color2 ?? "") ? s.Color2.ToLowerInvariant() : "#c7abff";
        s.Color3 = Hex.IsMatch(s.Color3 ?? "") ? s.Color3.ToLowerInvariant() : "#7863b3";
        if (!double.IsFinite(s.Speed)) s.Speed = 1.0;
        s.Speed = Math.Clamp(s.Speed, SpeedMin, SpeedMax);
        s.Renderer = (s.Renderer ?? "").Trim().ToLowerInvariant() switch
        {
            "cpu" => "cpu",
            "gpu" => "gpu",
            _ => "auto",
        };
    }

    public static (byte r, byte g, byte b) Rgb(string hex)
    {
        if (!Hex.IsMatch(hex ?? "")) return (255, 255, 255);
        return (Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16));
    }
}
