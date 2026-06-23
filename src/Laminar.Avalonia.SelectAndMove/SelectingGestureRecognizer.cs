using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove;

public abstract class SelectingGestureRecognizer : GestureRecognizer
{
    private const double MinimumSquaredMoveDistance = 40;
    
    public static readonly AttachedProperty<MouseButton> TriggerSelectionMouseButtonProperty = AvaloniaProperty.RegisterAttached<BoxSelectGesture, StyledElement, MouseButton>("TriggerSelectionMouseButton", MouseButton.Left);
    public static MouseButton GetTriggerSelectionMouseButton(StyledElement visual) => visual.GetValue(TriggerSelectionMouseButtonProperty);
    public static void SetTriggerSelectionMouseButton(StyledElement visual, MouseButton value) => visual.SetValue(TriggerSelectionMouseButtonProperty, value);

    public static readonly AttachedProperty<Shape?> IntersectionShapeProperty = AvaloniaProperty.RegisterAttached<BoxSelectGesture, StyledElement, Shape?>("IntersectionShape");
    public static Shape? GetIntersectionShape(StyledElement visual) => visual.GetValue(IntersectionShapeProperty);
    public static void SetIntersectionShape(StyledElement visual, Shape? value) => visual.SetValue(IntersectionShapeProperty, value);
    
    private PointerPressedEventArgs? _originalClick;
    private IPointer? _capturedPointer;

    protected abstract Geometry? CreateUpdatedSelectionGeometry(PointerEventArgs mostRecentArgs);

    protected abstract void Reset();
    
    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        if (e.Pointer is not Pointer pointer 
            || e.Handled
            || pointer.IsGestureRecognitionSkipped 
            || pointer.Type != PointerType.Mouse
            || !ButtonIsPressed(e.Properties, GetTriggerSelectionMouseButton(this)))
        {
            return;
        }
        
        _originalClick = e;
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (Target is not InputElement target || _originalClick is null)
        {
            return;
        }

        var dist = _originalClick.GetPosition(null) - e.GetPosition(null);
        if (dist.X * dist.X + dist.Y * dist.Y < MinimumSquaredMoveDistance)
        {
            return;
        }
        
        if (_capturedPointer is null)
        {
            Capture(e.Pointer);
            _capturedPointer = e.Pointer;            
        }

        if (CreateUpdatedSelectionGeometry(e) is not { } selectionGeometry)
        {
            return;
        }

        if (selectionGeometry.Bounds.Width == 0 || selectionGeometry.Bounds.Height == 0) return;
        
        if (e.KeyModifiers != SelectAndMove.GetSelectManyKeyModifiers(this))
        {
            Selection.ClearSiblingSelection(target);
        }
        
        foreach (var element in Selection.GetSiblings(target)?
                     .Where(el => SelectionIntersectionCheck(selectionGeometry, el)) ?? [])
        {
            Selection.SetIsSelected(element, true);
        }
    }

    protected override void PointerCaptureLost(IPointer pointer)
    {
        EndGesture();
    }

    protected override void PointerReleased(PointerReleasedEventArgs e)
    {
        EndGesture();
    }

    private void EndGesture()
    {
        _capturedPointer = null;
        _originalClick = null;
        Reset();
    }
    
    private bool SelectionIntersectionCheck(Geometry selectionGeometry, InputElement control)
    {
        if (Target is not Visual visualTarget || !Selection.GetIsSelectable(control))
        {
            return false;
        }

        selectionGeometry.Transform = new MatrixTransform(visualTarget.TransformToVisual(control)!.Value);
        var controlGeometry = GetIntersectionGeometry(control);
        Geometry intersection = new CombinedGeometry(GeometryCombineMode.Intersect, selectionGeometry, controlGeometry);
        return intersection.Bounds.Width > 0 || intersection.Bounds.Height > 0;
    }

    private Geometry GetIntersectionGeometry(InputElement control)
    {
        if (control is ICustomHitResolver customHitResolver)
        {
            return customHitResolver.GetCustomSelectionGeometry();
        }

        if (FindIntersectionShape(control) is { DefiningGeometry: { } geometry })
        {
            return geometry;
        }

        return new RectangleGeometry(new Rect(control.Bounds.Size));
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
                ContentControl contentControl => contentControl.Presenter,
                Decorator { IsHitTestVisible: false } decorator => decorator.Child,
                _ => null,
            };
        }
        return null;
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