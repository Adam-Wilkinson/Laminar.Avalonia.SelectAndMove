using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Laminar.Avalonia.SelectAndMove.MvvmExample;

public partial class SelectAndMoveItemModel : ObservableObject
{
    [ObservableProperty] public required partial string Text { get; set; }

    [ObservableProperty] public required partial double X { get; set; }
    
    [ObservableProperty] public required partial double Y { get; set; }
}