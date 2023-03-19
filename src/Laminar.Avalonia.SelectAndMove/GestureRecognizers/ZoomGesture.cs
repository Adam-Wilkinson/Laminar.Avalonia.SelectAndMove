using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public class ZoomGesture : GestureRecognizerBase
{
    public static readonly StyledProperty<double> ZoomSpeedProperty = 
        AvaloniaProperty.Register<ZoomGesture, double>(nameof(ZoomSpeed), 1.0);

    public static readonly StyledProperty<double> CurrentZoomProperty =
        AvaloniaProperty.Register<ZoomGesture, double>(nameof(CurrentZoom), 1.0);

    private readonly Dictionary<IVisual, BoundsChangedObserver> _boundsTrackers = new();

    private Point? _zoomCenter;

    static ZoomGesture()
    {
        CurrentZoomProperty.Changed.AddClassHandler<ZoomGesture>((o, e) =>
        {
            if (e.NewValue is not double newZoom 
            || e.OldValue is not double oldZoom 
            || o.Target is null)
            {
                return;
            }

            o.ZoomByDelta(newZoom / oldZoom, o._zoomCenter ?? o.Target.Bounds.Center - o.Target.Bounds.TopLeft);
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

    public override void PointerPressed(PointerPressedEventArgs e)
    {
    }

    protected override void TrackedPointerMoved(PointerEventArgs e)
    {
    }

    protected override void PostInitialize()
    {
        Target!.PointerWheelChanged += Target_PointerWheelChanged;
    }

    private void Target_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        double zoomAmount = Math.Exp(ZoomSpeed * e.Delta.Y / 5);
        _zoomCenter = e.GetPosition(Target);
        CurrentZoom *= zoomAmount;
        _zoomCenter = null;
        Point zoomCenter = e.GetPosition(Target);
        // ZoomByDelta(zoomAmount, zoomCenter);

        Target!.InvalidateVisual();
    }

    private void ZoomByDelta(double delta, Point positionInParent)
    {
        if (Target is not IPanel targetPanel)
        {
            return;
        }

        foreach (IControl control in targetPanel.Children)
        {
            control.RenderTransform ??= new MatrixTransform(Matrix.Identity);

            Matrix myScaleMatrix2 = GetTransform(control, Target, positionInParent, delta);

            control.RenderTransform = new MatrixTransform(myScaleMatrix2 * control.RenderTransform.Value);

            // If the control's location changes and it has zoom, it's render transform will need a pan adding to it.
            if (!_boundsTrackers.ContainsKey(control))
            {
                BoundsChangedObserver newObserver = new() { TrackedVisual = control };
                _boundsTrackers.Add(control, newObserver);
                control.GetPropertyChangedObservable(Visual.BoundsProperty).Subscribe(newObserver);
            }
        }

        Target.InvalidateVisual();
    }

    public static Matrix GetTransform(IControl control, IVisual parent, Point centerInParent, double scale)
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
        public IVisual? TrackedVisual { get; init; }

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
