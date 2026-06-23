using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Laminar.Avalonia.SelectAndMove.MvvmExample;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] public partial string NewElementName { get; set; } = "New Element Name";

    [ObservableProperty] public partial IReadOnlyList<object>? Selection { get; set; }
    
    public ObservableCollection<SelectAndMoveItemModel> Items { get; } = [];
    
    [RelayCommand]
    private void AddElement(Point point)
    {
        Items.Add(new SelectAndMoveItemModel
        {
            Text = NewElementName,
            X = point.X,
            Y = point.Y
        });
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selection is null || Selection.Count == 0) return;
        foreach (var item in Selection.Cast<SelectAndMoveItemModel>().ToList())
        {
            Items.Remove(item);
        }
    }

    [RelayCommand]
    private void SelectMostRecent(int count)
    {
        Selection = Items.Skip(Items.Count - count).Take(count).ToList();
    }
}