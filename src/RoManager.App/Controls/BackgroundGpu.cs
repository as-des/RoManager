using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;using RoManager.Core;
using Windows.Foundation;

namespace RoManager.App.Controls;

/**
 * GPU-версия анимированного фона.
 *
 * HLSL — дословный порт GLSL из ShaderBackground.tsx (2.1.0): три аддитивные
 * органические волны (simplex noise + подтёки) и виньетка. Считается
 * пиксельным шейдером на видеокарте через Win2D PixelShaderEffect, кадры
 * тикают в game loop CanvasAnimatedControl, поэтому UI-поток и ЦП кадром
 * не заняты вообще.
 *
 * Шейдер компилируется на старте через d3dcompiler_47.dll (есть в любой
 * Windows 10/11), fxc в сборочной машине не нужен.
 *
 * Рендер идёт в CanvasRenderTarget в 1/3 разрешения (как в 2.1.0) и
 * растягивается на фон билинейно — визуал тот же, стоимость мизерная.
 */
static class BackgroundGpu
{
    const double RenderScale = 3.0;

    public static bool Ready { get; private set; }
    public static bool Failed { get; private set; }

    /** GPU не смог — вызывающий переключается на CPU-версию. */
    public static event Action OnFailed;

    static CanvasAnimatedControl _canvas;
    static PixelShaderEffect _effect;
    static CanvasRenderTarget _target;
    static CanvasBitmap _source;
    static int _w, _h;

    static double _time;
    static double _lastTotal;
    static double _speed = 1;
    static bool _paused;
    static bool _visible = true;
    static Vector3[] _colors = new Vector3[3];

    /* ---------- подключение ---------- */

    public static bool Attach(CanvasAnimatedControl canvas)
    {
        if (canvas == null) return false;
        if (_canvas == canvas) return Ready;

        if (ShaderConfig.Current.Renderer == "cpu")
        {
            CoreHost.Log("shader: GPU отключён настройкой renderer=cpu");
            return false;
        }

        try
        {
            var code = CompileShader(ShaderSource);
            _effect = new PixelShaderEffect(code);
        }
        catch (Exception ex)
        {
            App.Dump("ShaderCompile", ex);
            Failed = true;
            return false;
        }

        _canvas = canvas;
        _speed = Math.Max(0.01, ShaderConfig.Current.Speed);
        _colors = BuildColors(ShaderConfig.Current);

        canvas.CreateResources += OnCreateResources;
        canvas.Draw += OnDraw;
        canvas.SizeChanged += (_, _) => ScheduleResize();
        canvas.Visibility = _visible ? Visibility.Visible : Visibility.Collapsed;
        canvas.Paused = _paused;

        // Фон медленный и мягкий: 30 кадров достаточно, а стоимость кадра
        // вдвое ниже, чем на 60. Движение остаётся плавным.
        canvas.IsFixedTimeStep = true;
        canvas.TargetElapsedTime = TimeSpan.FromMilliseconds(33);

        ShaderConfig.Changed += ApplySettings;
        Ready = true;
        return true;
    }

    public static void Detach()
    {
        if (_canvas == null) return;
        ShaderConfig.Changed -= ApplySettings;
        _canvas.CreateResources -= OnCreateResources;
        _canvas.Draw -= OnDraw;
        _target?.Dispose();
        _source?.Dispose();
        _effect?.Dispose();
        _canvas = null;
        _target = null;
        _source = null;
        _effect = null;
        _w = _h = 0;
        Ready = false;
    }

