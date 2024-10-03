using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public class ZoomGesture : GestureRecognizer
{
    public static readonly StyledProperty<double> ZoomSpeedProperty = 
        AvaloniaProperty.Register<ZoomGesture, double>(nameof(ZoomSpeed), 1.0);

    public static readonly StyledProperty<double> CurrentZoomProperty =
        AvaloniaProperty.Register<ZoomGesture, double>(nameof(CurrentZoom), 1.0);

    public static readonly StyledProperty<Control?> ScrollWheelListenerProperty =
        AvaloniaProperty.Register<ZoomGesture, Control?>(nameof(ScrollWheelListener), null);

    private readonly Dictionary<Visual, BoundsChangedObserver> _boundsTrackers = new();

    private Point? _zoomCenter;

    static ZoomGesture()
    {
        ScrollWheelListenerProperty.Changed.AddClassHandler<ZoomGesture>((zoomGesture, changedArgs) =>
        {  
            if (changedArgs.OldValue is Control oldControl)
            {
                oldControl.PointerWheelChanged -= zoomGesture.Target_PointerWheelChanged;
            }

            if (changedArgs.NewValue is Control newControl)
            {
                newControl.PointerWheelChanged += zoomGesture.Target_PointerWheelChanged;
            }
        });

        CurrentZoomProperty.Changed.AddClassHandler<ZoomGesture>((o, e) =>
        {
            if (e.NewValue is not double newZoom 
            || e.OldValue is not double oldZoom 
            || o.Target is not Visual targetVisual)
            {
                return;
            }

            o.ZoomByDelta(newZoom / oldZoom, o._zoomCenter ?? targetVisual.Bounds.Center - targetVisual.Bounds.TopLeft);
        });
    }
    
    public double ZoomSpeed
    {
        get => GetValue(ZoomSpeedProperty);
        set => SetValue(ZoomSpeedProperty, value);
    }

    /// <summary>
    /// The current zoom defined by the gesture, theoretically ranges from 0 to infinity. 1.0 is no zoom.
    /// </summary>
    public double CurrentZoom
    {
        get => GetValue(CurrentZoomProperty);
        set => SetValue(CurrentZoomProperty, value);
    }

    public Control? ScrollWheelListener
    {
        get => GetValue(ScrollWheelListenerProperty);
        set => SetValue(ScrollWheelListenerProperty, value);
    }

    protected override void PointerPressed(PointerPressedEventArgs e)
    {
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
    }

    protected override void PointerCaptureLost(IPointer pointer)
    {
    }

    protected override void PointerReleased(PointerReleasedEventArgs e)
    {
    }

    private void Target_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (Target is not Visual targetVisual)
        {
            return;
        }

        double zoomAmount = Math.Exp(ZoomSpeed * e.Delta.Y / 5);
        _zoomCenter = e.GetPosition(targetVisual);
        CurrentZoom *= zoomAmount;
        _zoomCenter = null;

        targetVisual.InvalidateVisual();
    }

    private void ZoomByDelta(double delta, Point positionInParent)
    {
        if (Target is not Panel targetPanel)
        {
            return;
        }

        foreach (Control control in targetPanel.Children)
        {
            control.RenderTransform ??= new MatrixTransform(Matrix.Identity);

            Matrix myScaleMatrix2 = GetTransform(control, targetPanel, positionInParent, delta);

            control.RenderTransform = new MatrixTransform(myScaleMatrix2 * control.RenderTransform.Value);

            // If the control's location changes and it has zoom, it's render transform will need a pan adding to it.
            if (!_boundsTrackers.ContainsKey(control))
            {
                BoundsChangedObserver newObserver = new() { TrackedVisual = control };
                _boundsTrackers.Add(control, newObserver);
                control.GetPropertyChangedObservable(Visual.BoundsProperty).Subscribe(newObserver);
            }
        }

        targetPanel.InvalidateVisual();
    }

    public static Matrix GetTransform(Control control, Visual parent, Point centerInParent, double scale)
    {
        Point positionInLocal = centerInParent * parent.TransformToVisual(control)!.Value;
        return ScaleAt(scale, positionInLocal.X - control.Bounds.Width / 2, positionInLocal.Y - control.Bounds.Height / 2);
    }

    private static Matrix ScaleAt(double scale, double centerX, double centerY)
    {
        return new Matrix(scale, 0, 0, scale, centerX - scale * centerX, centerY - scale * centerY);
    }

    private class BoundsChangedObserver : IObserver<AvaloniaPropertyChangedEventArgs>
    {
        public Visual? TrackedVisual { get; init; }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(AvaloniaPropertyChangedEventArgs propertyChangedArgs)
        {
            ArgumentNullException.ThrowIfNull(TrackedVisual);
            TrackedVisual.RenderTransform ??= new MatrixTransform();

            if (propertyChangedArgs.NewValue is not Rect newBounds 
                || propertyChangedArgs.OldValue is not Rect oldBounds)
            {
                return;
            }

            Point locationDelta = newBounds.TopLeft - oldBounds.TopLeft;
            Matrix transformChange = new(1.0, 0.0, 0.0, 1.0, locationDelta.X * (1 - 1 / TrackedVisual.RenderTransform.Value.M11), locationDelta.Y * (1 - 1 / TrackedVisual.RenderTransform.Value.M22));
            TrackedVisual.RenderTransform = new MatrixTransform(transformChange * TrackedVisual.RenderTransform.Value);
        }
    }
}
