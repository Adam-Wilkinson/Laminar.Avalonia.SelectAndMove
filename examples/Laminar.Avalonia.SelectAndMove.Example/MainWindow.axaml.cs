using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Laminar.Avalonia.SelectAndMove.Example;
public partial class MainWindow : Window
{
    public static readonly SnapMode[] AllSnapModes = Enum.GetValues<SnapMode>();

    public static readonly MouseButton[] AllMouseButtons = Enum.GetValues<MouseButton>();
    
    public static readonly ResizeBehavior[] AllResizeBehaviors = Enum.GetValues<ResizeBehavior>();
    
    public MainWindow()
    {
        InitializeComponent();
    }
}
