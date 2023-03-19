using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public class PanGesture : GestureRecognizerBase
{
    public static readonly StyledProperty<MouseButton> PanMouseButtonProperty =
        AvaloniaProperty.Register<PanGesture, MouseButton>(nameof(PanMouseButton), MouseButton.Middle);

    PointerEventArgs? _previousPositionArgs;

    public MouseButton PanMouseButton
    {
        get => GetValue(PanMouseButtonProperty);
        set => SetValue(PanMouseButtonProperty, value);
    }

    public Vector CurrentPan { get; private set; } = Vector.Zero;

    public override void PointerPressed(PointerPressedEventArgs e)
    {
        if (ButtonIsPressed(e.GetCurrentPoint(Target).Properties, PanMouseButton) && Target is IPanel targetPanel && targetPanel.Children.Count > 0)
        {
            Track(e.Pointer);
            _previousPositionArgs = e;
        }
    }

    protected override void TrackedPointerMoved(PointerEventArgs e)
    {
        if (Target is not IPanel targetPanel)
        {
            return;
        }

        foreach (IControl control in targetPanel.Children)
        {
            control.RenderTransform ??= new MatrixTransform(Matrix.Identity);
            Vector controlDelta = e.GetPosition(control) - _previousPositionArgs!.GetPosition(control);
            control.RenderTransform = new MatrixTransform(new Matrix(1.0, 0.0, 0.0, 1.0, controlDelta.X, controlDelta.Y) * control.RenderTransform.Value);
            control.InvalidateVisual();
        }

        Target.InvalidateVisual();
        _previousPositionArgs = e;
    }

    private static bool ButtonIsPressed(PointerPointProperties pointerProperties, MouseButton button) => button switch
    {
        MouseButton.Left => pointerProperties.IsLeftButtonPressed,
        MouseButton.Right => pointerProperties.IsRightButtonPressed,
        MouseButton.Middle => pointerProperties.IsMiddleButtonPressed,
        MouseButton.XButton1 => pointerProperties.IsXButton1Pressed,
        MouseButton.XButton2 => pointerProperties.IsXButton2Pressed,
        _ => false,
    };

    public static Matrix Translate(double offsetX, double offsetY)
    {
        return new Matrix(1.0, 0.0, 0.0, 1.0, offsetX, offsetY);
    }
}
