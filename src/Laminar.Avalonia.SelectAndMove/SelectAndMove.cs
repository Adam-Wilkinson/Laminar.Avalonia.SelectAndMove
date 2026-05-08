using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Laminar.Avalonia.SelectAndMove.GestureRecognizers;

namespace Laminar.Avalonia.SelectAndMove;

public class SelectAndMove : ItemsControl
{
    public static readonly AttachedProperty<bool> IsMovableProperty = AvaloniaProperty.RegisterAttached<SelectAndMove, AvaloniaObject, bool>("IsMovable", true);

    public static readonly StyledProperty<KeyModifiers> SelectManyKeyModifiersProperty = SelectGesture.SelectManyKeyModifiersProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<Rectangle> SelectionBoxProperty = BoxSelectGesture.SelectionBoxProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<MouseButton> BoxSelectMouseButtonProperty = BoxSelectGesture.BoxSelectMouseButtonProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<MouseButton> PanMouseButtonProperty = AvaloniaProperty.Register<SelectAndMove, MouseButton>(nameof(PanMouseButton), MouseButton.Middle);

    
    public static readonly StyledProperty<double> ZoomSpeedProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ZoomSpeed), 1.0);

    public static readonly StyledProperty<double> ViewZoomProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewZoom), 1.0);

    public static readonly StyledProperty<double> ViewTranslateXProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewTranslateX));
    
    public static readonly StyledProperty<double> ViewTranslateYProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewTranslateY));
    
    
    public static readonly StyledProperty<Rect> SnapGridProperty = MoveSelectionGesture.SnapGridProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<SnapMode> SnapModeProperty = MoveSelectionGesture.SnapModeProperty.AddOwner<SelectAndMove>();
    
    public static readonly StyledProperty<double> MajorLineSeparationProperty = BackgroundGridLines.MajorLineSeparationProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<double> MajorLineThicknessProperty = BackgroundGridLines.MajorLineThicknessProperty.AddOwner<SelectAndMove>();
        
    public static readonly StyledProperty<int> MinorLineCountProperty = BackgroundGridLines.MinorLineCountProperty.AddOwner<SelectAndMove>();
    
    public static readonly StyledProperty<IBrush> LineBrushProperty = BackgroundGridLines.LineBrushProperty.AddOwner<SelectAndMove>();
    
    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new Canvas());

    public static bool GetIsMovable(AvaloniaObject element) => element.GetValue(IsMovableProperty);

    public static void SetIsMovable(AvaloniaObject element, bool value) => element.SetValue(IsMovableProperty, value);

    private PointerEventArgs? _previousPanArgs;
    private bool _blockRenderRecalculation;
    private Visual? _transformRoot;
    
    static SelectAndMove()
    {
        ViewZoomProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        ViewTranslateXProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        ViewTranslateYProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        
        ItemsPanelProperty.OverrideDefaultValue<SelectAndMove>(DefaultPanel);
        BackgroundProperty.OverrideDefaultValue<SelectAndMove>(Brush.Parse("#00000000"));
        Selection.IsScopeProperty.OverrideDefaultValue<SelectAndMove>(true);

        ResourceInclude selectAndMoveTheme = new((Uri?)null)
        {
            Source = new Uri("avares://Laminar.Avalonia.SelectAndMove/SelectAndMoveTheme.axaml")
        };
        Application.Current?.Resources.MergedDictionaries.Add(selectAndMoveTheme);
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

    public double ViewZoom
    {
        get => GetValue(ViewZoomProperty);
        set => SetValue(ViewZoomProperty, value);
    }

    public double ViewTranslateX
    {
        get => GetValue(ViewTranslateXProperty);
        set => SetValue(ViewTranslateXProperty, value);
    }

    public double ViewTranslateY
    {
        get => GetValue(ViewTranslateYProperty);
        set => SetValue(ViewTranslateYProperty, value);
    }
    
    public double MajorLineSeparation
    {
        get => GetValue(MajorLineSeparationProperty);
        set => SetValue(MajorLineSeparationProperty, value);
    }

    public int MinorLineCount
    {
        get => GetValue(MinorLineCountProperty);
        set => SetValue(MinorLineCountProperty, value);
    }

    public double MajorLineThickness
    {
        get => GetValue(MajorLineThicknessProperty);
        set => SetValue(MajorLineThicknessProperty, value);
    }

    public IBrush LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Matrix ComputeCurrentTransform() => Matrix.CreateTranslation(ViewTranslateX, ViewTranslateY) *
                                               Matrix.CreateScale(ViewZoom, ViewZoom);
    
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _transformRoot = e.NameScope.Find<Visual>("PART_TransformRoot");
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        container.SetValue(Selection.IsSelectableProperty, true, BindingPriority.Style);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        Point cursorBefore = e.GetPosition(_transformRoot);
        ViewZoom *= Math.Exp(ZoomSpeed * e.Delta.Y / 5);
        Point positionDelta = e.GetPosition(_transformRoot) - cursorBefore;
        _blockRenderRecalculation = true;
        ViewTranslateX += positionDelta.X;
        ViewTranslateY +=  positionDelta.Y;
        _blockRenderRecalculation = false;
        RecalculateRenderTransform();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!ButtonIsPressed(e.Properties, PanMouseButton))
        {
            _previousPanArgs = null;
            return;
        }

        if (_previousPanArgs is not null)
        {
            _blockRenderRecalculation = true;
            Point delta = e.GetPosition(_transformRoot) - _previousPanArgs.GetPosition(_transformRoot);
            ViewTranslateX += delta.X;
            ViewTranslateY += delta.Y;
            _blockRenderRecalculation = false;
            RecalculateRenderTransform();
        }

        _previousPanArgs = e;
    }

    public void ResetView()
    {
        _blockRenderRecalculation = true;
        ViewZoom = 1.0;
        ViewTranslateX = 0;
        ViewTranslateY = 0;
        _blockRenderRecalculation = false;
        RecalculateRenderTransform();
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
        Vector zoomAmounts = bounds.Size / newView.Size;
        double zoomAmount = Math.Min(zoomAmounts.X, zoomAmounts.Y);
        Size offsetFromTopLeft = (bounds.Size - newView.Size) / 2;
        Point topLeft = newView.TopLeft - new Point(offsetFromTopLeft.Width, offsetFromTopLeft.Height);
        _blockRenderRecalculation = true;
        ViewTranslateX = -topLeft.X;
        ViewTranslateY = -topLeft.Y;
        ViewZoom = zoomAmount;
        _blockRenderRecalculation = false;
        RecalculateRenderTransform();
    }
    
    private void RecalculateRenderTransform()
    {
        if (_blockRenderRecalculation) return;
        _transformRoot?.RenderTransform = new MatrixTransform(ComputeCurrentTransform());
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
