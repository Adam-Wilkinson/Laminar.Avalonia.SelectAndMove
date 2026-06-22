using Avalonia;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;

namespace Laminar.Avalonia.SelectAndMove;

public class TwoPointerMoveGestureRecognizer : GestureRecognizer
{
    public static readonly RoutedEvent<TwoPointerMoveEventArgs> TwoPointerMoveEvent = RoutedEvent.Register<TwoPointerMoveGestureRecognizer, TwoPointerMoveEventArgs>("TwoTouch", RoutingStrategies.Bubble);

    private IPointer? _firstPointer;
    private IPointer? _secondPointer;
    private PointerEventArgs? _firstPointerMostRecentEventArgs;
    private PointerEventArgs? _secondPointerMovedEventArgs;
    
    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        if (_firstPointer is not null && _secondPointer is not null
            && !Equals(_firstPointer, e.Pointer) && !Equals(_secondPointer, e.Pointer))
        {
            EndGesture();
            return;
        }

        if (Equals(_firstPointer, e.Pointer))
        {
            _firstPointerMostRecentEventArgs = e;
            return;
        }

        if (Equals(_secondPointer, e.Pointer))
        {
            _secondPointerMovedEventArgs = e;
            return;
        }
        
        if (_firstPointer is null)
        {
            _firstPointer = e.Pointer;
            _firstPointerMostRecentEventArgs = e;
            return;
        }

        if (_secondPointer is null)
        {
            _secondPointer = e.Pointer;
            _secondPointerMovedEventArgs = e;
            Capture(_firstPointer);
            Capture(_secondPointer);
        }
    }

    private void EndGesture()
    {
        _firstPointer?.Capture(null);
        _firstPointer = null;
        _firstPointerMostRecentEventArgs = null;
        
        _secondPointer?.Capture(null);
        _secondPointer = null;
        _secondPointerMovedEventArgs = null;
    }

    protected override void PointerReleased(PointerReleasedEventArgs e)
    {
        if (_firstPointer is not null 
            && _secondPointer is not null 
            && (Equals(e.Pointer, _firstPointer) || Equals(e.Pointer, _secondPointer)))
        {
            EndGesture();
        }
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (_firstPointerMostRecentEventArgs is null || _secondPointerMovedEventArgs is null) return;
        
        if (Equals(_firstPointer, e.Pointer) && _secondPointer is not null)
        {
            Target?.RaiseEvent(ComputeTwoPointerMove(_firstPointerMostRecentEventArgs, 
                e, _secondPointerMovedEventArgs,
                _secondPointerMovedEventArgs));
            _firstPointerMostRecentEventArgs = e;
            return;
        }

        if (Equals(_secondPointer, e.Pointer) && _firstPointer is not null)
        {
            Target?.RaiseEvent(ComputeTwoPointerMove(_firstPointerMostRecentEventArgs, _firstPointerMostRecentEventArgs,
                _secondPointerMovedEventArgs, e));
            _secondPointerMovedEventArgs = e;
        }
    }

    protected override void PointerCaptureLost(IPointer pointer)
    {
        EndGesture();
    }

    private static TwoPointerMoveEventArgs ComputeTwoPointerMove(PointerEventArgs firstPointerBefore,
        PointerEventArgs firstPointerAfter, PointerEventArgs secondPointerBefore, PointerEventArgs secondPointerAfter)
    {
        var firstPositionBefore = firstPointerBefore.GetPosition(null);
        var secondPositionBefore =  secondPointerBefore.GetPosition(null);
        var firstPositionAfter = firstPointerAfter.GetPosition(null);
        var secondPositionAfter = secondPointerAfter.GetPosition(null);
        
        var centralPositionBefore = (firstPositionBefore + secondPositionBefore) / 2;
        var xSeparationBefore = Math.Abs(firstPositionBefore.X - secondPositionBefore.X);
        var ySeparationBefore = Math.Abs(firstPositionBefore.Y - secondPositionBefore.Y);

        var centralPositionAfter = (firstPositionAfter + secondPositionAfter) / 2;
        var xSeparationAfter = Math.Abs(firstPositionAfter.X - secondPositionAfter.X);
        var ySeparationAfter = Math.Abs(firstPositionAfter.Y - secondPositionAfter.Y);

        return new TwoPointerMoveEventArgs
        {
            CenterDelta = centralPositionAfter - centralPositionBefore,
            ScaleDelta = new Vector(xSeparationAfter / xSeparationBefore, ySeparationAfter / ySeparationBefore),
            FirstPointer = firstPointerBefore.Pointer,
            SecondPointer = secondPointerBefore.Pointer,
        };
    }
}