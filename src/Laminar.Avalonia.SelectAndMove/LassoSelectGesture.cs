using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace Laminar.Avalonia.SelectAndMove;

public class LassoSelectGesture : SelectingGestureRecognizer
{
    private readonly List<Point> _allPoints = [];

    private readonly Polyline _preview = new()
    {
        Stroke = Brushes.Gray,
        StrokeThickness = 2.5,
        StrokeDashArray = [7,7],
        Fill = new SolidColorBrush(Colors.Gray, 0.3)
    };
    
    private double _strokeOffset;

    static LassoSelectGesture()
    {
        CursorDecorationTemplateProperty.OverrideDefaultValue<LassoSelectGesture>(new FuncTemplate<Control>(() =>
            new Viewbox
            {
                Width = 25,
                Height = 25, 
                Margin = new Thickness(0, 0, 15, 0),
                ClipToBounds = false,
                Child = new Path
                {
                    Data = PathGeometry.Parse("M0,0 C0,15 30,15 30,0 C30,-15 0,-15 0,0 M8,11 C0,23 25,20 25,15 M6,11 C6,14 10,14 10,11 C10,7 6,7 6,11"),
                    Stroke = Brushes.Gray,
                    StrokeThickness = 2.5
                }
            }));
    }
    
    protected override void OnBeginGesture(PointerEventArgs e)
    {
        base.OnBeginGesture(e);
        DrawingCanvas?.Children.Add(_preview);
    }

    protected override Geometry? CreateUpdatedSelectionGeometry(PointerEventArgs mostRecentArgs)
    {
        _allPoints.Add(mostRecentArgs.GetPosition(DrawingCanvas));

        _preview.Points = [];
        _preview.Points = _allPoints;
        _preview.StrokeDashOffset = _strokeOffset++ * -0.3;

        return new PolylineGeometry(_allPoints, true);
    }

    protected override void Cleanup()
    {
        base.Cleanup();
        DrawingCanvas?.Children.Remove(_preview);
    }
}