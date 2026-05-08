using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Laminar.Avalonia.SelectAndMove.Example;
public partial class MainWindow : Window
{
    public static readonly SnapMode[] AllSnapModes = (SnapMode[])Enum.GetValues(typeof(SnapMode));

    public static readonly MouseButton[] AllMouseButtons = (MouseButton[])Enum.GetValues(typeof(MouseButton));
    
    public MainWindow()
    {
        InitializeComponent();
    }
}
