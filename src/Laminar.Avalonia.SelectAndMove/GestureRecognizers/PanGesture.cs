using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public class PanGesture : GestureRecognizer
{
    public static readonly StyledProperty<MouseButton> PanMouseButtonProperty =
        AvaloniaProperty.Register<PanGesture, MouseButton>(nameof(PanMouseButton), MouseButton.Middle);

    PointerEventArgs? _previousPositionArgs;
    IPointer? _capturedPointer = null;

    public MouseButton PanMouseButton
    {
        get => GetValue(PanMouseButtonProperty);
        set => SetValue(PanMouseButtonProperty, value);
    }

    public Vector CurrentPan { get; private set; } = Vector.Zero;

    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        if (e.Pointer is Pointer pointer && !pointer.IsGestureRecognitionSkipped && Target is Panel targetPanel && ButtonIsPressed(e.GetCurrentPoint(targetPanel).Properties, PanMouseButton) && targetPanel.Children.Count > 0)
        {
            Capture(e.Pointer);
            _capturedPointer = e.Pointer;
            _previousPositionArgs = e;
        }
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (Target is not Panel targetPanel || e.Pointer != _capturedPointer)
        {
            return;
        }

        foreach (Control control in targetPanel.Children)
        {
            control.RenderTransform ??= new MatrixTransform(Matrix.Identity);
            Vector controlDelta = e.GetPosition(control) - _previousPositionArgs!.GetPosition(control);
            control.RenderTransform = new MatrixTransform(new Matrix(1.0, 0.0, 0.0, 1.0, controlDelta.X, controlDelta.Y) * control.RenderTransform.Value);
            control.InvalidateVisual();
        }

        targetPanel.InvalidateVisual();
        _previousPositionArgs = e;
    }

    protected override void PointerCaptureLost(IPointer pointer)
    {
        _capturedPointer = null;
    }

    protected override void PointerReleased(PointerReleasedEventArgs e)
    {
        _capturedPointer = null;
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
