using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.VisualTree;

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

[PseudoClasses(IsPanningPseudoclass)]
[TemplatePart(SelectionGestureOverlayName, typeof(Canvas))]
[TemplatePart(TransformRootName, typeof(Visual))]
[TemplatePart(BackgroundGestureTargetName, typeof(InputElement))]
public class SelectAndMove : ItemsControl
{
    private const string IsPanningPseudoclass = ":panning";
    private const string SelectionGestureOverlayName = "PART_SelectionGestureOverlay";
    private const string TransformRootName = "PART_TransformRoot";
    private const string BackgroundGestureTargetName = "PART_BackgroundGestureTarget";
    
    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new Canvas());

    public static readonly StyledProperty<SelectionGestureCollection> SelectionGesturesProperty = AvaloniaProperty.Register<SelectAndMove, SelectionGestureCollection>(nameof(SelectionGestures), defaultValue: []); 
    
    public static readonly StyledProperty<MouseButton> PanMouseButtonProperty = AvaloniaProperty.Register<SelectAndMove, MouseButton>(nameof(PanMouseButton), MouseButton.Middle);

    public static readonly AttachedProperty<KeyModifiers> SelectManyKeyModifiersProperty = AvaloniaProperty.RegisterAttached<SelectAndMove, AvaloniaObject, KeyModifiers>(nameof(SelectManyKeyModifiers), KeyModifiers.Shift);
    public static KeyModifiers GetSelectManyKeyModifiers(AvaloniaObject element) => element.GetValue(SelectManyKeyModifiersProperty);
    public static void SetSelectManyKeyModifiers(AvaloniaObject element, KeyModifiers modifiers) => element.SetValue(SelectManyKeyModifiersProperty, modifiers);
    
    public static readonly StyledProperty<IReadOnlyList<object>> SelectionProperty = AvaloniaProperty.Register<SelectAndMove, IReadOnlyList<object>>(nameof(Selection), defaultBindingMode: BindingMode.TwoWay);
    
    public static readonly StyledProperty<double> ZoomSpeedProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ZoomSpeed), 1.0);

    public static readonly StyledProperty<double> ViewZoomProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewZoom), 1.0);

    public static readonly StyledProperty<double> ViewTranslateXProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewTranslateX));
    
    public static readonly StyledProperty<double> ViewTranslateYProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewTranslateY));
    
    public static readonly StyledProperty<ResizeBehavior> ResizeBehaviorProperty = AvaloniaProperty.Register<SelectAndMove, ResizeBehavior>(nameof(ResizeBehavior));
    
    public static readonly StyledProperty<Rect> SnapGridProperty = MoveSelectionGesture.SnapGridProperty.AddOwner<SelectAndMove>();

    public static readonly DirectProperty<SelectAndMove, Visual> TransformRootProperty = AvaloniaProperty.RegisterDirect<SelectAndMove, Visual>(nameof(TransformRoot), sam => sam._transformRoot ?? sam);
    
    private PointerEventArgs? _previousPanArgs;
    private bool _blockRenderRecalculation;
    private bool _selectionChanging;
    private bool _lastClickOnSelectedElement;
    private InputElement? _gestureBackground;
    private Visual? _transformRoot;
    private Canvas? _selectionGestureLayer;
    private List<InputElement> _clickedElements = [];

    static SelectAndMove()
    {
        ViewZoomProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        ViewTranslateXProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        ViewTranslateYProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        SelectionGesturesProperty.Changed.AddClassHandler<SelectAndMove>((sam, args) => sam.OnSelectionGesturesChanged(args));
        BoundsProperty.Changed.AddClassHandler<SelectAndMove>((sam, args) => sam.BoundsChanged(args));
        Avalonia.SelectAndMove.Selection.SelectedElementsProperty.Changed.AddClassHandler<SelectAndMove>((sam, args) => sam.SelectedElementsChanged(args));
        SelectionProperty.Changed.AddClassHandler<SelectAndMove>((sam, args) => sam.SelectionChanged(args));

        TwoPointerMoveGestureRecognizer.TwoPointerMoveEvent.AddClassHandler<SelectAndMove>((sam, args) => sam.OnTwoPointerGesture(args));
        ScrollGestureEvent.AddClassHandler<SelectAndMove>((sam, args) => sam.OnScroll(args));
        ScrollGestureEndedEvent.AddClassHandler<SelectAndMove>((sam, args) => sam.OnScrollEnded(args));
        
        ItemsPanelProperty.OverrideDefaultValue<SelectAndMove>(DefaultPanel);
        BackgroundProperty.OverrideDefaultValue<SelectAndMove>(Brush.Parse("#00000000"));
        Avalonia.SelectAndMove.Selection.IsScopeProperty.OverrideDefaultValue<SelectAndMove>(true);
        
        ResourceInclude selectAndMoveTheme = new((Uri?)null)
        {
            Source = new Uri("avares://Laminar.Avalonia.SelectAndMove/SelectAndMoveTheme.axaml")
        };
        Application.Current?.Resources.MergedDictionaries.Add(selectAndMoveTheme);
    }
    
    public Visual TransformRoot => _transformRoot ?? this;

    /// <summary>
    /// Defines the snap grid as tessellations of the given rectangle, interpreted in transformed coordinates 
    /// </summary>
    public Rect SnapGrid
    {
        get => GetValue(SnapGridProperty);
        set => SetValue(SnapGridProperty, value);
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
    /// A set of key modifiers that, when satisfied, indicates not to clear selection when a new item is selected
    /// </summary>
    public KeyModifiers SelectManyKeyModifiers
    {
        get => GetValue(SelectManyKeyModifiersProperty);
        set => SetValue(SelectManyKeyModifiersProperty, value);
    }

    public SelectionGestureCollection SelectionGestures
    {
        get => GetValue(SelectionGesturesProperty);
        set => SetValue(SelectionGesturesProperty, value);
    }
    
    public Matrix ComputeCurrentTransform() => 
        Matrix.CreateTranslation(ViewTranslateX, ViewTranslateY) * Matrix.CreateScale(ViewZoom, ViewZoom);

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

    public void ActivateSelectionGesture(SelectingGestureRecognizer selectingGesture)
    {
        if (_selectionGestureLayer is null) throw new InvalidOperationException();

        while (_selectionGestureLayer.GestureRecognizers.Count > 0)
        {
            _selectionGestureLayer.GestureRecognizers.Remove(_selectionGestureLayer.GestureRecognizers.First());
        }
        
        _selectionGestureLayer.GestureRecognizers.Add(selectingGesture);
        _selectionGestureLayer.IsHitTestVisible = true;
        selectingGesture.OnGestureFinished += OnGestureFinished;
        selectingGesture.BeginHover();
        return;

        void OnGestureFinished(object? sender, EventArgs e)
        {
            _selectionGestureLayer.IsHitTestVisible = false;
            selectingGesture.OnGestureFinished -= OnGestureFinished;
        }
    }

    internal void UpdateSelectionFromEvent(RoutedEventArgs args)
    {
        if (args is PointerPressedEventArgs pointerPressedEventArgs)
        {
            if (pointerPressedEventArgs.Handled || !pointerPressedEventArgs.Properties.IsLeftButtonPressed) return;
            _lastClickOnSelectedElement = false;
            _clickedElements = GetSelectableChildrenAtPointerPress(pointerPressedEventArgs)
                .Reverse()
                .OrderBy(x => x.ZIndex)
                .ToList();

            if (_clickedElements.Count == 0)
            {
                return;
            }
            
            if (Laminar.Avalonia.SelectAndMove.Selection.GetIsSelected(_clickedElements[^1]))
            {
                _lastClickOnSelectedElement = true;
                return;
            }

            if (pointerPressedEventArgs.KeyModifiers != SelectManyKeyModifiers)
            {
                Laminar.Avalonia.SelectAndMove.Selection.ClearSiblingSelection(this);
            }

            if (_clickedElements.Count > 0)
            {
                ZIndexLayerManger.BringToFront(_clickedElements[^1]);
                Laminar.Avalonia.SelectAndMove.Selection.SetIsSelected(_clickedElements[^1], true);
            }
        }
        else if (args is PointerReleasedEventArgs)
        {
            if (_lastClickOnSelectedElement && _clickedElements.Count > 1)
            {
                Laminar.Avalonia.SelectAndMove.Selection.SetIsSelected(_clickedElements[^1], false);
                Laminar.Avalonia.SelectAndMove.Selection.SetIsSelected(_clickedElements[0], true);
                ZIndexLayerManger.BringToFront(_clickedElements[0]);
            }
            
            args.Handled = true;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _transformRoot = e.NameScope.Find<Visual>(TransformRootName);
        RaisePropertyChanged(TransformRootProperty, this, _transformRoot!);
        
        _selectionGestureLayer = e.NameScope.Find<Canvas>(SelectionGestureOverlayName);

        foreach (var gesture in SelectionGestures)
        {
            SelectionGestureRemoved(gesture);
        }
        
        _gestureBackground = e.NameScope.Find<InputElement>(BackgroundGestureTargetName);
        foreach (var gesture in SelectionGestures)
        {
            SelectionGestureAdded(gesture);
        }
    }

    private void OnScroll(ScrollGestureEventArgs args)
    {
        PseudoClasses.Add(IsPanningPseudoclass);
        using var _ = new ChangeTransformScope(this);
        ViewTranslateX -= args.Delta.X / ViewZoom;
        ViewTranslateY -= args.Delta.Y / ViewZoom;
        args.Handled = true;
    }
    
    private void OnScrollEnded(ScrollGestureEndedEventArgs _)
    {
        PseudoClasses.Remove(IsPanningPseudoclass);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    { 
        base.OnPointerReleased(e);
        if (ItemsPanelRoot is null)
        {
            return;
        }
        
        foreach (var child in ItemsPanelRoot.Children)
        {
            SelectingItemsControl.SetIsSelected(child, false);
        }
        
        PseudoClasses.Remove(IsPanningPseudoclass);
    }

    private void OnTwoPointerGesture(TwoPointerMoveEventArgs args)
    {
        using var _ = new ChangeTransformScope(this);
        ViewTranslateX += args.CenterDelta.X / ViewZoom;
        ViewTranslateY += args.CenterDelta.Y / ViewZoom;
        ViewZoom *= (args.ScaleDelta.X + args.ScaleDelta.Y) / 2;
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
            PseudoClasses.Add(IsPanningPseudoclass);
            using var _ = new ChangeTransformScope(this);
            Point delta = e.GetPosition(_transformRoot) - _previousPanArgs.GetPosition(_transformRoot);
            ViewTranslateX += delta.X;
            ViewTranslateY += delta.Y;
        }

        _previousPanArgs = e;
    }
    
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        return NeedsContainer<SelectAndMoveItem>(item, out recycleKey);
    }
    
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        var newContainer = new SelectAndMoveItem();
        
        if (item is ILogical { LogicalParent: not null } and ISetLogicalParent setLogicalParent)
        {
            setLogicalParent.SetParent(null);
            setLogicalParent.SetParent(newContainer);
        }
        
        return newContainer;
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        container.SetValue(Avalonia.SelectAndMove.Selection.IsSelectableProperty, true, BindingPriority.Style);
        ZIndexLayerManger.BringToFront(container);
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
            .Cast<Control>().Select(ItemFromContainer).OfType<Control>().ToList();
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

    private IEnumerable<InputElement> GetSelectableChildrenAtPointerPress(PointerPressedEventArgs point)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            yield break;
        }
        
        foreach (var child in topLevel.GetVisualsAt(point.GetPosition(topLevel))
                     .Select(visualAtCursor => visualAtCursor
                         .GetSelfAndVisualAncestors()
                         .FirstOrDefault(ancestor => 
                             ancestor is SelectAndMoveItem samItem && Laminar.Avalonia.SelectAndMove.Selection.GetIsSelectable(samItem)))
                     .OfType<InputElement>())
        {
            if (child.InputHitTest(point.GetPosition(child)) is not null)
            {
                yield return child;
            }
        }
    }
    
    private void OnSelectionGesturesChanged(AvaloniaPropertyChangedEventArgs args)
    {
        var (oldValue, newValue) = args.GetOldAndNewValue<SelectionGestureCollection?>();
        if (oldValue is not null)
        {
            oldValue.CollectionChanged -= SelectionGesturesCollectionChanged;
            foreach (var selectionRecognizer in oldValue)
            {
                SelectionGestureRemoved(selectionRecognizer);
            }
        }

        if (newValue is not null)
        {
            newValue.CollectionChanged += SelectionGesturesCollectionChanged;
            foreach (var selectionRecognizer in newValue)
            {
                SelectionGestureAdded(selectionRecognizer);
            }
        }
    }

    private void SelectionGesturesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.NewItems is not null)
        {
            foreach (var item in args.NewItems.Cast<SelectingGestureRecognizer>())
            {
                SelectionGestureAdded(item);
            }
        }

        if (args.OldItems is not null)
        {
            foreach (var item in args.OldItems.Cast<SelectingGestureRecognizer>())
            {
                SelectionGestureRemoved(item);
            }
        }
    }

    private void SelectionGestureAdded(SelectingGestureRecognizer recognizer)
    {
        recognizer.DrawingCanvas = _selectionGestureLayer;
        if (recognizer.Parent is null)
        {
            _gestureBackground?.GestureRecognizers.Add(recognizer);
            return;
        }

        if (_gestureBackground?.GestureRecognizers.Contains(recognizer) == true)
        {
            return;
        }

        if (recognizer.Parent is InputElement parentElement && parentElement.GestureRecognizers.Contains(recognizer))
        {
            parentElement.GestureRecognizers.Remove(recognizer);
            _gestureBackground?.GestureRecognizers.Add(recognizer);
            return;
        }
        
        throw new InvalidOperationException("The gesture recognizer already has a parent. Unable to add it to the gesture background");
    }

    private void SelectionGestureRemoved(SelectingGestureRecognizer recognizer)
    {
        recognizer.DrawingCanvas = null;
        _gestureBackground?.GestureRecognizers.Remove(recognizer);
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
