using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace RoManager.App.Controls;

/**
 * «Стеклянная» панель: полупрозрачная тонировка поверх размытого фона.
 *
 * Раньше сюда подключался CompositionBackdropBrush с Win2D-размытием.
 * В WinUI 3 такой слой захватывает уже отрисованное содержимое окна, из-за
 * чего при кликах и ресайзе появлялись «двойные» панели, а после смены
 * размера старый кадр оставался непрозрачным. Теперь размывается сам фон
 * (BackgroundGpu/BackgroundShader), а панель — только тонировка и рамка.
 */
public sealed class GlassSurface : ContentControl
{
    public GlassSurface()
    {
        // ContentControl по умолчанию прижимает содержимое влево; стеклянная
        // панель должна растягивать его на всю свою площадь.
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }
}
