using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;

namespace Laminar.Avalonia.SelectAndMove.MvvmExample;

public partial class MainWindow : Window
{
    public static readonly StyledProperty<IRelayCommand<Point>?> AddElementCommandProperty = AvaloniaProperty.Register<MainWindow, IRelayCommand<Point>?>(nameof(AddElementCommand));

    public IRelayCommand<Point>? AddElementCommand
    {
        get => GetValue(AddElementCommandProperty);
        set => SetValue(AddElementCommandProperty, value);
    }
    
    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectAndMove_DoubleTapped(object? sender, TappedEventArgs e)
    {
        AddElementCommand?.Execute(e.GetPosition(SelectAndMove));
    }
}