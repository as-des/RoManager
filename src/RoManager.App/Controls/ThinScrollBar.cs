using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace RoManager.App.Controls;

/**
 * Тонкий ползунок как в 2.1.0: 8 px, скруглённый, без трека и стрелок.
 * Прикрепляется к ScrollViewer: обновляется при прокрутке/ресайзе,
 * перетаскивается мышью, клик по дорожке перематывает.
 */
public sealed class ThinScrollBar : Grid
{
    readonly Border _thumb;
    ScrollViewer _viewer;
    bool _dragging;
    double _startY;
    double _startOffset;
    double _trackHeight;
    double _thumbHeight;

    Brush _normalBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x3A, 0x3A));

    public Brush ThumbBrush
    {
        get => _normalBrush;
        set
        {
            _normalBrush = value;
            if (!_hover) _thumb.Background = value;
        }
    }

    bool _hover;

    public ThinScrollBar()
    {
        Width = 8;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        _thumb = new Border
        {
            Width = 8,
            CornerRadius = new CornerRadius(4),
            Background = _normalBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };
        Children.Add(_thumb);

        _thumb.PointerEntered += (_, _) =>
        {
            _hover = true;
            _thumb.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x4A, 0x4A, 0x4A));
        };
        _thumb.PointerExited += (_, _) =>
        {
            _hover = false;
            _thumb.Background = _normalBrush;
        };
        _thumb.PointerPressed += OnThumbPressed;
        _thumb.PointerMoved += OnThumbMoved;
        _thumb.PointerReleased += OnThumbReleased;
        _thumb.PointerCaptureLost += (_, _) => _dragging = false;

        Tapped += OnTrackTapped;
        SizeChanged += (_, _) => Update();
    }

    public void Attach(ScrollViewer viewer)
    {
        if (_viewer != null) Detach();
        _viewer = viewer;
        _viewer.ViewChanged += (_, _) => Update();
        _viewer.SizeChanged += (_, _) => Update();
        _viewer.Loaded += (_, _) => Update();
        Update();
    }

    void Detach()
    {
        _viewer = null;
    }

    void Update()
    {
        if (_viewer == null) return;

        double viewport = _viewer.ViewportHeight;
        double extent = _viewer.ExtentHeight;
        _trackHeight = ActualHeight;

        if (extent <= viewport + 0.5 || _trackHeight <= 1)
        {
            _thumb.Visibility = Visibility.Collapsed;
            return;
        }

        double ratio = Math.Clamp(viewport / extent, 0, 1);
        _thumbHeight = Math.Max(24, Math.Round(_trackHeight * ratio));
        double maxOffset = extent - viewport;
        double travel = Math.Max(0, _trackHeight - _thumbHeight);
        double y = maxOffset > 0 ? _viewer.VerticalOffset / maxOffset * travel : 0;

        _thumb.Height = _thumbHeight;
        _thumb.Margin = new Thickness(0, y, 0, 0);
        _thumb.Visibility = Visibility.Visible;
    }

    void OnThumbPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_viewer == null) return;
        _dragging = true;
        _startY = e.GetCurrentPoint(this).Position.Y;
        _startOffset = _viewer.VerticalOffset;
        _thumb.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    void OnThumbMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging || _viewer == null) return;
        double y = e.GetCurrentPoint(this).Position.Y;
        double travel = Math.Max(1, _trackHeight - _thumbHeight);
        double maxOffset = Math.Max(0, _viewer.ExtentHeight - _viewer.ViewportHeight);
        double offset = _startOffset + (y - _startY) / travel * maxOffset;
        _viewer.ChangeView(null, offset, null, true);
        e.Handled = true;
    }

    void OnThumbReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _thumb.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    void OnTrackTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_viewer == null || _dragging) return;
        double y = e.GetPosition(this).Y;
        double travel = Math.Max(1, _trackHeight - _thumbHeight);
        double maxOffset = Math.Max(0, _viewer.ExtentHeight - _viewer.ViewportHeight);
        double offset = (y - _thumbHeight / 2) / travel * maxOffset;
        _viewer.ChangeView(null, Math.Clamp(offset, 0, maxOffset), null, true);
    }
}
