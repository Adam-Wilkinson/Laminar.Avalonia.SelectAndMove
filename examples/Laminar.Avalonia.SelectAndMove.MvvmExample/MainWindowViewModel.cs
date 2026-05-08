using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Laminar.Avalonia.SelectAndMove.MvvmExample;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] public partial string NewElementName { get; set; } = "New Element Name";

    public ObservableCollection<SelectAndMoveItem> Items { get; } = [];

    [RelayCommand]
    private void AddElement(Point point)
    {
        Items.Add(new SelectAndMoveItem
        {
            Text = NewElementName,
            Position = point
        });
    }
}