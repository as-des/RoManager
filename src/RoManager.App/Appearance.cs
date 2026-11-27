using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using RoManager.Core;
using Windows.UI;

namespace RoManager.App;

/**
 * Applies appearance.json to the theme resources.
 *
 * Theme.xaml brushes are recoloured, not replaced: styles and templates already
 * reference them, so the colour updates in every window at once without a
 * restart. The accent from Settings can now be changed.
 */
static class Appearance
{
    public static Color AccentColor
    {
        get
        {
            if (Application.Current?.Resources.TryGetValue("AppPrimary", out var value) == true &&
                value is SolidColorBrush brush)
            {
                return brush.Color;
            }
            var (r, g, b) = AppearanceConfig.Rgb(AppearanceConfig.Current.Accent);
            return Color.FromArgb(0xFF, r, g, b);
        }
    }

    public static void Apply()
    {
        try
        {
            if (Application.Current == null) return;

            var accent = Parse(AppearanceConfig.Current.Accent);

            SetBrush("AppPrimary", accent);
            SetBrush("AppPrimaryHover", Darken(accent, 0.16));
            SetBrush("AppPrimaryPressed", Darken(accent, 0.28));
            SetBrush("AppPrimaryDisabled", Mix(accent, Color.FromArgb(0xFF, 0x10, 0x10, 0x14), 0.62));
            SetBrush("AppPrimaryDisabledText", Mix(accent, Color.FromArgb(0xFF, 0xCC, 0xCC, 0xCC), 0.45));
            SetBrush("AppPrimaryGlow", WithAlpha(accent, 0x26));

            // Standard controls (sliders, checkboxes, focus) take their accent
            // colours from these resources.
            SetColor("SystemAccentColor", accent);
            SetColor("SystemAccentColorLight1", Mix(accent, Colors.White, 0.28));
            SetColor("SystemAccentColorLight2", Mix(accent, Colors.White, 0.52));
            SetColor("SystemAccentColorLight3", Mix(accent, Colors.White, 0.76));
            SetColor("SystemAccentColorDark1", Darken(accent, 0.18));
            SetColor("SystemAccentColorDark2", Darken(accent, 0.32));
            SetColor("SystemAccentColorDark3", Darken(accent, 0.46));
        }
        catch (Exception ex)
        {
            CoreHost.Log("ApplyAppearance EX: " + ex.Message);
        }
    }

    static void SetBrush(string key, Color color)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) &&
            value is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    static void SetColor(string key, Color color) =>
        Application.Current.Resources[key] = color;

    public static Color Parse(string hex)
    {
        var (r, g, b) = AppearanceConfig.Rgb(hex);
        return Color.FromArgb(0xFF, r, g, b);
    }

    static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            0xFF,
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    static Color Darken(Color c, double amount) =>
        Mix(c, Color.FromArgb(0xFF, 0, 0, 0), amount);

    static Color WithAlpha(Color c, byte alpha) =>
        Color.FromArgb(alpha, c.R, c.G, c.B);
}
