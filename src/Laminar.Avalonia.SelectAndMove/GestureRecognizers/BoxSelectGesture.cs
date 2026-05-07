using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public class BoxSelectGesture : GestureRecognizer
{
    public static readonly StyledProperty<KeyModifiers> SelectManyKeyModifiersProperty =
        SelectGesture.SelectManyKeyModifiersProperty.AddOwner<SelectGesture>();

    public static readonly StyledProperty<Rectangle> SelectionBoxProperty =
        AvaloniaProperty.Register<BoxSelectGesture, Rectangle>(nameof(SelectionBox), new Rectangle { Stroke = Brushes.Red, StrokeThickness = 2 });

    public static readonly StyledProperty<MouseButton> BoxSelectMouseButtonProperty =
        AvaloniaProperty.Register<BoxSelectGesture, MouseButton>(nameof(BoxSelectMouseButton), MouseButton.Left);

    private readonly Func<Panel?> _workingPanelFinder;
    private readonly Canvas _drawingCanvas = new();
    
    private bool _selectionBoxAdded;
    private Point? _startingPoint;
    private PointerPressedEventArgs? _originalClick;
    private IPointer? _capturedPointer;
    private Panel? _workingPanel;

    static BoxSelectGesture()
    {
        SelectionBoxProperty.Changed.AddClassHandler<BoxSelectGesture>((bsg, args) => bsg.SelectionBoxChanged(args));
    }

    public BoxSelectGesture(Panel workingPanel) : this(() => workingPanel)
    {
    }

    public BoxSelectGesture(ItemsControl itemsControl) : this(() => itemsControl.ItemsPanelRoot)
    {
    }

    public BoxSelectGesture(Func<Panel?> workingPanelFinder)
    {
        _workingPanelFinder = workingPanelFinder;
        _drawingCanvas.Children.Add(SelectionBox);
    }

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
        if (e.Pointer is not Pointer pointer || Target is not InputElement target || pointer.IsGestureRecognitionSkipped || !ButtonIsPressed(e.GetCurrentPoint(null).Properties, BoxSelectMouseButton))
        {
            return;
        }

        Capture(e.Pointer);
        _capturedPointer = e.Pointer;

        Selection.SetIsSelectable(SelectionBox, false);
        _workingPanel = _workingPanelFinder();
        _originalClick = e;
        
        _startingPoint = e.GetPosition(target);
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (Target is not InputElement target || e.Pointer != _capturedPointer || _originalClick is null || _workingPanel is null)
        {
            return;
        }

        if (!_selectionBoxAdded && OverlayLayer.GetOverlayLayer(target) is { } overlayLayer)
        {
            overlayLayer.Children.Add(_drawingCanvas);
            _selectionBoxAdded = true;       
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
            Selection.ClearSiblings(target);
        }

        Rect intersectRect =
            new Rect(_originalClick.GetPosition(_workingPanel), e.GetPosition(_workingPanel)).Normalize(); 
        
        foreach (Control control in _workingPanel.Children
                     .Where(control => Selection.GetIsSelectable(control) && BoxIntersectsControl(intersectRect, control)))
        {
            Selection.SetIsSelected(control, true);
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
        if (Target is not Visual visualTarget) return;
        OverlayLayer.GetOverlayLayer(visualTarget)?.Children.Remove(_drawingCanvas);
        _capturedPointer = null;
        _startingPoint = null;
        _selectionBoxAdded = false;
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

        if (control is Shape { DefiningGeometry: { } shapeGeometry })
        {
            Geometry intersection = Geometry.Combine(shapeGeometry, new RectangleGeometry(rectInLocal), GeometryCombineMode.Intersect);
            return intersection.Bounds.Width > 0 || intersection.Bounds.Height > 0;
        }

        return new Rect(control.Bounds.Size).Intersects(rectInLocal);
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
