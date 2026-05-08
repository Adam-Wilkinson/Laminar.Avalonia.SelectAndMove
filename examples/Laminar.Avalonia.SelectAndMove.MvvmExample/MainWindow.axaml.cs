using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;

namespace Laminar.Avalonia.SelectAndMove.MvvmExample;

public partial class MainWindow : Window
{
    public static readonly StyledProperty<IRelayCommand<Point>?> AddElementCommandProperty = AvaloniaProperty.Register<MainWindow, IRelayCommand<Point>?>(nameof(AddElementCommand));

    public static readonly StyledProperty<ICommand?> DeleteCommandProperty = AvaloniaProperty.Register<MainWindow, ICommand?>(nameof(DeleteCommand));
    
    public MainWindow()
    {
        InitializeComponent();
    }
        
    public IRelayCommand<Point>? AddElementCommand
    {
        get => GetValue(AddElementCommandProperty);
        set => SetValue(AddElementCommandProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    private void SelectAndMove_DoubleTapped(object? sender, TappedEventArgs e)
    {
        AddElementCommand?.Execute(e.GetPosition(SelectAndMove.ItemsPanelRoot));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Delete)
        {
            DeleteCommand?.Execute(null);
        }
    }
}