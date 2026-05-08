using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Laminar.Avalonia.SelectAndMove.MvvmExample;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] public partial string NewElementName { get; set; } = "New Element Name";

    [ObservableProperty] public partial IAvaloniaReadOnlyList<InputElement>? Selection { get; set; }
    
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

    [RelayCommand]
    private void Delete()
    {
        if (Selection is null || Selection.Count == 0) return;
        foreach (var item in Selection.Select(x => x.DataContext).Cast<SelectAndMoveItem>().ToList())
        {
            Items.Remove(item);
        }
    }
}