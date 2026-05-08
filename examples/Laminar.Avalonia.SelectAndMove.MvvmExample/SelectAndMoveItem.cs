using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Laminar.Avalonia.SelectAndMove.MvvmExample;

public partial class SelectAndMoveItem : ObservableObject
{
    [ObservableProperty] public required partial string Text { get; set; }

    [ObservableProperty] public required partial Point Position { get; set; }
}