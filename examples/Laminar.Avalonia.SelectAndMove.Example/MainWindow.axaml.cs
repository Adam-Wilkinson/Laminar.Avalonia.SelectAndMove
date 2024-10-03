using System;
using System.Collections.Generic;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;

namespace Laminar.Avalonia.SelectAndMove.Example;
public partial class MainWindow : Window
{
    public static readonly StyledProperty<IEnumerable<string>> SelectedControlsProperty = AvaloniaProperty.Register<MainWindow, IEnumerable<string>>(nameof(SelectedControls));

    public SnapMode[] AllSnapModes { get; } = (SnapMode[])Enum.GetValues(typeof(SnapMode));

    public MouseButton[] AllMouseButtons { get; } = (MouseButton[])Enum.GetValues(typeof(MouseButton));

    public ReactiveCommand<object?, Unit> FitToControlsCommand { get; }

    public MainWindow()
    {
        DataContext = this;
        InitializeComponent();

        SelectAndMove.IsSelectedProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<bool>>((obj) =>
        {
            SelectedControls = GetSelectedControls();
        }));

        FitToControlsCommand = ReactiveCommand.Create<object?>(FitToControls);

        SelectedControls = GetSelectedControls();
    }

    public IEnumerable<string> SelectedControls
    {
        get => GetValue(SelectedControlsProperty);
        set => SetValue(SelectedControlsProperty, value);
    }

    public void FitToControls(object? marginObject)
    {
        if (marginObject is string marginString && double.TryParse(marginString, out double value))
        {
            ExampleSelectAndMove.FitViewToChildren(value);
        }
    }

    public bool CanFitToControls(string margin)
    {
        return double.TryParse(margin, out _) && ExampleSelectAndMove.Children.Count > 0;
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
