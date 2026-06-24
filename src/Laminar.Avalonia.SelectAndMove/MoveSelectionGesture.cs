using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Laminar.Avalonia.SelectAndMove;

public class MoveSelectionGesture : GestureRecognizer
{
    private const double MinimumMoveDistance = 30;
    
    public static readonly StyledProperty<Rect> SnapGridProperty = AvaloniaProperty.RegisterAttached<MoveSelectionGesture, Rect>(nameof(SnapGrid), typeof(MoveSelectionGesture), new Rect(0, 0, 50, 50));

    public static readonly AttachedProperty<SnapMode> SnapModeProperty = AvaloniaProperty.RegisterAttached<MoveSelectionGesture, StyledElement, SnapMode>(nameof(SnapMode));
    public static SnapMode GetSnapMode(StyledElement element) => element.GetValue(SnapModeProperty);
    public static void SetSnapMode(StyledElement element, SnapMode value) => element.SetValue(SnapModeProperty, value);
    
    public static readonly AttachedProperty<bool> IsMovableProperty = AvaloniaProperty.RegisterAttached<SelectAndMove, AvaloniaObject, bool>("IsMovable", true);
    public static bool GetIsMovable(AvaloniaObject element) => element.GetValue(IsMovableProperty);
    public static void SetIsMovable(AvaloniaObject element, bool value) => element.SetValue(IsMovableProperty, value);
    
    private readonly List<(SelectAndMoveItem item, Point originalTopLeft)> _moving = [];

    private Point _originalClickPoint = new(0, 0);
    private IPointer? _clickedPointer;
    private Point? _snapPoint;

    public Rect SnapGrid
    {
        get => GetValue(SnapGridProperty);
        set => SetValue(SnapGridProperty, value);
    }

    public SnapMode SnapMode
    {
        get => GetValue(SnapModeProperty);
        set => SetValue(SnapModeProperty, value);
    }

    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        if (e.Pointer is not Pointer pointer
            || e.Handled
            || pointer.IsGestureRecognitionSkipped 
            || Target is not InputElement target 
            || !e.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Bubble up from the clicked object until we find a selected, movable control
        if (e.Source is not Visual interactiveSource)
        {
            return;
        }

        var samItem = interactiveSource.FindAncestorOfType<SelectAndMoveItem>();
        if (samItem is null || !GetIsMovable(samItem) || !Selection.GetIsSelectable(samItem))
        {
            return;
        }

        _clickedPointer = e.Pointer;
        _moving.Clear();

        var originalBoundsOfSelection = new Rect(0, 0, 0, 0);
        foreach (var sibling in Selection.GetSelectedSiblings(target)?.Where(GetIsMovable).OfType<SelectAndMoveItem>() ?? [])
        {
            sibling.RenderTransform ??= new MatrixTransform();
            
            Point controlTopLeftToParent = new(Canvas.GetLeft(sibling), Canvas.GetTop(sibling));
            if (double.IsNaN(controlTopLeftToParent.X))
            {
                controlTopLeftToParent = controlTopLeftToParent.WithX(0);
            }

            if (double.IsNaN(controlTopLeftToParent.Y))
            {
                controlTopLeftToParent = controlTopLeftToParent.WithY(0);
            }
            
            originalBoundsOfSelection = originalBoundsOfSelection.Union(sibling.Bounds);
            _moving.Add((sibling, controlTopLeftToParent));
            ZIndexLayerManger.BringToFront(sibling);
        }

        _snapPoint = GetSnapPoint(originalBoundsOfSelection, SnapMode);
        _originalClickPoint = e.GetPosition(target);
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (Target is not Visual visualTarget || _clickedPointer != e.Pointer)
        {
            return;
        }

        Point targetDelta = e.GetPosition(visualTarget) - _originalClickPoint;

        if (targetDelta.X * targetDelta.X + targetDelta.Y * targetDelta.Y < MinimumMoveDistance)
        {
            return;
        }
        
        Capture(e.Pointer);

        foreach ((SelectAndMoveItem item, Point originalControlTopLeft) in _moving)
        {
            Point localMouseDelta = targetDelta;
            if (item.TransformToVisual(visualTarget) is { } transform)
            { 
                Vector controlScale = new Point(1, 1) * transform.Invert() - new Point(0, 0) * transform.Invert();
                localMouseDelta = new(targetDelta.X * controlScale.X, targetDelta.Y * controlScale.Y);
            }

            item.SetIsDragging(true);
            
            if (_snapPoint is not { } snapPoint)
            {
                Canvas.SetLeft(item, (originalControlTopLeft + localMouseDelta).X);
                Canvas.SetTop(item, (originalControlTopLeft + localMouseDelta).Y);
                continue;
            }

            Point offsetFromSnapAnchor = originalControlTopLeft - snapPoint;
            Point snapAnchor = snapPoint + localMouseDelta;
            Point newPositionOfAnchor = Snap(snapAnchor, SnapGrid);
            // For some reason Avalonia rounds at some point during Canvas.SetTop and Canvas.SetLeft, this stops controls from 'wiggling'
            newPositionOfAnchor = new(Math.Round(newPositionOfAnchor.X), Math.Round(newPositionOfAnchor.Y));

            Point newControlLocation = newPositionOfAnchor + offsetFromSnapAnchor;

            Canvas.SetTop(item, newControlLocation.Y);
            Canvas.SetLeft(item, newControlLocation.X);
        }
    }

    public static Point Snap(Point point, Rect snapGrid)
    {
        return point
            .WithX(snapGrid.Width == 0.0 ? point.X : Math.Round((point.X - (snapGrid.X)) / snapGrid.Width) * snapGrid.Width + snapGrid.X)
            .WithY(snapGrid.Height == 0.0 ? point.Y : Math.Round((point.Y - (snapGrid.Y)) / snapGrid.Height) * snapGrid.Height + snapGrid.Y);
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
        _clickedPointer?.Capture(null);
        _clickedPointer = null;
        foreach (var (control, _) in _moving)
        {
            if (control is SelectAndMoveItem item)
            {
                item.SetIsDragging(false);
            }
        }
    }

    private static Point? GetSnapPoint(Rect boundsRect, SnapMode snapMode) => snapMode switch
    {
        SnapMode.TopLeft => boundsRect.TopLeft,
        SnapMode.TopRight => boundsRect.TopRight,
        SnapMode.BottomLeft => boundsRect.BottomLeft,
        SnapMode.BottomRight => boundsRect.BottomRight,
        SnapMode.Center => boundsRect.Center,
        _ => null,
    };
}
