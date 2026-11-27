using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace RoManager.App.Controls;

/**
 * Glass panel: a translucent tint over the blurred background.
 *
 * It used to hook up CompositionBackdropBrush with a Win2D blur. In WinUI 3
 * such a layer captures the already-rendered window content, which caused
 * doubled panels on click and resize and left an opaque stale frame after
 * resizing. Now the background itself is blurred (BackgroundGpu/
 * BackgroundShader) and the panel only draws the tint and the border.
 */
public sealed class GlassSurface : ContentControl
{
    public GlassSurface()
    {
        // ContentControl aligns its content to the left by default; a glass
        // panel must stretch it across its full area.
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }
}
