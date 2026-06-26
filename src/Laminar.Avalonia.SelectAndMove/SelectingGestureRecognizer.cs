using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Laminar.Avalonia.SelectAndMove;

public abstract class SelectingGestureRecognizer : GestureRecognizer
{
    private const double MinimumSquaredMoveDistance = 40;

    public static readonly StyledProperty<ITemplate<Control>?> CursorDecorationTemplateProperty = AvaloniaProperty.Register<SelectingGestureRecognizer, ITemplate<Control>?>(nameof(CursorDecorationTemplate));
    
    public static readonly StyledProperty<MouseButton> TriggerSelectionMouseButtonProperty = AvaloniaProperty.Register<SelectingGestureRecognizer, MouseButton>(nameof(TriggerSelectionMouseButton));

    public static readonly AttachedProperty<Shape?> IntersectionShapeProperty = AvaloniaProperty.RegisterAttached<BoxSelectGesture, StyledElement, Shape?>("IntersectionShape");
    public static Shape? GetIntersectionShape(StyledElement visual) => visual.GetValue(IntersectionShapeProperty);
    public static void SetIntersectionShape(StyledElement visual, Shape? value) => visual.SetValue(IntersectionShapeProperty, value);
    
    private PointerEventArgs? _originalClick;
    private Control? _cursorDecoration;
    private IPointer? _capturedPointer;
    private SelectAndMove? _host;
    private bool _beginGestureRequested;
    private bool _hoverStarted;

    public MouseButton TriggerSelectionMouseButton
    {
        get => GetValue(TriggerSelectionMouseButtonProperty);
        set => SetValue(TriggerSelectionMouseButtonProperty, value);
    }

    public ITemplate<Control>? CursorDecorationTemplate
    {
        get => GetValue(CursorDecorationTemplateProperty);
        set => SetValue(CursorDecorationTemplateProperty, value);
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
        OnBeginGesture(e);
    }
    
    public bool PointerPressShouldTriggerGesture(PointerPressedEventArgs e)
    {
        if (_beginGestureRequested)
        {
            return true;
        }
        
        return e is { Pointer: Pointer { IsGestureRecognitionSkipped: false, Type: PointerType.Mouse }, Handled: false }
               && e.Properties.PointerUpdateKind.GetMouseButton() == TriggerSelectionMouseButton;
    }

    public event EventHandler? OnGestureFinished;
    
    protected virtual void OnHoverStart(PointerEventArgs e)
    {
        if (CursorDecorationTemplate is not null)
        {
            _cursorDecoration = CursorDecorationTemplate.Build();
            DrawingCanvas?.Children.Add(_cursorDecoration);
        }
    }
    
    protected virtual void OnHoverMove(PointerEventArgs e)
    {
        if (_cursorDecoration is not null)
        {
            var pos = e.GetPosition(DrawingCanvas);
            Canvas.SetLeft(_cursorDecoration, pos.X - _cursorDecoration.Bounds.Width);
            Canvas.SetTop(_cursorDecoration, pos.Y + _cursorDecoration.Bounds.Height);
        }
    }
    
    protected virtual void OnBeginGesture(PointerEventArgs e)
    {
    }
    
    protected abstract Geometry? CreateUpdatedSelectionGeometry(PointerEventArgs mostRecentArgs);

    protected virtual void Cleanup()
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
            if (!_hoverStarted)
            {
                OnHoverStart(e);
                _hoverStarted = true;
            }
            
            OnHoverMove(e);
        }
        
        if (_originalClick is null || _host is null)
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
            _host.SelectionModel.DeselectAll();
        }
        
        foreach (var element in _host.ItemsPanelRoot?.Children
                     .OfType<SelectAndMoveItem>()
                     .Where(el => SelectionIntersectionCheck(selectionGeometry, el)) ?? [])
        {
            element.IsSelected = true;
        }
    }

    protected override void PointerCaptureLost(IPointer pointer)
    {
        EndGesture();
    }

    protected override void PointerReleased(PointerReleasedEventArgs e)
    {
        if (_hoverStarted)
        {
            e.Handled = true;
        }
        
        EndGesture();
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        _host = (Target as Visual).FindAncestorOfType<SelectAndMove>();
    }

    private void EndGesture()
    {
        _capturedPointer = null;
        _originalClick = null;
        _beginGestureRequested = false;
        _hoverStarted = false;
        if (_cursorDecoration is not null)
        {
            DrawingCanvas?.Children.Remove(_cursorDecoration);
            _cursorDecoration = null;
        }
        Cleanup();
        OnGestureFinished?.Invoke(this, EventArgs.Empty);
    }
    
    private bool SelectionIntersectionCheck(Geometry selectionGeometry, SelectAndMoveItem item)
    {
        if (Target is not Visual visualTarget || !item.IsSelectable)
        {
            return false;
        }

        if (!AutoDeselectDuringGesture && item.IsSelected)
        {
            return true;
        }
        
        selectionGeometry.Transform = new MatrixTransform(visualTarget.TransformToVisual(item)!.Value);
        var controlGeometry = GetIntersectionGeometry(item);
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
            _host?.SelectionModel.DeselectAll();
        }
    }
}