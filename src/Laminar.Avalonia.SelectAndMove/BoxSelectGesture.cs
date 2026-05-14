using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove;

public class BoxSelectGesture : GestureRecognizer
{
    public static readonly AttachedProperty<Rectangle> SelectionBoxProperty = AvaloniaProperty.RegisterAttached<BoxSelectGesture, Visual, Rectangle>(nameof(SelectionBox), new Rectangle { Stroke = Brushes.Red, StrokeThickness = 2 });
    public static Rectangle GetSelectionBox(Visual visual) => visual.GetValue(SelectionBoxProperty);
    public static void SetSelectionBox(Visual visual, Rectangle value) => visual.SetValue(SelectionBoxProperty, value);
    
    public static readonly AttachedProperty<MouseButton> BoxSelectMouseButtonProperty = AvaloniaProperty.RegisterAttached<BoxSelectGesture, Visual, MouseButton>(nameof(BoxSelectMouseButton), MouseButton.Left);
    public static MouseButton GetBoxSelectMouseButton(Visual visual) => visual.GetValue(BoxSelectMouseButtonProperty);
    public static void SetBoxSelectMouseButton(Visual visual, MouseButton value) => visual.SetValue(BoxSelectMouseButtonProperty, value);

    public static readonly AttachedProperty<Shape?> IntersectionShapeProperty = AvaloniaProperty.RegisterAttached<BoxSelectGesture, Visual, Shape?>("IntersectionShape");
    public static Shape? GetIntersectionShape(Visual visual) => visual.GetValue(IntersectionShapeProperty);
    public static void SetIntersectionShape(Visual visual, Shape? value) => visual.SetValue(IntersectionShapeProperty, value);
    
    private readonly Canvas _drawingCanvas = new()
    {
        IsHitTestVisible = false
    };
    
    private bool _selectionBoxAdded;
    private PointerPressedEventArgs? _originalClick;
    private IPointer? _capturedPointer;

    static BoxSelectGesture()
    {
        SelectionBoxProperty.Changed.AddClassHandler<BoxSelectGesture>((bsg, args) => bsg.SelectionBoxChanged(args));
    }

    public BoxSelectGesture()
    {
        _drawingCanvas.Children.Add(SelectionBox);
    }

    public KeyModifiers SelectManyKeyModifiers
    {
        get => GetValue(SelectGesture.SelectManyKeyModifiersProperty);
        set => SetValue(SelectGesture.SelectManyKeyModifiersProperty, value);
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
        if (e.Pointer is not Pointer pointer 
            || e.Handled
            || pointer.IsGestureRecognitionSkipped 
            || !ButtonIsPressed(e.Properties, BoxSelectMouseButton))
        {
            return;
        }

        Capture(e.Pointer);
        _capturedPointer = e.Pointer;
        
        Selection.SetIsSelectable(SelectionBox, false);
        _originalClick = e;
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (Target is not InputElement target || e.Pointer != _capturedPointer || _originalClick is null)
        {
            return;
        }

        if (!_selectionBoxAdded && OverlayLayer.GetOverlayLayer(target) is { } overlayLayer)
        {
            overlayLayer.Children.Add(_drawingCanvas);
            _selectionBoxAdded = true;
            _drawingCanvas.Clip = new RectangleGeometry(
                target.TransformToVisual(_drawingCanvas) is { } transform ? 
                target.Bounds.TransformToAABB(transform) : target.Bounds);
        }
        
        Rect drawnRect = new Rect(_originalClick.GetPosition(_drawingCanvas), e.GetPosition(_drawingCanvas))
            .Normalize();
        Canvas.SetLeft(SelectionBox, drawnRect.Left);
        Canvas.SetTop(SelectionBox, drawnRect.Top);
        SelectionBox.Width = drawnRect.Width;
        SelectionBox.Height = drawnRect.Height;

        if (drawnRect.Size.Height == 0 || drawnRect.Size.Width == 0) return;
        
        if (e.KeyModifiers != SelectManyKeyModifiers)
        {
            Selection.ClearSiblingSelection(target);
        }

        Rect intersectRect =
            new Rect(_originalClick.GetPosition(target), e.GetPosition(target)).Normalize(); 
        
        foreach (var element in Selection.GetSiblings(target)?
                     .Where(el => Selection.GetIsSelectable(el) && BoxIntersects(intersectRect, el)) ?? [])
        {
            Selection.SetIsSelected(element, true);
        }
    }

    protected override void PointerReleased(PointerReleasedEventArgs e)
    {
        EndGesture();
    }

    protected override void PointerCaptureLost(IPointer pointer)
    {
        EndGesture();
    }

    private void EndGesture()
    {
        if (Target is not Visual visualTarget) return;
        OverlayLayer.GetOverlayLayer(visualTarget)?.Children.Remove(_drawingCanvas);
        _capturedPointer = null;
        _originalClick = null;
        _selectionBoxAdded = false;
    }

    private bool BoxIntersects(Rect selectionRect, InputElement control)
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

        if (FindIntersectionShape(control) is { DefiningGeometry: { } geometry })
        {
            Geometry intersection = Geometry.Combine(geometry, new RectangleGeometry(rectInLocal), GeometryCombineMode.Intersect);
            return intersection.Bounds.Width > 0 || intersection.Bounds.Height > 0;
        }

        return new Rect(control.Bounds.Size).Intersects(rectInLocal);
    }

    private Shape? FindIntersectionShape(InputElement? element)
    {
        while (element is not null)
        {
            if (GetIntersectionShape(element) is { } userDefinedIntersection)
            {
                return userDefinedIntersection;
            }

            if (element is Shape shapeElement)
            {
                return shapeElement;
            }

            element = element switch
            {
                ContentPresenter contentPresenter => contentPresenter.Child,
                ContentControl contentControl => contentControl.Content as InputElement,
                Decorator { IsHitTestVisible: false } decorator => decorator.Child,
                _ => null,
            };
        }
        return null;
    }

    private void SelectionBoxChanged(AvaloniaPropertyChangedEventArgs args)
    {
        var (oldValue, newValue) = args.GetOldAndNewValue<Rectangle>();
        _drawingCanvas.Children.Remove(oldValue);
        _drawingCanvas.Children.Add(newValue);
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
