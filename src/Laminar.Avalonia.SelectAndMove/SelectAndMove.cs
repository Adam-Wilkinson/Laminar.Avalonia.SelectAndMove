using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
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

[Flags]
public enum ResizeBehavior
{
    None = 0,
    KeepHorizontalCenterline = 1 << 0,
    KeepVerticalCenterLine = 1 << 1,
    KeepZoom = 1 << 2,
    KeepCenter = KeepHorizontalCenterline | KeepVerticalCenterLine,
    KeepView = KeepZoom | KeepCenter,
}

public class SelectAndMove : ItemsControl
{
    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new Canvas());
    
    public static readonly AttachedProperty<bool> IsMovableProperty = AvaloniaProperty.RegisterAttached<SelectAndMove, AvaloniaObject, bool>("IsMovable", true);

    public static readonly StyledProperty<KeyModifiers> SelectManyKeyModifiersProperty = SelectGesture.SelectManyKeyModifiersProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<Rectangle> SelectionBoxProperty = BoxSelectGesture.SelectionBoxProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<MouseButton> BoxSelectMouseButtonProperty = BoxSelectGesture.BoxSelectMouseButtonProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<MouseButton> PanMouseButtonProperty = AvaloniaProperty.Register<SelectAndMove, MouseButton>(nameof(PanMouseButton), MouseButton.Middle);

    public static readonly StyledProperty<IReadOnlyList<object>> SelectionProperty = AvaloniaProperty.Register<SelectAndMove, IReadOnlyList<object>>(nameof(Selection), defaultBindingMode: BindingMode.TwoWay);
    
    public static readonly StyledProperty<double> ZoomSpeedProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ZoomSpeed), 1.0);

    public static readonly StyledProperty<double> ViewZoomProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewZoom), 1.0);

    public static readonly StyledProperty<double> ViewTranslateXProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewTranslateX));
    
    public static readonly StyledProperty<double> ViewTranslateYProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewTranslateY));
    
    public static readonly StyledProperty<ResizeBehavior> ResizeBehaviorProperty = AvaloniaProperty.Register<SelectAndMove, ResizeBehavior>(nameof(ResizeBehavior));
    
    public static readonly StyledProperty<Rect> SnapGridProperty = MoveSelectionGesture.SnapGridProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<SnapMode> SnapModeProperty = MoveSelectionGesture.SnapModeProperty.AddOwner<SelectAndMove>();
    
    public static readonly StyledProperty<double> MajorLineSeparationProperty = BackgroundGridLines.MajorLineSeparationProperty.AddOwner<SelectAndMove>();

    public static readonly StyledProperty<double> MajorLineThicknessProperty = BackgroundGridLines.MajorLineThicknessProperty.AddOwner<SelectAndMove>();
        
    public static readonly StyledProperty<int> MinorLineCountProperty = BackgroundGridLines.MinorLineCountProperty.AddOwner<SelectAndMove>();
    
    public static readonly StyledProperty<IBrush> LineBrushProperty = BackgroundGridLines.LineBrushProperty.AddOwner<SelectAndMove>();
    

    public static bool GetIsMovable(AvaloniaObject element) => element.GetValue(IsMovableProperty);

    public static void SetIsMovable(AvaloniaObject element, bool value) => element.SetValue(IsMovableProperty, value);

    private PointerEventArgs? _previousPanArgs;
    private bool _blockRenderRecalculation;
    private bool _selectionChanging;
    private Visual? _transformRoot;
    
    static SelectAndMove()
    {
        ViewZoomProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        ViewTranslateXProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        ViewTranslateYProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        BoundsProperty.Changed.AddClassHandler<SelectAndMove>((sam, args) => sam.BoundsChanged(args));
        
        ItemsPanelProperty.OverrideDefaultValue<SelectAndMove>(DefaultPanel);
        BackgroundProperty.OverrideDefaultValue<SelectAndMove>(Brush.Parse("#00000000"));
        Avalonia.SelectAndMove.Selection.IsScopeProperty.OverrideDefaultValue<SelectAndMove>(true);
        SelectionProperty.Changed.AddClassHandler<SelectAndMove>((sam, args) => sam.SelectionChanged(args));
        Avalonia.SelectAndMove.Selection.SelectedElementsProperty.Changed.AddClassHandler<SelectAndMove>((sam, args) => sam.SelectedElementsChanged(args));
        
        ResourceInclude selectAndMoveTheme = new((Uri?)null)
        {
            Source = new Uri("avares://Laminar.Avalonia.SelectAndMove/SelectAndMoveTheme.axaml")
        };
        Application.Current?.Resources.MergedDictionaries.Add(selectAndMoveTheme);
    }

    /// <summary>
    /// Defines the snap grid as tessellations of the given rectangle, interpreted in transformed coordinates 
    /// </summary>
    public Rect SnapGrid
    {
        get => GetValue(SnapGridProperty);
        set => SetValue(SnapGridProperty, value);
    }

    /// <summary>
    /// Defines the anchor point by which move operations are quantized to the <see cref="SnapGrid"/>
    /// </summary>
    public SnapMode SnapMode
    {
        get => GetValue(SnapModeProperty);
        set => SetValue(SnapModeProperty, value);
    }

    /// <summary>
    /// Key modifiers that must all be held down to select many items
    /// </summary>
    public KeyModifiers SelectManyKeyModifiers
    {
        get => GetValue(SelectManyKeyModifiersProperty);
        set => SetValue(SelectManyKeyModifiersProperty, value);
    }

    /// <summary>
    /// The rectangle that is used when applying a box selection. Use this to define the stroke color, fill etc.
    /// </summary>
    public Rectangle SelectionBox
    {
        get => GetValue(SelectionBoxProperty);
        set => SetValue(SelectionBoxProperty, value);
    }

    /// <summary>
    /// The mouse button used to trigger a box selection
    /// </summary>
    public MouseButton BoxSelectMouseButton
    {
        get => GetValue(BoxSelectMouseButtonProperty);
        set => SetValue(BoxSelectMouseButtonProperty, value);
    }

    /// <summary>
    /// The mouse button that triggers a pan gesture
    /// </summary>
    public MouseButton PanMouseButton
    {
        get => GetValue(PanMouseButtonProperty);
        set => SetValue(PanMouseButtonProperty, value);
    }

    /// <summary>
    /// A read-only view of the selected items that are selected by the controls.
    /// When working from an ItemsSource, this will always be a subset of that ItemsSource
    /// </summary>
    public IReadOnlyList<object> Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    /// <summary>
    /// The speed at which mouse wheel changes affect the zoom level
    /// </summary>
    public double ZoomSpeed
    {
        get => GetValue(ZoomSpeedProperty);
        set => SetValue(ZoomSpeedProperty, value);
    }

    /// <summary>
    /// The current zoom level of the view, in the range (0,infinity). 1 Implies no zoom
    /// </summary>
    public double ViewZoom
    {
        get => GetValue(ViewZoomProperty);
        set => SetValue(ViewZoomProperty, value);
    }

    /// <summary>
    /// Translation of the view in the X direction. Default is 0
    /// </summary>
    public double ViewTranslateX
    {
        get => GetValue(ViewTranslateXProperty);
        set => SetValue(ViewTranslateXProperty, value);
    }

    /// <summary>
    /// Translation of the view in the Y direction. Default is 0
    /// </summary>
    public double ViewTranslateY
    {
        get => GetValue(ViewTranslateYProperty);
        set => SetValue(ViewTranslateYProperty, value);
    }

    /// <summary>
    /// The behavior of the canvas when its bounds are changed.
    /// Use this to tell SelectAndMove to keep its horizontal center-line, vertical center-line, or zoom range on resizing.
    /// </summary>
    public ResizeBehavior ResizeBehavior
    {
        get => GetValue(ResizeBehaviorProperty);
        set => SetValue(ResizeBehaviorProperty, value);
    }
    
    /// <summary>
    /// The separation between the major grid lines
    /// </summary>
    public double MajorLineSeparation
    {
        get => GetValue(MajorLineSeparationProperty);
        set => SetValue(MajorLineSeparationProperty, value);
    }

    /// <summary>
    /// The number of minor grid lines in between the major grid lines
    /// </summary>
    public int MinorLineCount
    {
        get => GetValue(MinorLineCountProperty);
        set => SetValue(MinorLineCountProperty, value);
    }

    /// <summary>
    /// The thickness of the major grid lines
    /// </summary>
    public double MajorLineThickness
    {
        get => GetValue(MajorLineThicknessProperty);
        set => SetValue(MajorLineThicknessProperty, value);
    }

    /// <summary>
    /// The brush used to draw the grid lines
    /// </summary>
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
        container.SetValue(Avalonia.SelectAndMove.Selection.IsSelectableProperty, true, BindingPriority.Style);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        Point cursorBefore = e.GetPosition(_transformRoot);
        ViewZoom *= Math.Exp(ZoomSpeed * e.Delta.Y / 5);
        Point positionDelta = e.GetPosition(_transformRoot) - cursorBefore;
        using var _ = new ChangeTransformScope(this);
        ViewTranslateX += positionDelta.X;
        ViewTranslateY +=  positionDelta.Y;
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
            using var _ = new ChangeTransformScope(this);
            Point delta = e.GetPosition(_transformRoot) - _previousPanArgs.GetPosition(_transformRoot);
            ViewTranslateX += delta.X;
            ViewTranslateY += delta.Y;
        }

        _previousPanArgs = e;
    }

    public void ResetView()
    {
        using var _ = new ChangeTransformScope(this);
        ViewZoom = 1.0;
        ViewTranslateX = 0;
        ViewTranslateY = 0;
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
        using var _ = new ChangeTransformScope(this);
        ViewTranslateX = -topLeft.X;
        ViewTranslateY = -topLeft.Y;
        ViewZoom = zoomAmount;
    }
    
    private void BoundsChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (!IsLoaded) return;
        var (oldValue, newValue) = args.GetOldAndNewValue<Rect>();
        using var _ = new ChangeTransformScope(this);
        if (ResizeBehavior.HasFlag(ResizeBehavior.KeepHorizontalCenterline))
        {
            ViewTranslateX += (newValue.Width - oldValue.Width) / 2;
        }

        if (ResizeBehavior.HasFlag(ResizeBehavior.KeepVerticalCenterLine))
        {
            ViewTranslateY += (newValue.Height - oldValue.Height) / 2;
        }

        if (ResizeBehavior.HasFlag(ResizeBehavior.KeepZoom))
        {
            double widthChangeFactor = (newValue.Width - oldValue.Width) / oldValue.Width;
            double heightChangeFactor = (newValue.Height - oldValue.Height) / oldValue.Height;
            ViewZoom += ViewZoom * (widthChangeFactor + heightChangeFactor / 2);
        }
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
    
    private void SelectedElementsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var (oldValue, newValue) = e.GetOldAndNewValue<IAvaloniaReadOnlyList<InputElement>?>();

        oldValue?.CollectionChanged -= SelectedElementsCollectionChanged;
        newValue?.CollectionChanged += SelectedElementsCollectionChanged;
        SelectedElementsCollectionChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private void SelectedElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_selectionChanging) return;
        _selectionChanging = true;
        Selection = Avalonia.SelectAndMove.Selection.GetSelectedElements(this)!
            .Cast<Control>().Select(x => ItemFromContainer(x) ?? throw new ArgumentNullException()).ToList();
        _selectionChanging = false;
    }

    private void SelectionChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_selectionChanging) return;
        _selectionChanging = true;
        Avalonia.SelectAndMove.Selection.ClearSiblingSelection(this);
        foreach (var selected in args.GetNewValue<IReadOnlyList<object>>())
        {
            if (ContainerFromItem(selected) is { } container)
            {
                Avalonia.SelectAndMove.Selection.SetIsSelected(container, true);
            }
        }
        
        _selectionChanging = false;
    }

    private readonly struct ChangeTransformScope : IDisposable
    {
        private readonly SelectAndMove _target;
        
        public ChangeTransformScope(SelectAndMove target)
        {
            _target = target;
            _target._blockRenderRecalculation = true;
        }

        public void Dispose()
        {
            _target._blockRenderRecalculation = false;
            _target.RecalculateRenderTransform();
        }
    }
}
