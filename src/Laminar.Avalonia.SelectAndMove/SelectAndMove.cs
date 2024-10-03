using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Laminar.Avalonia.SelectAndMove.GestureRecognizers;

namespace Laminar.Avalonia.SelectAndMove;

public class SelectAndMove : Canvas
{
    public static readonly AttachedProperty<bool> IsSelectedProperty = AvaloniaProperty.RegisterAttached<SelectAndMove, AvaloniaObject, bool>("IsSelected", false);

    public static readonly AttachedProperty<bool> IsSelectableProperty = AvaloniaProperty.RegisterAttached<SelectAndMove, AvaloniaObject, bool>("IsSelectable", true);

    public static readonly AttachedProperty<bool> IsMovableProperty = AvaloniaProperty.RegisterAttached<SelectAndMove, AvaloniaObject, bool>("IsMovable", true);

    public static readonly StyledProperty<KeyModifiers> SelectManyKeyModifiersProperty = SelectGesture.SelectManyKeyModifiersProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<Rectangle> SelectionBoxProperty = BoxSelectGesture.SelectionBoxProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<MouseButton> BoxSelectMouseButtonProperty = BoxSelectGesture.BoxSelectMouseButtonProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<MouseButton> PanMouseButtonProperty = PanGesture.PanMouseButtonProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<double> ZoomSpeedProperty = ZoomGesture.ZoomSpeedProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<double> CurrentZoomProperty = ZoomGesture.CurrentZoomProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<Rect> SnapGridProperty = MoveSelectionGesture.SnapGridProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<SnapMode> SnapModeProperty = MoveSelectionGesture.SnapModeProperty.AddOwner<SelectAndMove>();

    public static bool GetIsSelected(AvaloniaObject element) => element.GetValue(IsSelectedProperty);

    public static void SetIsSelected(AvaloniaObject element, bool value) => element.SetValue(IsSelectedProperty, value);

    public static bool GetIsSelectable(AvaloniaObject element) => element.GetValue(IsSelectableProperty);

    public static void SetIsSelectable(AvaloniaObject element, bool value) => element.SetValue(IsSelectableProperty, value);

    public static bool GetIsMovable(AvaloniaObject element) => element.GetValue(IsMovableProperty);

    public static void SetIsMovable(AvaloniaObject element, bool value) => element.SetValue(IsMovableProperty, value);

    static SelectAndMove()
    {
        ClipToBoundsProperty.OverrideDefaultValue<SelectAndMove>(true);
        HorizontalAlignmentProperty.OverrideDefaultValue<SelectAndMove>(HorizontalAlignment.Stretch);
        VerticalAlignmentProperty.OverrideDefaultValue<SelectAndMove>(VerticalAlignment.Stretch);
        BackgroundProperty.OverrideDefaultValue<SelectAndMove>(Brush.Parse("#00000000"));
    }

    public SelectAndMove()
    {
        GestureRecognizers.Add(new SelectGesture { 
            [!SelectGesture.SelectManyKeyModifiersProperty] = this[!SelectManyKeyModifiersProperty],
        });

        GestureRecognizers.Add(new MoveSelectionGesture { 
            [!MoveSelectionGesture.SnapGridProperty] = this[!SnapGridProperty], 
            [!MoveSelectionGesture.SnapModeProperty] = this[!SnapModeProperty],
        });

        GestureRecognizers.Add(new PanGesture { 
            [!PanGesture.PanMouseButtonProperty] = this[!PanMouseButtonProperty],
        });

        GestureRecognizers.Add(new BoxSelectGesture { 
            [!BoxSelectGesture.SelectManyKeyModifiersProperty] = this[!SelectManyKeyModifiersProperty],
            [!BoxSelectGesture.SelectionBoxProperty] = this[!SelectionBoxProperty],
            [!BoxSelectGesture.BoxSelectMouseButtonProperty] = this[!BoxSelectMouseButtonProperty],
        });

        GestureRecognizers.Add(new ZoomGesture { 
            [!ZoomGesture.ZoomSpeedProperty] = this[!ZoomSpeedProperty],
            [!ZoomGesture.CurrentZoomProperty] = this[(!CurrentZoomProperty).WithMode(BindingMode.TwoWay)],
            ScrollWheelListener = this,
        });
    }

    public Rect SnapGrid
    {
        get => GetValue(SnapGridProperty);
        set => SetValue(SnapGridProperty, value);
    }

    public SnapMode SnapMode
    {
        get => GetValue(SnapModeProperty);
        set => SetValue(SnapModeProperty, value);
    }

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

    public MouseButton PanMouseButton
    {
        get => GetValue(PanMouseButtonProperty);
        set => SetValue(PanMouseButtonProperty, value);
    }

    public double ZoomSpeed
    {
        get => GetValue(ZoomSpeedProperty);
        set => SetValue(ZoomSpeedProperty, value);
    }

    public double CurrentZoom
    {
        get => GetValue(CurrentZoomProperty);
        set => SetValue(CurrentZoomProperty, value);
    }

    public void ResetView()
    {
        CurrentZoom = 1.0;

        foreach (Control control in Children)
        {
            control.RenderTransform = new MatrixTransform();
        }
    }

    public void FitViewToChildren(double margin)
    {
        Rect overallBounds = new(0, 0, 0, 0);
        foreach (Control control in Children)
        {
            overallBounds = overallBounds.Union(control.Bounds);
        }

        FitViewToRect(overallBounds.Inflate(margin));
    }

    public void FitViewToRect(Rect newView)
    {
        FitToViewRectWithManualBounds(newView, Bounds);
    }

    public void FitToViewRectWithManualBounds(Rect newView, Rect bounds)
    {
        Vector zoomAmounts = bounds.Size / newView.Size;
        double zoomAmount = Math.Min(zoomAmounts.X, zoomAmounts.Y);
        CurrentZoom = zoomAmount;

        Size offsetFromTopLeft = (bounds.Size - newView.Size) / 2;
        Point topLeft = newView.TopLeft - new Point(offsetFromTopLeft.Width, offsetFromTopLeft.Height);

        foreach (Control control in Children)
        {
            control.RenderTransform = new MatrixTransform(Matrix.CreateTranslation(-topLeft));
            Matrix controlTransform = ZoomGesture.GetTransform(control, this, bounds.Center - bounds.TopLeft, zoomAmount) * control.RenderTransform.Value;
            control.RenderTransform = new MatrixTransform(controlTransform);
        }
    }
}
