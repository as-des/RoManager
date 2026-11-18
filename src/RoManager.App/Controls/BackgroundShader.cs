using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using RoManager.Core;

namespace RoManager.App.Controls;

/**
 * Анимированный фон — точный CPU-порт GLSL из ShaderBackground.tsx (2.1.0).
 *
 * В оригинале WebGL рисовал кадр в 1/3 разрешения каждый animation frame:
 * три аддитивные органические волны (simplex noise + подтёки) и виньетка.
 * Здесь та же математика считается на CPU в низком разрешении и выводится
 * через WriteableBitmap, поэтому картинка живая и совпадает по палитре/движению.
 *
 * Настройки берутся из shader.json: enabled, color1..color3, speed.
 */
public static class BackgroundShader
{
    // Ограничение стоимости кадра: ~48k пикселей независимо от размера окна.
    // Фон мягкий и растягивается на весь экран, поэтому потерь не видно,
    // а нагрузка на CPU падает примерно вдвое.
    const double MaxRenderPixels = 48_000;
    const double BaseScale = 3.5;
    const int FrameIntervalMs = 80;

    static readonly ParallelOptions RenderOptions = new()
    {
        MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
    };

    struct Wave
    {
        public float Freq, Speed, Ripple, Morph, Drip, Fade, Thickness, Weight;
        public Wave(float freq, float speed, float ripple, float morph, float drip, float fade, float thickness, float weight)
        {
            Freq = freq; Speed = speed; Ripple = ripple; Morph = morph;
            Drip = drip; Fade = fade; Thickness = thickness; Weight = weight;
        }
    }

    // 1:1 параметры трёх волн из getOrganicWave().
    static readonly Wave[] Waves =
    {
        new(25f, 0.4f, 0.015f, 0.08f, 0.6f, 5f, 6f, 0.55f),
        new(15f, 0.6f, 0.008f, 0.12f, 0.4f, 8f, 10f, 0.45f),
        new(10f, 0.25f, 0.02f, 0.15f, 0.3f, 12f, 8f, 0.2f),
    };

    static Image _target;
    static WriteableBitmap _bitmap;
    static DispatcherTimer _timer;
    static byte[] _buffer;
    static int _w, _h;
    static int _busy;

    static readonly Stopwatch _clock = Stopwatch.StartNew();
    static double _lastTick;
    static float _time;
    static bool _active = true;
    static ShaderSettings _settings = ShaderConfig.Current;
    static Vector3[] _colors = BuildColors(ShaderConfig.Current);

    public static bool IsAttached => _target != null;

