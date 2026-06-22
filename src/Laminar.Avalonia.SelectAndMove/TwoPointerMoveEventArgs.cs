using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Laminar.Avalonia.SelectAndMove;

public class TwoPointerMoveEventArgs() : RoutedEventArgs(TwoPointerMoveGestureRecognizer.TwoPointerMoveEvent)
{
    public required Vector CenterDelta { get; init; }
    public required Vector ScaleDelta { get; init; }
    public required IPointer FirstPointer { get; init; }
    public required IPointer SecondPointer { get; init; }
}