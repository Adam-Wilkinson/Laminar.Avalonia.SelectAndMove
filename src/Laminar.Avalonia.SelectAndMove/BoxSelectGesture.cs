using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove;

public class BoxSelectGesture : SelectingGestureRecognizer
{
    public static readonly AttachedProperty<Rectangle> SelectionBoxProperty = AvaloniaProperty.RegisterAttached<BoxSelectGesture, StyledElement, Rectangle>("SelectionBox", new Rectangle { Stroke = Brushes.Red, StrokeThickness = 2 });
    public static Rectangle GetSelectionBox(StyledElement visual) => visual.GetValue(SelectionBoxProperty);
    public static void SetSelectionBox(StyledElement visual, Rectangle value) => visual.SetValue(SelectionBoxProperty, value);
    
    private readonly Canvas _drawingCanvas = new()
    {
        IsHitTestVisible = false
    };
    
    private bool _selectionBoxAdded;
    private PointerEventArgs? _originalClick;

    static BoxSelectGesture()
    {
        SelectionBoxProperty.Changed.AddClassHandler<BoxSelectGesture>((bsg, args) => bsg.SelectionBoxChanged(args));
    }

    public BoxSelectGesture()
    {
        _drawingCanvas.Children.Add(GetSelectionBox(this));
    }

    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        base.PointerPressed(e);
        Selection.SetIsSelectable(GetSelectionBox(this), false);
    }

    protected override Geometry? CreateUpdatedSelectionGeometry(PointerEventArgs mostRecentArgs)
    {
        if (Target is not Visual visualTarget) return null;
        
        if (_originalClick is null)
        {
            _originalClick = mostRecentArgs;
            return null;
        }
        
        if (!_selectionBoxAdded && OverlayLayer.GetOverlayLayer(visualTarget) is { } overlayLayer)
        {
            overlayLayer.Children.Add(_drawingCanvas);
            _selectionBoxAdded = true;
            _drawingCanvas.Clip = new RectangleGeometry(
                visualTarget.TransformToVisual(_drawingCanvas) is { } transform ? 
                    visualTarget.Bounds.TransformToAABB(transform) : visualTarget.Bounds);
        }
        
        var selectionBox = GetSelectionBox(this);
        Rect drawnRect = new Rect(_originalClick.GetPosition(_drawingCanvas), mostRecentArgs.GetPosition(_drawingCanvas)).Normalize();
        Canvas.SetLeft(selectionBox, drawnRect.Left);
        Canvas.SetTop(selectionBox, drawnRect.Top);
        selectionBox.Width = drawnRect.Width;
        selectionBox.Height = drawnRect.Height;

        return new RectangleGeometry(new Rect(_originalClick.GetPosition(visualTarget),
            mostRecentArgs.GetPosition(visualTarget)).Normalize());
    }

    protected override void Reset()
    {
        if (Target is not Visual visualTarget) return;
        OverlayLayer.GetOverlayLayer(visualTarget)?.Children.Remove(_drawingCanvas);
        _selectionBoxAdded = false;
        _originalClick = null;
    }

    private void SelectionBoxChanged(AvaloniaPropertyChangedEventArgs args)
    {
        var (oldValue, newValue) = args.GetOldAndNewValue<Rectangle>();
        _drawingCanvas.Children.Remove(oldValue);
        _drawingCanvas.Children.Add(newValue);
    }
}
