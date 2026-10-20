using DrawingRectangle = System.Drawing.Rectangle;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShapeRectangle = System.Windows.Shapes.Rectangle;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace HornMelodyTimer.Views;

public sealed class RegionSelectionWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly ShapeRectangle _selection = new()
    {
        Stroke = MediaBrushes.Yellow,
        StrokeThickness = 2,
        Fill = new SolidColorBrush(MediaColor.FromArgb(45, 255, 230, 0)),
        Visibility = Visibility.Collapsed
    };
    private System.Windows.Point _start;
    private bool _dragging;

    public event Action<DrawingRectangle>? RegionSelected;

    public RegionSelectionWindow()
    {
        Title = "框選音符提示列";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(MediaColor.FromArgb(90, 0, 0, 0));
        Topmost = true;
        ShowInTaskbar = false;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Cursor = System.Windows.Input.Cursors.Cross;
        Content = _canvas;
        _canvas.Children.Add(_selection);

        var help = new TextBlock
        {
            Text = "框選提示列音符可能出現的範圍（最多四列），避免其他 HUD 區域；按 Esc 取消",
            Foreground = MediaBrushes.White,
            Background = new SolidColorBrush(MediaColor.FromArgb(180, 40, 25, 20)),
            FontSize = 18,
            Padding = new Thickness(12)
        };
        _canvas.Children.Add(help);
        Canvas.SetLeft(help, 24);
        Canvas.SetTop(help, 24);

        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
                Close();
        };
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        _dragging = true;
        _selection.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging)
            return;
        var current = e.GetPosition(this);
        Canvas.SetLeft(_selection, Math.Min(_start.X, current.X));
        Canvas.SetTop(_selection, Math.Min(_start.Y, current.Y));
        _selection.Width = Math.Abs(current.X - _start.X);
        _selection.Height = Math.Abs(current.Y - _start.Y);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;
        _dragging = false;
        ReleaseMouseCapture();
        var first = PointToScreen(_start);
        var end = PointToScreen(e.GetPosition(this));
        var region = DrawingRectangle.FromLTRB(
            (int)Math.Round(Math.Min(first.X, end.X)),
            (int)Math.Round(Math.Min(first.Y, end.Y)),
            (int)Math.Round(Math.Max(first.X, end.X)),
            (int)Math.Round(Math.Max(first.Y, end.Y)));
        if (region.Width > 8 && region.Height > 8)
            RegionSelected?.Invoke(region);
        Close();
    }
}
