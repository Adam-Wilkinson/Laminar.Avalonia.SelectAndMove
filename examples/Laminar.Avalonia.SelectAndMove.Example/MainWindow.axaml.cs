using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Reactive;
using CommunityToolkit.Mvvm.Input;

namespace Laminar.Avalonia.SelectAndMove.Example;
public partial class MainWindow : Window
{
    public static readonly StyledProperty<IEnumerable<string>> SelectedControlsProperty = AvaloniaProperty.Register<MainWindow, IEnumerable<string>>(nameof(SelectedControls));

    public static readonly SnapMode[] AllSnapModes = (SnapMode[])Enum.GetValues(typeof(SnapMode));

    public static readonly MouseButton[] AllMouseButtons = (MouseButton[])Enum.GetValues(typeof(MouseButton));
    
    public MainWindow()
    {
        DataContext = this;
        InitializeComponent();

        SelectAndMove.IsSelectedProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<bool>>((obj) =>
        {
            SelectedControls = GetSelectedControls();
        }));
        
        SelectedControls = GetSelectedControls();
    }

    public IEnumerable<string> SelectedControls
    {
        get => GetValue(SelectedControlsProperty);
        set => SetValue(SelectedControlsProperty, value);
    }

    [RelayCommand]
    private void FitToControls(double margin)
    {
        ExampleSelectAndMove.FitViewToChildren(margin);
    }

    private IEnumerable<string> GetSelectedControls()
    {
        foreach (Control control in ExampleSelectAndMove.Children)
        {
            if (SelectAndMove.GetIsSelected(control))
            {
                yield return control.Name ?? "Unnamed Control";
            }
        }
    }
}
