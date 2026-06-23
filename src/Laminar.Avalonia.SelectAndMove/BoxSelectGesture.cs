using Avalonia;
using Avalonia.Animation;
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
    
    private PointerEventArgs? _originalClick;
    private Rectangle _selectionBox;
    private bool _selectionBoxAdded;

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

    protected override Geometry? CreateUpdatedSelectionGeometry(PointerEventArgs mostRecentArgs)
    {
        if (_originalClick is null)
        {
            _originalClick = mostRecentArgs;
            return null;
        }
        
        Rect drawnRect = new Rect(_originalClick.GetPosition(DrawingCanvas), mostRecentArgs.GetPosition(DrawingCanvas)).Normalize();
        Canvas.SetLeft(_selectionBox, drawnRect.Left);
        Canvas.SetTop(_selectionBox, drawnRect.Top);
        _selectionBox.Width = drawnRect.Width;
        _selectionBox.Height = drawnRect.Height;

        if (!_selectionBoxAdded)
        {
            _selectionBoxAdded = true;
            DrawingCanvas?.Children.Add(_selectionBox);
        }

        return new RectangleGeometry(drawnRect);
    }

    protected override void Cleanup()
    {
        _originalClick = null;
        DrawingCanvas?.Children.Remove(_selectionBox);
        _selectionBoxAdded = false;
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
