using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove;

public class CircleSelectGesture : SelectingGestureRecognizer
{
    public static readonly StyledProperty<double> CircleRadiusProperty = AvaloniaProperty.Register<CircleSelectGesture, double>(nameof(CircleRadius), 50);
    
    public static readonly AttachedProperty<ITemplate<Ellipse?>?> CircleDisplayTemplateProperty = AvaloniaProperty.RegisterAttached<BoxSelectGesture, StyledElement, ITemplate<Ellipse?>?>(nameof(CircleDisplayTemplate), inherits: true);
    public static ITemplate<Ellipse?>? GetCircleDisplayTemplate(StyledElement element) => element.GetValue(CircleDisplayTemplateProperty);
    public static void SetCircleDisplayTemplate(StyledElement element, ITemplate<Ellipse?>? template) => element.SetValue(CircleDisplayTemplateProperty, template);
    
    private Ellipse _indicatorCircle;
    private bool _hoverInitialized;

    static CircleSelectGesture()
    {
        CircleDisplayTemplateProperty.Changed.AddClassHandler<CircleSelectGesture>((g, _) => g.OnCircleDisplayTemplateChanged());
        CircleRadiusProperty.Changed.AddClassHandler<CircleSelectGesture>((g, e) => g.OnCircleRadiusChanged(e));
    }

    public CircleSelectGesture()
    {
        _indicatorCircle = null!;
        OnCircleDisplayTemplateChanged();
        AutoDeselectDuringGesture = false;
    }

    public double CircleRadius
    {
        get => GetValue(CircleRadiusProperty);
        set => SetValue(CircleRadiusProperty, value);
    }

    public ITemplate<Ellipse?>? CircleDisplayTemplate
    {
        get => GetValue(CircleDisplayTemplateProperty);
        set => SetValue(CircleDisplayTemplateProperty, value);
    }

    protected override void OnHoverMove(PointerEventArgs e)
    {
        if (!_hoverInitialized)
        {
            _hoverInitialized = true;
            DrawingCanvas?.Children.Add(_indicatorCircle);
            DrawingCanvas?.PointerWheelChanged += DrawingCanvasOnPointerWheelChanged;
        }
        
        Canvas.SetLeft(_indicatorCircle, e.GetPosition(DrawingCanvas).X - CircleRadius);
        Canvas.SetTop(_indicatorCircle, e.GetPosition(DrawingCanvas).Y - CircleRadius);
    }

    private void DrawingCanvasOnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        CircleRadius *= Math.Exp(e.Delta.Y * 0.2);
        e.Handled = true;
    }

    protected override Geometry? CreateUpdatedSelectionGeometry(PointerEventArgs mostRecentArgs)
    {
        return new EllipseGeometry(
            new Rect(mostRecentArgs.GetPosition(DrawingCanvas) - new Point(CircleRadius, CircleRadius),
                new Size(CircleRadius * 2, CircleRadius * 2)));
    }

    protected override void Cleanup()
    {
        DrawingCanvas?.Children.Remove(_indicatorCircle);
        DrawingCanvas?.PointerWheelChanged -= DrawingCanvasOnPointerWheelChanged;
        _hoverInitialized = false;
    }

    private void OnCircleDisplayTemplateChanged()
    {
        _indicatorCircle = CircleDisplayTemplate?.Build() ?? new Ellipse
        {
            Width = CircleRadius * 2,
            Height = CircleRadius * 2,
            Stroke = Brushes.Red,
            StrokeThickness = 3,
        };
    }

    private void OnCircleRadiusChanged(AvaloniaPropertyChangedEventArgs eventArgs)
    {
        var (oldValue, newValue) = eventArgs.GetOldAndNewValue<double>();
        
        _indicatorCircle.Width = newValue * 2;
        _indicatorCircle.Height = newValue * 2;
        Canvas.SetLeft(_indicatorCircle, Canvas.GetLeft(_indicatorCircle) + oldValue - newValue);
        Canvas.SetTop(_indicatorCircle, Canvas.GetTop(_indicatorCircle) + oldValue - newValue);
    }
}