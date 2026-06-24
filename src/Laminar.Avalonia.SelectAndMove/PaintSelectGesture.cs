using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove;

public class PaintSelectGesture : SelectingGestureRecognizer
{
    public static readonly StyledProperty<double> CircleRadiusProperty = AvaloniaProperty.Register<PaintSelectGesture, double>(nameof(CircleRadius), 50);
    
    public static readonly AttachedProperty<ITemplate<Ellipse?>?> IndicatorCircleTemplateProperty = AvaloniaProperty.RegisterAttached<BoxSelectGesture, StyledElement, ITemplate<Ellipse?>?>(nameof(IndicatorCircleTemplate), inherits: true);
    public static ITemplate<Ellipse?>? GetIndicatorCircleTemplate(StyledElement element) => element.GetValue(IndicatorCircleTemplateProperty);
    public static void SetIndicatorCircleTemplate(StyledElement element, ITemplate<Ellipse?>? template) => element.SetValue(IndicatorCircleTemplateProperty, template);
    
    private Ellipse _indicatorCircle;

    static PaintSelectGesture()
    {
        IndicatorCircleTemplateProperty.Changed.AddClassHandler<PaintSelectGesture>((g, _) => g.OnCircleDisplayTemplateChanged());
        CircleRadiusProperty.Changed.AddClassHandler<PaintSelectGesture>((g, e) => g.OnCircleRadiusChanged(e));
    }

    public PaintSelectGesture()
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

    public ITemplate<Ellipse?>? IndicatorCircleTemplate
    {
        get => GetValue(IndicatorCircleTemplateProperty);
        set => SetValue(IndicatorCircleTemplateProperty, value);
    }

    protected override void OnHoverStart(PointerEventArgs e)
    {
        base.OnHoverStart(e);
        Canvas.SetLeft(_indicatorCircle, e.GetPosition(DrawingCanvas).X - CircleRadius);
        Canvas.SetTop(_indicatorCircle, e.GetPosition(DrawingCanvas).Y - CircleRadius);
        DrawingCanvas?.Children.Add(_indicatorCircle);
        DrawingCanvas?.PointerWheelChanged += DrawingCanvasOnPointerWheelChanged;
    }

    protected override void OnHoverMove(PointerEventArgs e)
    {
        base.OnHoverMove(e);
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
        base.Cleanup();
        DrawingCanvas?.Children.Remove(_indicatorCircle);
        DrawingCanvas?.PointerWheelChanged -= DrawingCanvasOnPointerWheelChanged;
    }

    private void OnCircleDisplayTemplateChanged()
    {
        _indicatorCircle = IndicatorCircleTemplate?.Build() ?? new Ellipse
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