    public static void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        if (_canvas == null) return;
        _canvas.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _canvas.Paused = _paused || !visible;
    }

    public static void SetActive(bool active)
    {
        _paused = !active;
        if (_canvas != null) _canvas.Paused = _paused || !_visible;
    }

    static void ApplySettings()
    {
        _speed = Math.Max(0.01, ShaderConfig.Current.Speed);
        _colors = BuildColors(ShaderConfig.Current);
    }

    static Vector3[] BuildColors(ShaderSettings s) => new[]
    {
        ToVector(ShaderConfig.Rgb(s.Color1)),
        ToVector(ShaderConfig.Rgb(s.Color2)),
        ToVector(ShaderConfig.Rgb(s.Color3)),
    };

    static Vector3 ToVector((byte r, byte g, byte b) c) =>
        new(c.r / 255f, c.g / 255f, c.b / 255f);

    /* ---------- кадры ---------- */

    static void OnCreateResources(ICanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args) =>
        ScheduleResize();

    static void ScheduleResize()
    {
        _w = _h = 0;
    }

    static void OnDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        if (Failed || _effect == null) return;

        try
        {
            var size = sender.Size;
            if (size.Width < 2 || size.Height < 2) return;

            int pw = Math.Max(4, (int)Math.Round(size.Width / RenderScale));
            int ph = Math.Max(4, (int)Math.Round(size.Height / RenderScale));

            if (_target == null || _w != pw || _h != ph)
            {
                _target?.Dispose();
                _source?.Dispose();
                _target = new CanvasRenderTarget(_canvas, pw, ph, 96);
                _w = pw;
                _h = ph;
            }

            double total = args.Timing.TotalTime.TotalSeconds;
            double dt = total - _lastTotal;
            _lastTotal = total;
            if (dt < 0 || dt > 0.25) dt = 0;
            _time += dt * _speed;

            _effect.Properties["Time"] = (float)_time;
            _effect.Properties["Resolution"] = new Vector2(pw, ph);
            _effect.Properties["Color1"] = _colors[0];
            _effect.Properties["Color2"] = _colors[1];
            _effect.Properties["Color3"] = _colors[2];

            using (var ds = _target.CreateDrawingSession())
                ds.DrawImage(_effect, new Rect(0, 0, pw, ph), new Rect(0, 0, pw, ph));

            args.DrawingSession.DrawImage(_target, new Rect(0, 0, size.Width, size.Height),
                new Rect(0, 0, pw, ph), 1, CanvasImageInterpolation.Linear);
        }
        catch (Exception ex)
        {
            App.Dump("ShaderDraw", ex);
            Failed = true;
            Ready = false;
            OnFailed?.Invoke();
        }
    }

    /* ---------- компиляция HLSL ---------- */

    static byte[] CompileShader(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        // ps_4_0 — профиль, который принимает Direct2D.
        int hr = D3DCompile(bytes, (UIntPtr)(uint)bytes.Length, "Background.hlsl",
            IntPtr.Zero, IntPtr.Zero, "main", "ps_4_0", 0, 0, out var code, out var errors);

        string errorText = null;
        if (errors != null)
        {
            try { errorText = ReadBlob(errors); } catch { }
            Marshal.ReleaseComObject(errors);
        }

        if (hr != 0 || code == null)
        {
            CoreHost.Log("shader compile failed: 0x" + hr.ToString("X8") + "\n" + errorText);
            throw new InvalidOperationException("HLSL compile failed: " + (errorText ?? hr.ToString("X8")));
        }

        try { return GetBlobBytes(code); }
        finally { Marshal.ReleaseComObject(code); }
    }

    static byte[] GetBlobBytes(ID3DBlob blob)
    {
        var ptr = blob.GetBufferPointer();
        int size = (int)blob.GetBufferSize().ToUInt64();
        var bytes = new byte[size];
        Marshal.Copy(ptr, bytes, 0, size);
        return bytes;
    }

    static string ReadBlob(ID3DBlob blob) =>
        Encoding.UTF8.GetString(GetBlobBytes(blob)).TrimEnd('\0', '\r', '\n', ' ');

    [DllImport("d3dcompiler_47.dll", EntryPoint = "D3DCompile", CharSet = CharSet.Ansi, ExactSpelling = true)]
    static extern int D3DCompile(
        byte[] pSrcData, UIntPtr SrcDataSize,
        [MarshalAs(UnmanagedType.LPStr)] string pSourceName,
        IntPtr pDefines, IntPtr pInclude,
        [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint,
        [MarshalAs(UnmanagedType.LPStr)] string pTarget,
        uint Flags1, uint Flags2,
        out ID3DBlob ppCode, out ID3DBlob ppErrorMsgs);

    [ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ID3DBlob
    {
        // Возвращают значение напрямую (не HRESULT) — иначе маршалинг
        // превращает вызов в HRESULT-метод и указатель теряется.
        [PreserveSig] IntPtr GetBufferPointer();
        [PreserveSig] UIntPtr GetBufferSize();
    }

    /* ---------- HLSL: порт GLSL 1:1 ---------- */

    const string ShaderSource = @"
// Процедурный фон RoManager: порт GLSL из ShaderBackground.tsx (2.1.0).
// Без входных текстур: координата берётся из SV_Position целевого буфера.

float Time;
float2 Resolution;
float3 Color1;
float3 Color2;
float3 Color3;

static const float K1 = 0.211324865405187;
static const float K2 = 0.366025403784439;
static const float K3 = -0.577350269189626;
static const float K4 = 0.024390243902439;   // 1 / 41

float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float2 mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float3 permute(float3 x) { return mod289(((x * 34.0) + 1.0) * x); }

float snoise(float2 v)
{
    float2 i = floor(v + dot(v, float2(K2, K2)));
    float2 x0 = v - i + dot(i, float2(K1, K1));

    float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);

    float4 x12 = x0.xyxy + float4(K1, K1, K3, K3);
    x12.xy -= i1;

    i = mod289(i);

    float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0)) + i.x + float3(0.0, i1.x, 1.0));

    float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
    m = m * m;
    m = m * m;

    float3 x = 2.0 * frac(p * K4) - 1.0;
    float3 h = abs(x) - 0.5;
    float3 ox = floor(x + 0.5);
    float3 a0 = x - ox;

    m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);

    float3 g;
    g.x = a0.x * x0.x + h.x * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;

    return 130.0 * dot(m, g);
}