    public static void Attach(Image target)
    {
        if (_target != null || target == null) return;
        _target = target;
        _settings = ShaderConfig.Current;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameIntervalMs) };
        _timer.Tick += (_, _) => Tick();
        _lastTick = _clock.Elapsed.TotalSeconds;

        target.SizeChanged += (_, _) =>
        {
            Resize();
            Schedule(force: true);
        };
        ShaderConfig.Changed += () =>
        {
            _settings = ShaderConfig.Current;
            _colors = BuildColors(_settings);
            ApplyEnabled();
        };

        Resize();
        ApplyEnabled();
    }

    /** Окно свернули/потеряло фокус — не тратим CPU на невидимый фон. */
    public static void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        ApplyEnabled();
    }

    static void ApplyEnabled()
    {
        if (_target == null) return;
        bool on = _settings.Enabled && _active;
        _target.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

        if (on)
        {
            _lastTick = _clock.Elapsed.TotalSeconds;
            if (!_timer.IsEnabled) _timer.Start();
            Schedule(force: true);
        }
        else if (_timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    static Vector3[] BuildColors(ShaderSettings s) => new[]
    {
        ToVector(ShaderConfig.Rgb(s.Color1)),
        ToVector(ShaderConfig.Rgb(s.Color2)),
        ToVector(ShaderConfig.Rgb(s.Color3)),
    };

    /* ---------- размеры ---------- */

    static void Resize()
    {
        if (_target == null) return;
        var size = _target.ActualSize;
        if (size.X < 1 || size.Y < 1)
        {
            if (_target.Parent is FrameworkElement parent && parent.ActualSize.X >= 1 && parent.ActualSize.Y >= 1)
                size = parent.ActualSize;
            else
                return;
        }

        double scale = Math.Max(BaseScale, Math.Sqrt(size.X * size.Y / MaxRenderPixels));
        int w = Math.Max(1, (int)Math.Round(size.X / scale));
        int h = Math.Max(1, (int)Math.Round(size.Y / scale));
        if (w == _w && h == _h) return;

        _w = w;
        _h = h;
        _buffer = new byte[w * h * 4];
        _bitmap = new WriteableBitmap(w, h);
        _target.Source = _bitmap;
    }

    /* ---------- кадры ---------- */

    static void Tick()
    {
        if (_target == null || !_settings.Enabled || !_active) return;
        if (_bitmap == null) Resize();

        double now = _clock.Elapsed.TotalSeconds;
        double dt = Math.Clamp(now - _lastTick, 0, 0.1);
        _lastTick = now;
        _time += (float)(dt * Math.Max(0.01, _settings.Speed));

        Schedule(force: false);
    }

    static void Schedule(bool force)
    {
        if (_target == null || _bitmap == null || _buffer == null) return;
        if (!_settings.Enabled || !_active) return;
        if (!force && Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
        if (force) Interlocked.Exchange(ref _busy, 1);

        int w = _w, h = _h;
        var buffer = _buffer;
        float time = _time;
        var colors = _colors;

        _ = Task.Run(() =>
        {
            try
            {
                RenderFrame(buffer, w, h, time, colors);
            }
            catch (Exception ex)
            {
                App.Dump("ShaderRender", ex);
            }

            _target?.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (_bitmap != null && _w == w && _h == h && ReferenceEquals(buffer, _buffer))
                    {
                        using var stream = _bitmap.PixelBuffer.AsStream();
                        stream.Position = 0;
                        stream.Write(buffer, 0, buffer.Length);
                        _bitmap.Invalidate();
                    }
                }
                catch (Exception ex)
                {
                    App.Dump("ShaderBlit", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _busy, 0);
                }
            });
        });
    }

    static Vector3 ToVector((byte r, byte g, byte b) c) =>
        new(c.r / 255f, c.g / 255f, c.b / 255f);

    static void RenderFrame(byte[] px, int w, int h, float time, Vector3[] colors)
    {
        float aspect = (float)w / h;
        float centerY = 0.5f / aspect;
        var c1 = colors[0];
        var c2 = colors[1];
        var c3 = colors[2];

        Parallel.For(0, h, RenderOptions, y =>
        {
            // gl_FragCoord.y в GLSL отсчитывается снизу — в BMP-строках y сверху, поэтому инвертируем.
            float sy = 1f - (y + 0.5f) / h;
            int row = y * w * 4;
            float dy = sy - centerY;

            for (int x = 0; x < w; x++)
            {
                float sx = (x + 0.5f) / w * aspect;

                float v1 = OrganicWave(sx, sy, time, Waves[0]);
                float v2 = OrganicWave(sx, sy, time, Waves[1]);
                float v3 = OrganicWave(sx, sy, time, Waves[2]);

                float r = c1.X * v1 * Waves[0].Weight
                        + c2.X * v2 * Waves[1].Weight
                        + c3.X * v3 * Waves[2].Weight;
                float g = c1.Y * v1 * Waves[0].Weight
                        + c2.Y * v2 * Waves[1].Weight
                        + c3.Y * v3 * Waves[2].Weight;
                float b = c1.Z * v1 * Waves[0].Weight
                        + c2.Z * v2 * Waves[1].Weight
                        + c3.Z * v3 * Waves[2].Weight;

                // Виньетка: distance(st, vec2(0.5, 0.5 * (h / w))), mix(0.7, 1.0, vignette).
                float dx = sx - 0.5f;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float vignette = 1f - Smoothstep(0.5f, 1f, dist);
                float mul = 0.7f + 0.3f * vignette;

                int p = row + x * 4;
                px[p + 0] = ToByte(b * mul);      // B
                px[p + 1] = ToByte(g * mul);      // G
                px[p + 2] = ToByte(r * mul);      // R
                px[p + 3] = 255;                  // A
            }
        });
    }

    static byte ToByte(float v) => (byte)Math.Clamp((int)(v * 255f + 0.5f), 0, 255);

    static float OrganicWave(float stx, float sty, float time, in Wave w)
    {
        float morph = Snoise(stx * 1.5f, sty * 0.3f - time * 0.08f) * w.Morph;
        float ripple = Snoise(stx * 12f, sty * 3f - time * 0.3f) * w.Ripple;

        float dripMask = Snoise(stx * 3f, sty * 0.8f - time * 0.15f);
        dripMask = Smoothstep(0.3f, 0.9f, dripMask);

        float dripStretchX = dripMask * w.Drip * 0.3f;
        float dripLiftY = dripMask * w.Drip * 0.5f;

        float distortedX = stx + morph + ripple + dripStretchX;
        float rawLine = MathF.Sin(distortedX * w.Freq + time * w.Speed + dripLiftY);

        float softness = Lerp(w.Thickness, w.Thickness * 0.5f, dripMask * 0.7f);
        float intensity = MathF.Pow(MathF.Abs(rawLine), softness);

        float fade = MathF.Exp(-sty * w.Fade);
        fade += dripMask * w.Drip * 0.15f * MathF.Exp(-sty * w.Fade * 0.5f);

        return intensity * Math.Clamp(fade, 0f, 1f);
    }

    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /* ---------- simplex noise (дословный порт vec-функций) ---------- */

    static readonly Vector4 C = new(0.211324865405187f, 0.366025403784439f, -0.577350269189626f, 0.024390243902439f);

    static float Mod289(float x) => x - MathF.Floor(x * (1f / 289f)) * 289f;

    static Vector3 Mod289(Vector3 v) => new(Mod289(v.X), Mod289(v.Y), Mod289(v.Z));

    static Vector3 Permute(Vector3 x) => Mod289((x * 34f + Vector3.One) * x);

    static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;

    static float Snoise(float vx, float vy)
    {
        var v = new Vector2(vx, vy);
        var yy = new Vector2(C.Y, C.Y);
        var xx = new Vector2(C.X, C.X);

        var i = new Vector2(MathF.Floor(v.X + Dot(v, yy)), MathF.Floor(v.Y + Dot(v, yy)));
        var x0 = v - i + new Vector2(Dot(i, xx), Dot(i, xx));

        Vector2 i1 = x0.X > x0.Y ? new Vector2(1f, 0f) : new Vector2(0f, 1f);

        var x12 = new Vector4(x0.X, x0.Y, x0.X, x0.Y) + new Vector4(C.X, C.X, C.Z, C.Z);
        x12.X -= i1.X;
        x12.Y -= i1.Y;

        i = new Vector2(Mod289(i.X), Mod289(i.Y));

        var p = Permute(Permute(new Vector3(i.Y + 0f, i.Y + i1.Y, i.Y + 1f)) +
                        new Vector3(i.X + 0f, i.X + i1.X, i.X + 1f));

        var m = new Vector3(
            MathF.Max(0.5f - Dot(x0, x0), 0f),
            MathF.Max(0.5f - (x12.X * x12.X + x12.Y * x12.Y), 0f),
            MathF.Max(0.5f - (x12.Z * x12.Z + x12.W * x12.W), 0f));
        m *= m;
        m *= m;

        var x = new Vector3(
            2f * Fract(p.X * C.W) - 1f,
            2f * Fract(p.Y * C.W) - 1f,
            2f * Fract(p.Z * C.W) - 1f);
        var h = Vector3.Abs(x) - new Vector3(0.5f);
        var ox = new Vector3(MathF.Floor(x.X + 0.5f), MathF.Floor(x.Y + 0.5f), MathF.Floor(x.Z + 0.5f));
        var a0 = x - ox;

        m *= new Vector3(1.79284291400159f) -
              new Vector3(0.85373472095314f) * (a0 * a0 + h * h);

        var g = new Vector3(
            a0.X * x0.X + h.X * x0.Y,
            a0.Y * x12.X + h.Y * x12.Y,
            a0.Z * x12.Z + h.Z * x12.W);

        return 130f * Vector3.Dot(m, g);
    }

    static float Fract(float x) => x - MathF.Floor(x);
}
