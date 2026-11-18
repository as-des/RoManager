using System.Numerics;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace RoManager.App.Controls;

/**
 * «Стеклянная» поверхность как backdrop-filter: blur() в 2.1.0.
 *
 * Внутри шаблона лежит пустой слой GlassLayer; к нему через композицию
 * подключается CompositionBackdropBrush + Win2D GaussianBlur, поэтому
 * размывается именно то, что находится позади панели (анимированный фон).
 * Тонировка и содержимое рисуются поверх размытого слоя как обычные дети.
 */
public sealed class GlassSurface : ContentControl
{
    /// Радиус размытия в DIP (в legacy — 18 px для панелей).
    public static double BlurRadius { get; set; } = 18;

    Border _layer;
    SpriteVisual _sprite;
    CompositionGeometricClip _clip;
    float _radius;

    public GlassSurface()
    {
        // ContentControl по умолчанию прижимает содержимое влево; стеклянная
        // панель должна растягивать его на всю свою площадь.
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;

        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        SizeChanged += (_, _) => UpdateSize();
    }

    protected override void OnApplyTemplate()
    {
        Detach();
        base.OnApplyTemplate();
        _layer = GetTemplateChild("GlassLayer") as Border;
        Attach();
    }

    void Attach()
    {
        if (_layer == null || _sprite != null) return;

        var compositor = ElementCompositionPreview.GetElementVisual(_layer).Compositor;
        var effect = new GaussianBlurEffect
        {
            BlurAmount = (float)BlurRadius,
            BorderMode = EffectBorderMode.Hard,
            Optimization = EffectOptimization.Balanced,
            Source = new CompositionEffectSourceParameter("backdrop"),
        };

        var brush = compositor.CreateEffectFactory(effect).CreateBrush();
        brush.SetSourceParameter("backdrop", compositor.CreateBackdropBrush());

        _sprite = compositor.CreateSpriteVisual();
        _sprite.Brush = brush;
        _radius = (float)CornerRadius.TopLeft;
        ElementCompositionPreview.SetElementChildVisual(_layer, _sprite);
        UpdateSize();
    }

    void Detach()
    {
        if (_layer != null)
        {
            try { ElementCompositionPreview.SetElementChildVisual(_layer, null); } catch { }
        }
        _sprite?.Dispose();
        _sprite = null;
        _clip = null;
    }

    void UpdateSize()
    {
        if (_sprite == null || _layer == null) return;

        var size = _layer.ActualSize;
        if (size.X < 1 || size.Y < 1) return;

        _sprite.Size = size;

        // Скругляем размытый слой по радиусу панели, иначе видны углы.
        var compositor = _sprite.Compositor;
        var geometry = compositor.CreateRoundedRectangleGeometry();
        geometry.Size = size;
        geometry.CornerRadius = new Vector2(_radius);
        _clip = compositor.CreateGeometricClip(geometry);
        _sprite.Clip = _clip;
    }
}
