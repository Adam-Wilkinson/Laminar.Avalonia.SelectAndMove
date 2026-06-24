using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove;

public class BoxSelectGesture : SelectingGestureRecognizer
{
    public static readonly AttachedProperty<ITemplate<Rectangle?>?> SelectionBoxTemplateProperty = AvaloniaProperty.RegisterAttached<BoxSelectGesture, StyledElement, ITemplate<Rectangle?>?>("SelectionBoxTemplate", inherits: true);
    public static ITemplate<Rectangle?>? GetSelectionBoxTemplate(StyledElement element) => element.GetValue(SelectionBoxTemplateProperty);
    public static void SetSelectionBoxTemplate(StyledElement element, ITemplate<Rectangle?>? template) => element.SetValue(SelectionBoxTemplateProperty, template);
    
    private Point _originalClickPoint;
    private Rectangle _selectionBox;

    public ITemplate<Rectangle?>? SelectionBoxTemplate
    {
        get => GetValue(SelectionBoxTemplateProperty);
        set => SetValue(SelectionBoxTemplateProperty, value);
    }
    
    static BoxSelectGesture()
    {
        SelectionBoxTemplateProperty.Changed.AddClassHandler<BoxSelectGesture>((bsg, args) => bsg.SelectionBoxChanged(args));
    }

    public BoxSelectGesture()
    {
        _selectionBox = SelectionBoxTemplate?.Build() ?? new Rectangle
        {
            Stroke = Brushes.Red,
            StrokeThickness = 3
        };
    }

    protected override void OnBeginGesture(PointerEventArgs e)
    {
        base.OnBeginGesture(e);
        _originalClickPoint = e.GetPosition(DrawingCanvas);
        UpdateSelectionBox(e);
        DrawingCanvas?.Children.Add(_selectionBox);
    }

    protected override Geometry? CreateUpdatedSelectionGeometry(PointerEventArgs mostRecentArgs) 
        => new RectangleGeometry(UpdateSelectionBox(mostRecentArgs));

    private Rect UpdateSelectionBox(PointerEventArgs e)
    {
        Rect drawnRect = new Rect(_originalClickPoint, e.GetPosition(DrawingCanvas)).Normalize();
        Canvas.SetLeft(_selectionBox, drawnRect.Left);
        Canvas.SetTop(_selectionBox, drawnRect.Top);
        _selectionBox.Width = drawnRect.Width;
        _selectionBox.Height = drawnRect.Height;
        return drawnRect;
    }

    protected override void Cleanup()
    {
        base.Cleanup();
        _originalClickPoint = new Point(double.NaN, double.NaN);
        DrawingCanvas?.Children.Remove(_selectionBox);
    }

    private void SelectionBoxChanged(AvaloniaPropertyChangedEventArgs args)
    {
        _selectionBox = args.GetNewValue<ITemplate<Rectangle?>?>()?.Build() ?? new Rectangle
        {
            Stroke = Brushes.Red,
            StrokeThickness = 3
        };
    }
}
