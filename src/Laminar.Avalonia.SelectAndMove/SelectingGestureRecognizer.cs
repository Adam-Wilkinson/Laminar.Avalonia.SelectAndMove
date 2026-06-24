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
    
    private PointerEventArgs? _originalClick;
    private IPointer? _capturedPointer;
    private bool _beginGestureRequested;

    public MouseButton TriggerSelectionMouseButton
    {
        get => GetValue(TriggerSelectionMouseButtonProperty);
        set => SetValue(TriggerSelectionMouseButtonProperty, value);
    }

    protected internal Canvas? DrawingCanvas
    {
        get => field ??= Target as Canvas;
        set;
    }

    protected bool AutoDeselectDuringGesture { get; set; } = true;

    public void BeginHover()
    {
        _beginGestureRequested = true;
    }

    public void BeginGesture(PointerEventArgs e)
    {
        _originalClick = e;
    }
    
    public bool PointerPressShouldTriggerGesture(PointerPressedEventArgs e)
    {
        if (_beginGestureRequested)
        {
            return true;
        }
        
        return e is { Pointer: Pointer { IsGestureRecognitionSkipped: false, Type: PointerType.Mouse }, Handled: false }
               && e.Properties.PointerUpdateKind.GetMouseButton() == GetTriggerSelectionMouseButton(this);
    }

    public event EventHandler? OnGestureFinished;

    
    protected abstract Geometry? CreateUpdatedSelectionGeometry(PointerEventArgs mostRecentArgs);

    protected abstract void Cleanup();

    protected virtual void OnHoverMove(PointerEventArgs e)
    {
    }

    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        // Right click = cancel gesture
        if (e.Properties.PointerUpdateKind.GetMouseButton() == MouseButton.Right)
        {
            Cleanup();
            OnGestureFinished?.Invoke(this, EventArgs.Empty);
            _beginGestureRequested = false;
        }
        
        if (PointerPressShouldTriggerGesture(e))
        { 
            BeginGesture(e);
        }
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (_beginGestureRequested)
        {
            OnHoverMove(e);
        }
        
        if (Target is not InputElement target || _originalClick is null)
        {
            return;
        }

        var dist = _originalClick.GetPosition(null) - e.GetPosition(null);
        if (dist.X * dist.X + dist.Y * dist.Y < MinimumSquaredMoveDistance)
        {
            return;
        }
        
        EnsureSelectionInitialized(e);
        
        if (CreateUpdatedSelectionGeometry(e) is not { } selectionGeometry)
        {
            return;
        }

        if (selectionGeometry.Bounds.Width == 0 || selectionGeometry.Bounds.Height == 0) return;
        
        if (AutoDeselectDuringGesture && e.KeyModifiers != SelectAndMove.GetSelectManyKeyModifiers(this))
        {
            Selection.ClearSiblingSelection((InputElement)Target!);
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
        _beginGestureRequested = false;
        Cleanup();
        OnGestureFinished?.Invoke(this, EventArgs.Empty);
    }
    
    private bool SelectionIntersectionCheck(Geometry selectionGeometry, InputElement control)
    {
        if (Target is not Visual visualTarget || !Selection.GetIsSelectable(control))
        {
            return false;
        }

        if (!AutoDeselectDuringGesture && Selection.GetIsSelected(control))
        {
            return true;
        }
        
        selectionGeometry.Transform = new MatrixTransform(visualTarget.TransformToVisual(control)!.Value);
        var controlGeometry = GetIntersectionGeometry(control);
        if (!selectionGeometry.Bounds.Intersects(controlGeometry.Bounds))
        {
            return false;
        }
        
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

    private void EnsureSelectionInitialized(PointerEventArgs e)
    {
        var pointer = e.Pointer;
        if (Equals(_capturedPointer, pointer)) return;

        _capturedPointer?.Capture(null);
        Capture(pointer);
        _capturedPointer = pointer;
        
        if (e.KeyModifiers != SelectAndMove.GetSelectManyKeyModifiers(this))
        {
            Selection.ClearSiblingSelection((InputElement)Target!);
        }
    }
}