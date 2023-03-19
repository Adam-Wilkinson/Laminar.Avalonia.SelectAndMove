# Laminar.Avalonia.SelectAndMove

SelectAndMove is a lightweight, modular Avalonia control for selecting and moving controls. 

![Example Screenshot](./Images/Example_Screenshot.png "An example of some controls in the SelectAndMove environment")
[See source code for this example](examples/Laminar.Avalonia.SelectAndMove.Example/MainWindow.axaml)

## Implementation Details
The core SelectAndMove class is a Canvas with several GestureRecognisers that manage the select, pan, zoom, move, and box select gestures. These are designed to be portable and can go on any other Panel - making a new Canvas and adding these GestureRecognisers gives identical functionality to the SelectAndMove class.

Also provided is a BackgroundGridLines object which is designed to be added as a child to the SelectAndMove canvas. It adds grid lines that automatically adjusts to the current zoom level, and provides a "SnapGrid" property that can be bound to the SelectAndMove canvas to snap controls to the grid lines (shown in the [example](examples/Laminar.Avalonia.SelectAndMove.Example/MainWindow.axaml))

Thanks to the [PanAndZoom](https://github.com/wieslawsoltes/PanAndZoom) for name and code inspiration. If all you need is the Pan and Zoom functionality, PanAndZoom may be easier to use.

## Resources
[GitHub Repository](https://github.com/Adam-Wilkinson/Laminar.Avalonia.SelectAndMove)

[Nuget Package]()

## License

SelectAndMove is licensed under the [MIT license](LICENSE.TXT)