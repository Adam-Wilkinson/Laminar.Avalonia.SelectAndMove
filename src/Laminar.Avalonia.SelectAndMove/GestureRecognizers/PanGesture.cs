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

    private PointerEventArgs? _previousPositionArgs;
    private IPointer? _capturedPointer;

    public MouseButton PanMouseButton
    {
        get => GetValue(PanMouseButtonProperty);
        set => SetValue(PanMouseButtonProperty, value);
    }
    
    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        if (e.Pointer is not Pointer { IsGestureRecognitionSkipped: false } 
            || !ButtonIsPressed(e.GetCurrentPoint(null).Properties, PanMouseButton)) 
            return;
        
        Capture(e.Pointer);
        _capturedPointer = e.Pointer;
        _previousPositionArgs = e;
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (Target is not ItemsControl targetItemsControl)
            throw new InvalidOperationException("This gesture is only valid on an ItemsControl");

        if (e.Pointer != _capturedPointer || targetItemsControl.ItemsPanelRoot is not { } panel) return;
        
        panel.RenderTransform ??= new MatrixTransform(Matrix.Identity);
        Vector changeInTransform = e.GetPosition(panel) - _previousPositionArgs!.GetPosition(panel);
        panel.RenderTransform = new MatrixTransform(new Matrix(1, 0, 0, 1, changeInTransform.X, changeInTransform.Y) * panel.RenderTransform.Value);
        panel.InvalidateVisual();
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
