using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public class BoxSelectGesture : GestureRecognizerBase
{
    public static readonly StyledProperty<KeyModifiers> SelectManyKeyModifiersProperty = 
        SelectGesture.SelectManyKeyModifiersProperty.AddOwner<SelectGesture>();

    public static readonly StyledProperty<Rectangle> SelectionBoxProperty =
        AvaloniaProperty.Register<BoxSelectGesture, Rectangle>(nameof(SelectionBox), new Rectangle { Stroke = Brushes.Red, StrokeThickness = 2 });

    public static readonly StyledProperty<MouseButton> BoxSelectMouseButtonProperty =
        AvaloniaProperty.Register<BoxSelectGesture, MouseButton>(nameof(BoxSelectMouseButton), MouseButton.Left);

    bool childAdded = false;
    Point startingPoint = new();

    public KeyModifiers SelectManyKeyModifiers
    {
        get => GetValue(SelectManyKeyModifiersProperty);
        set => SetValue(SelectManyKeyModifiersProperty, value);
    }

    public Rectangle SelectionBox
    {
        get => GetValue(SelectionBoxProperty);
        set => SetValue(SelectionBoxProperty, value);
    }

    public MouseButton BoxSelectMouseButton
    {
        get => GetValue(BoxSelectMouseButtonProperty);
        set => SetValue(BoxSelectMouseButtonProperty, value);
    }

    public override void PointerPressed(PointerPressedEventArgs e)
    {
        if (!ButtonIsPressed(e.GetCurrentPoint(Target).Properties, BoxSelectMouseButton))
        {
            return;
        }

        if (Target is not Canvas)
        {
            return;
        }

        Track(e.Pointer);

        SelectAndMove.SetIsSelectable(SelectionBox, false);
        startingPoint = e.GetPosition(Target);
    }

    protected override void TrackedPointerMoved(PointerEventArgs e)
    {
        if (Target is not Canvas targetCanvas)
        {
            return;
        }

        if (!childAdded)
        {
            targetCanvas.Children.Add(SelectionBox);
            childAdded = true;
        }

        Rect selectionRect = new Rect(startingPoint, e.GetPosition(Target)).Normalize();

        Canvas.SetLeft(SelectionBox, selectionRect.Left);
        Canvas.SetTop(SelectionBox, selectionRect.Top);
        SelectionBox.Width = selectionRect.Width;
        SelectionBox.Height = selectionRect.Height;

        foreach (IControl control in targetCanvas.Children)
        {
            if (e.KeyModifiers != SelectManyKeyModifiers)
            {
                SelectAndMove.SetIsSelected(control, false);
            }

            if (SelectAndMove.GetIsSelectable(control) && BoxIntersectsControl(selectionRect, control))
            {
                SelectAndMove.SetIsSelected(control, true);
            }
        }
    }

    protected override void EndGesture()
    {
        if (Target is IPanel targetPanel)
        {
            targetPanel.Children.Remove(SelectionBox);
            childAdded = false;
        }
    }

    private bool BoxIntersectsControl(Rect selectionRect, IControl control)
    {
        Rect rectInLocal = selectionRect.TransformToAABB(Target.TransformToVisual(control)!.Value);

        if (control is ICustomHitResolver customHitResolver)
        {
            return customHitResolver.IntersectsWithRectangle(rectInLocal);
        }

        if (control is Shape shape && shape.DefiningGeometry is Geometry shapeGeometry)
        {
            IPlatformRenderInterface? factory = AvaloniaLocator.Current.GetService<IPlatformRenderInterface>();

            IGeometryImpl rectGeometryImpl = factory!.CreateRectangleGeometry(rectInLocal);
            IGeometryImpl rectAndShapeIntersect = shapeGeometry.PlatformImpl.Intersect(rectGeometryImpl);
            return rectAndShapeIntersect.Bounds.Width > 0 || rectAndShapeIntersect.Bounds.Height > 0;
        }

        return new Rect(control.Bounds.Size).Intersects(rectInLocal);
    }

    private static bool ButtonIsPressed(PointerPointProperties pointerProperties, MouseButton button) => button switch
    {
        MouseButton.Left => pointerProperties.IsLeftButtonPressed,
        MouseButton.Right => pointerProperties.IsRightButtonPressed,
        MouseButton.Middle => pointerProperties.IsMiddleButtonPressed,
        MouseButton.XButton1 => pointerProperties.IsXButton1Pressed,
        MouseButton.XButton2 => pointerProperties.IsXButton2Pressed,
        _ => false,
    };
}
