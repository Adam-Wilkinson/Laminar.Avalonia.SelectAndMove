using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public class MoveSelectionGesture : GestureRecognizerBase
{
    public static readonly StyledProperty<Rect> SnapGridProperty = 
        AvaloniaProperty.RegisterAttached<MoveSelectionGesture, Rect>(nameof(SnapGrid), typeof(MoveSelectionGesture), new Rect(0, 0, 50, 50));

    public static readonly StyledProperty<SnapMode> SnapModeProperty = 
        AvaloniaProperty.RegisterAttached<MoveSelectionGesture, SnapMode>(nameof(SnapMode), typeof(MoveSelectionGesture), SnapMode.None);

    private readonly List<(Control control, Point originalTopLeft)> _movingControls = new();

    Point _originalClickPoint = new(0, 0);
    Rect _originalBoundsOfSelection;

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

    protected override void TrackedPointerMoved(PointerEventArgs e)
    {
        Point delta = e.GetPosition(Target) - _originalClickPoint;

        foreach ((Control control, Point originalControlTopLeft) in _movingControls)
        {
            Vector controlScale = (new Point(1, 1) * control.RenderTransform!.Value.Invert()) - (new Point(0, 0) * control.RenderTransform!.Value.Invert());
            Point localMouseDelta = new(delta.X * controlScale.X, delta.Y * controlScale.Y);
            if (SnapMode == SnapMode.None)
            {
                Canvas.SetLeft(control, (originalControlTopLeft + localMouseDelta).X);
                Canvas.SetTop(control, (originalControlTopLeft + localMouseDelta).Y);
                continue;
            }

            Point offsetFromSnapAnchor = originalControlTopLeft - GetSnapPoint(_originalBoundsOfSelection, SnapMode);
            Point snapAnchor = GetSnapPoint(_originalBoundsOfSelection, SnapMode) + localMouseDelta;
            Point newPositionOfAnchor = Snap(snapAnchor, SnapGrid);
            // For some reason Avalonia rounds at some point during Canvas.SetTop and Canvas.SetLeft, this stops controls from 'wiggling'
            newPositionOfAnchor = new(Math.Round(newPositionOfAnchor.X), Math.Round(newPositionOfAnchor.Y));

            Point newControlLocation = newPositionOfAnchor + offsetFromSnapAnchor;

            Canvas.SetTop(control, newControlLocation.Y);
            Canvas.SetLeft(control, newControlLocation.X);
        }
    }

    public override void PointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Target).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (Target is not IPanel targetPanel)
        {
            return;
        }

        // Bubble up from the clicked interactive object until we find a selected, movable control
        IInteractive? currentItem = e.Source;
        while (currentItem is not null && !(currentItem is IControl currentControl && SelectAndMove.GetIsSelected(currentControl) && SelectAndMove.GetIsMovable(currentControl)))
        {
            currentItem = currentItem.InteractiveParent;
        }

        if (currentItem is null)
        {
            return;
        }

        Track(e.Pointer);

        _movingControls.Clear();

        _originalBoundsOfSelection = Rect.Empty;
        foreach (IControl control in targetPanel.Children)
        {
            if (control is Control fullControl && SelectAndMove.GetIsSelected(control) && SelectAndMove.GetIsMovable(control))
            {
                control.RenderTransform ??= new MatrixTransform();

                Point controlTopLeftToParent = new(Canvas.GetLeft(fullControl), Canvas.GetTop(fullControl));
                _originalBoundsOfSelection = _originalBoundsOfSelection.Union(control.Bounds);
                _movingControls.Add((fullControl, controlTopLeftToParent));
            }
        }

        _originalClickPoint = e.GetPosition(Target);
    }

    public static Point Snap(Point point, Rect snapGrid)
    {
        return point
            .WithX(snapGrid.Width == 0.0 ? point.X : Math.Round((point.X - (snapGrid.X)) / snapGrid.Width) * snapGrid.Width + snapGrid.X)
            .WithY(snapGrid.Height == 0.0 ? point.Y : Math.Round((point.Y - (snapGrid.Y)) / snapGrid.Height) * snapGrid.Height + snapGrid.Y);
    }

    private static Point GetSnapPoint(Rect boundsRect, SnapMode snapMode) => (snapMode) switch
    {
        SnapMode.TopLeft => boundsRect.TopLeft,
        SnapMode.TopRight => boundsRect.TopRight,
        SnapMode.BottomLeft => boundsRect.BottomLeft,
        SnapMode.BottomRight => boundsRect.BottomRight,
        SnapMode.Center => boundsRect.Center,
        _ => throw new ArgumentException("SnapMode must not be None to find a snap point", nameof(snapMode)),
    };
}
