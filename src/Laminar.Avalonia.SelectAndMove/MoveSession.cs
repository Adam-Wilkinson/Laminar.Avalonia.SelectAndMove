using System.Collections;
using Avalonia;

namespace Laminar.Avalonia.SelectAndMove;

public sealed class MoveSession : IDisposable
{
    private readonly SelectAndMove _parent;
    private readonly List<(SelectAndMoveItem item, Point originalTopLeft)> _moving = [];
    private readonly IList _movingItems;
    private readonly Point? _snapPoint;
    private readonly Rect _snapGrid;
    private readonly double _minimumDistance;
    
    private bool _isDisposed;
    private bool _hasStarted;

    /// <summary>
    /// Internal use only; use <see cref="SelectAndMove.GetMoveSession"/>.
    /// </summary>
    internal MoveSession(SelectAndMove parent, SnapMode snapMode, Rect snapGrid, double minimumDistance = -1)
    {
        _parent = parent;
        _snapGrid = snapGrid;
        _minimumDistance = minimumDistance;
        
        var originalBoundsOfSelection = new Rect(0, 0, 0, 0);
        foreach (var item in parent.ItemsPanelRoot?.Children
                     .OfType<SelectAndMoveItem>()
                     .Where(x => x is { IsMovable: true, IsSelected: true }) ?? [])
        {
            Point itemTopLeft = new(item.Left, item.Top);
            if (double.IsNaN(itemTopLeft.X))
            {
                itemTopLeft = itemTopLeft.WithX(0);
            }

            if (double.IsNaN(itemTopLeft.Y))
            {
                itemTopLeft = itemTopLeft.WithY(0);
            }
            
            originalBoundsOfSelection = originalBoundsOfSelection.Union(item.Bounds);
            _moving.Add((item, itemTopLeft));
        }

        _snapPoint = GetSnapPoint(originalBoundsOfSelection, snapMode);
        _movingItems = _moving.Select(moving => parent.ItemFromContainer(moving.item)).ToArray();
        
        StartIfDistanceSufficient();
    }

    public Vector OverallMoveDistance
    {
        get;
        set
        {
            if (EqualityComparer<Vector>.Default.Equals(field, value)) return;
            var increment = value - field;
            field = value;
            if (_isDisposed || (!_hasStarted && !StartIfDistanceSufficient())) return;
        
            foreach (var (item, originalControlTopLeft) in _moving)
            {
                if (_snapPoint is not { } snapPoint)
                {
                    item.Left = originalControlTopLeft.X + OverallMoveDistance.X;
                    item.Top =  originalControlTopLeft.Y + OverallMoveDistance.Y;
                    continue;
                }

                Point offsetFromSnapAnchor = originalControlTopLeft - snapPoint;
                Point snapAnchor = snapPoint + OverallMoveDistance;
                Point newPositionOfAnchor = Snap(snapAnchor, _snapGrid);
                // For some reason Avalonia rounds at some point during Canvas.SetTop and Canvas.SetLeft, this stops controls from 'wiggling'
                newPositionOfAnchor = new(Math.Round(newPositionOfAnchor.X), Math.Round(newPositionOfAnchor.Y));

                Point newControlLocation = newPositionOfAnchor + offsetFromSnapAnchor;

                item.Left = newControlLocation.X;
                item.Top = newControlLocation.Y;
            }
        
            _parent.RaiseEvent(new MoveEventArgs(SelectAndMove.MoveEvent, _parent)
            {
                LastIncrement = increment,
                TotalMovement = OverallMoveDistance,
                MovedItems = _movingItems,
            });
        }
    }

    public bool IsActive => !_isDisposed && _hasStarted;

    private bool StartIfDistanceSufficient()
    {
        if (_hasStarted || OverallMoveDistance.Length < _minimumDistance) return false;
        _hasStarted = true;
        
        foreach (var (item, _) in _moving)
        {
            item.SetIsDragging(true);
            ZIndexLayerManger.BringToFront(item);   
        }
        
        _parent.RaiseEvent(new MoveEventArgs(SelectAndMove.MoveStartedEvent, _parent)
        {
            LastIncrement = Vector.Zero,
            TotalMovement = Vector.Zero,
            MovedItems = _movingItems,
        });

        return true;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        foreach (var (item, _) in _moving)
        {
            item.SetIsDragging(false);
        }
        _parent.RaiseEvent(new MoveEventArgs(SelectAndMove.MoveEndedEvent, _parent)
        {
            LastIncrement = Vector.Zero,
            TotalMovement = OverallMoveDistance,
            MovedItems = _movingItems,
        });
    }
    
    private static Point Snap(Point point, Rect snapGrid)
    {
        return point
            .WithX(snapGrid.Width == 0.0 ? point.X : Math.Round((point.X - snapGrid.X) / snapGrid.Width) * snapGrid.Width + snapGrid.X)
            .WithY(snapGrid.Height == 0.0 ? point.Y : Math.Round((point.Y - snapGrid.Y) / snapGrid.Height) * snapGrid.Height + snapGrid.Y);
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