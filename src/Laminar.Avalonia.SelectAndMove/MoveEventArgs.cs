using System.Collections;
using Avalonia;
using Avalonia.Interactivity;

namespace Laminar.Avalonia.SelectAndMove;

public class MoveEventArgs(RoutedEvent routedEvent, object? source) : RoutedEventArgs(routedEvent, source)
{
    public required Vector TotalMovement { get; init; }

    public required Vector LastIncrement { get; init; }

    public required IList MovedItems { get; init; }
}