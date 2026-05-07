using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Laminar.Avalonia.SelectAndMove.GestureRecognizers;

namespace Laminar.Avalonia.SelectAndMove;

public class SelectAndMove : ItemsControl
{
    private const string ItemsPresenterName = "PART_ItemsPresenter";

    public static readonly AttachedProperty<bool> IsMovableProperty = AvaloniaProperty.RegisterAttached<SelectAndMove, AvaloniaObject, bool>("IsMovable", true);

    public static readonly StyledProperty<KeyModifiers> SelectManyKeyModifiersProperty = SelectGesture.SelectManyKeyModifiersProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<Rectangle> SelectionBoxProperty = BoxSelectGesture.SelectionBoxProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<MouseButton> BoxSelectMouseButtonProperty = BoxSelectGesture.BoxSelectMouseButtonProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<MouseButton> PanMouseButtonProperty = PanGesture.PanMouseButtonProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<double> ZoomSpeedProperty = ZoomGesture.ZoomSpeedProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<double> CurrentZoomProperty = ZoomGesture.CurrentZoomProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<Rect> SnapGridProperty = MoveSelectionGesture.SnapGridProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<SnapMode> SnapModeProperty = MoveSelectionGesture.SnapModeProperty.AddOwner<SelectAndMove>();

    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new Canvas());

    public static bool GetIsMovable(AvaloniaObject element) => element.GetValue(IsMovableProperty);

    public static void SetIsMovable(AvaloniaObject element, bool value) => element.SetValue(IsMovableProperty, value);
    
    static SelectAndMove()
    {
        ItemsPanelProperty.OverrideDefaultValue<SelectAndMove>(DefaultPanel);
        ClipToBoundsProperty.OverrideDefaultValue<SelectAndMove>(true);
        HorizontalAlignmentProperty.OverrideDefaultValue<SelectAndMove>(HorizontalAlignment.Stretch);
        VerticalAlignmentProperty.OverrideDefaultValue<SelectAndMove>(VerticalAlignment.Stretch);
        BackgroundProperty.OverrideDefaultValue<SelectAndMove>(Brush.Parse("#00000000"));

        ResourceInclude samTheme = new((Uri?)null)
        {
            Source = new Uri("avares://Laminar.Avalonia.SelectAndMove/SelectAndMoveTheme.axaml")
        };
        Application.Current?.Resources.MergedDictionaries.Add(samTheme);
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
        
        GestureRecognizers.Add(new BoxSelectGesture(this) { 
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

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        Selection.SetIsSelectable(container, true);
    }

    public void ResetView()
    {
        CurrentZoom = 1.0;
        ItemsPanelRoot?.RenderTransform = new MatrixTransform();
    }

    public void FitViewToChildren(double margin)
    {
        if (ItemsPanelRoot is null) return;
        Rect overallBounds = new(0, 0, 0, 0);
        foreach (Control control in ItemsPanelRoot.Children)
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
        if (ItemsPanelRoot is null) return;
        Vector zoomAmounts = bounds.Size / newView.Size;
        double zoomAmount = Math.Min(zoomAmounts.X, zoomAmounts.Y);
        CurrentZoom = zoomAmount;

        Size offsetFromTopLeft = (bounds.Size - newView.Size) / 2;
        Point topLeft = newView.TopLeft - new Point(offsetFromTopLeft.Width, offsetFromTopLeft.Height);

        ItemsPanelRoot.RenderTransform = new MatrixTransform(Matrix.CreateTranslation(-topLeft));
        Matrix controlTransform = ZoomGesture.GetTransform(ItemsPanelRoot, this, bounds.Center - bounds.TopLeft, zoomAmount) * ItemsPanelRoot.RenderTransform.Value;
        ItemsPanelRoot.RenderTransform = new MatrixTransform(controlTransform);
    }
}
