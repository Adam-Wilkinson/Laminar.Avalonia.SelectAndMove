using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Media;
using Avalonia.Platform;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public class BoxSelectGesture : GestureRecognizer
{
    public static readonly StyledProperty<KeyModifiers> SelectManyKeyModifiersProperty =
        SelectGesture.SelectManyKeyModifiersProperty.AddOwner<SelectGesture>();

    public static readonly StyledProperty<Rectangle> SelectionBoxProperty =
        AvaloniaProperty.Register<BoxSelectGesture, Rectangle>(nameof(SelectionBox), new Rectangle { Stroke = Brushes.Red, StrokeThickness = 2 });

    public static readonly StyledProperty<MouseButton> BoxSelectMouseButtonProperty =
        AvaloniaProperty.Register<BoxSelectGesture, MouseButton>(nameof(BoxSelectMouseButton), MouseButton.Left);

    bool _selectionBoxAdded = false;
    Point _startingPoint = new();
    IPointer? _capturedPointer = null;

    public KeyModifiers SelectManyKeyModifiers
    {
        get => GetValue(SelectManyKeyModifiersProperty);
        set => SetValue(SelectManyKeyModifiersProperty, value);
    }

    public Rectangle SelectionBox
    {
        get => GetValue(SelectionBoxProperty);
        set => SetValue(SelectionBoxProperty, value);
    }

    public MouseButton BoxSelectMouseButton
    {
        get => GetValue(BoxSelectMouseButtonProperty);
        set => SetValue(BoxSelectMouseButtonProperty, value);
    }

    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        if (e.Pointer is not Pointer pointer || pointer.IsGestureRecognitionSkipped || Target is not Canvas targetCanvas || !ButtonIsPressed(e.GetCurrentPoint(targetCanvas).Properties, BoxSelectMouseButton))
        {
            return;
        }

        Capture(e.Pointer);
        _capturedPointer = e.Pointer;

        SelectAndMove.SetIsSelectable(SelectionBox, false);
        _startingPoint = e.GetPosition(targetCanvas);
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (Target is not Canvas targetCanvas || e.Pointer != _capturedPointer)
        {
            return;
        }

        if (!_selectionBoxAdded)
        {
            targetCanvas.Children.Add(SelectionBox);
            _selectionBoxAdded = true;
        }

        Rect selectionRect = new Rect(_startingPoint, e.GetPosition(targetCanvas)).Normalize();

        Canvas.SetLeft(SelectionBox, selectionRect.Left);
        Canvas.SetTop(SelectionBox, selectionRect.Top);
        SelectionBox.Width = selectionRect.Width;
        SelectionBox.Height = selectionRect.Height;

        foreach (Control control in targetCanvas.Children)
        {
            if (e.KeyModifiers != SelectManyKeyModifiers)
            {
                SelectAndMove.SetIsSelected(control, false);
            }

            if (SelectAndMove.GetIsSelectable(control) && BoxIntersectsControl(selectionRect, control))
            {
                SelectAndMove.SetIsSelected(control, true);
            }
        }
    }

    protected override void PointerReleased(PointerReleasedEventArgs e)
    {
        _capturedPointer = null;
    }

    protected override void PointerCaptureLost(IPointer pointer)
    {
        EndGesture();
    }

    private void EndGesture()
    {
        if (Target is Panel targetPanel)
        {
            targetPanel.Children.Remove(SelectionBox);
            _capturedPointer = null;
            _selectionBoxAdded = false;
        }
    }

    private bool BoxIntersectsControl(Rect selectionRect, Control control)
    {
        if (Target is not Visual visualTarget)
        {
            return false;
        }

        Rect rectInLocal = selectionRect.TransformToAABB(visualTarget.TransformToVisual(control)!.Value);

        if (control is ICustomHitResolver customHitResolver)
        {
            return customHitResolver.IntersectsWithRectangle(rectInLocal);
        }

        if (control is Shape shape && shape.DefiningGeometry is Geometry shapeGeometry)
        {
            Geometry intersection = Geometry.Combine(shapeGeometry, new RectangleGeometry(rectInLocal), GeometryCombineMode.Intersect);
            return intersection.Bounds.Width > 0 || intersection.Bounds.Height > 0;
        }

        return new Rect(control.Bounds.Size).Intersects(rectInLocal);
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
}