float wave_smoothstep(float edge0, float edge1, float x)
{
    float t = saturate((x - edge0) / (edge1 - edge0));
    return t * t * (3.0 - 2.0 * t);
}

float getOrganicWave(float2 st, float time,
                     float freq, float speed,
                     float rippleAmp, float morphAmp,
                     float dripStrength, float fadeRate, float thicknessPower)
{
    float morph = snoise(float2(st.x * 1.5, st.y * 0.3 - time * 0.08)) * morphAmp;
    float ripple = snoise(float2(st.x * 12.0, st.y * 3.0 - time * 0.3)) * rippleAmp;

    float dripMask = snoise(float2(st.x * 3.0, st.y * 0.8 - time * 0.15));
    dripMask = wave_smoothstep(0.3, 0.9, dripMask);

    float dripStretchX = dripMask * dripStrength * 0.3;
    float dripLiftY = dripMask * dripStrength * 0.5;

    float distortedX = st.x + morph + ripple + dripStretchX;
    float rawLine = sin(distortedX * freq + time * speed + dripLiftY);

    float softness = lerp(thicknessPower, thicknessPower * 0.5, dripMask * 0.7);
    float intensity = pow(abs(rawLine), softness);

    float fade = exp(-st.y * fadeRate);
    fade += dripMask * dripStrength * 0.15 * exp(-st.y * fadeRate * 0.5);

    return intensity * clamp(fade, 0.0, 1.0);
}

float4 main(float4 pos : SV_Position) : SV_Target
{
    float2 st = pos.xy / Resolution;
    st.x *= Resolution.x / Resolution.y;

    // gl_FragCoord в GLSL отсчитывается снизу — в Direct2D сверху,
    // поэтому инвертируем Y, чтобы картинка совпала с 2.1.0.
    st.y = 1.0 - st.y;

    float3 finalColor = float3(0.0, 0.0, 0.0);

    float wave1 = getOrganicWave(st, Time, 25.0, 0.4, 0.015, 0.08, 0.6, 5.0, 6.0);
    finalColor += Color1 * wave1 * 0.55;

    float wave2 = getOrganicWave(st, Time, 15.0, 0.6, 0.008, 0.12, 0.4, 8.0, 10.0);
    finalColor += Color2 * wave2 * 0.45;

    float wave3 = getOrganicWave(st, Time, 10.0, 0.25, 0.02, 0.15, 0.3, 12.0, 8.0);
    finalColor += Color3 * wave3 * 0.2;

    float dist = distance(st, float2(0.5, 0.5 * (Resolution.y / Resolution.x)));
    float vignette = 1.0 - wave_smoothstep(0.5, 1.0, dist);
    finalColor *= lerp(0.7, 1.0, vignette);

    return float4(finalColor, 1.0);
}
";
}
