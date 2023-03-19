using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace Laminar.Avalonia.SelectAndMove.Example;
public partial class MainWindow : Window
{
    readonly SelectAndMove _selectAndMove;

    public static readonly StyledProperty<IEnumerable<string>> SelectedControlsProperty = AvaloniaProperty.Register<MainWindow, IEnumerable<string>>(nameof(SelectedControls));

    public MainWindow()
    {
        DataContext = this;
        InitializeComponent();

        _selectAndMove = this.FindControl<SelectAndMove>("ExampleSelectAndMove");

        SelectAndMove.IsSelectedProperty.Changed.Subscribe((obj) =>
        {
            SelectedControls = GetSelectedControls();
        });

        SelectedControls = GetSelectedControls();
    }

    public IEnumerable<string> SelectedControls
    {
        get => GetValue(SelectedControlsProperty);
        set => SetValue(SelectedControlsProperty, value);
    }

    private IEnumerable<string> GetSelectedControls()
    {
        foreach (IControl control in _selectAndMove.Children)
        {
            if (SelectAndMove.GetIsSelected(control))
            {
                yield return control.Name;
            }
        }
    }
}
