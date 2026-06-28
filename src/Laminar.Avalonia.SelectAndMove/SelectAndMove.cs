using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
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
    
    public static readonly StyledProperty<double> ZoomSpeedProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ZoomSpeed), 1.0);

    public static readonly StyledProperty<double> ViewZoomProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewZoom), 1.0);

    public static readonly StyledProperty<double> ViewTranslateXProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewTranslateX));
    
    public static readonly StyledProperty<double> ViewTranslateYProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ViewTranslateY));
    
    public static readonly StyledProperty<double> ArrowKeyMovementDistanceProperty = AvaloniaProperty.Register<SelectAndMove, double>(nameof(ArrowKeyMovementDistance), 50);
    
    public static readonly StyledProperty<ResizeBehavior> ResizeBehaviorProperty = AvaloniaProperty.Register<SelectAndMove, ResizeBehavior>(nameof(ResizeBehavior));

    public static readonly StyledProperty<Rect> SnapGridProperty = AvaloniaProperty.Register<SelectAndMove, Rect>(nameof(SnapGrid));

    public static readonly StyledProperty<SnapMode> SnapModeProperty = AvaloniaProperty.Register<SelectAndMove, SnapMode>(nameof(SnapMode));

    public static readonly DirectProperty<SelectAndMove, Visual> TransformRootProperty = AvaloniaProperty.RegisterDirect<SelectAndMove, Visual>(nameof(TransformRoot), sam => sam._transformRoot ?? sam);

    public static readonly DirectProperty<SelectAndMove, CanvasSelectionModel> SelectionModelProperty = AvaloniaProperty.RegisterDirect<SelectAndMove, CanvasSelectionModel>(nameof(SelectionModel), o => o.SelectionModel, defaultBindingMode: BindingMode.OneWayToSource);
        
    public static readonly RoutedEvent<MoveEventArgs> MoveStartedEvent = RoutedEvent.Register<SelectAndMove, MoveEventArgs>(nameof(MoveStarted), RoutingStrategies.Direct);
    
    public static readonly RoutedEvent<MoveEventArgs> MoveEvent = RoutedEvent.Register<SelectAndMove, MoveEventArgs>(nameof(Move),  RoutingStrategies.Direct);

    public static readonly RoutedEvent<MoveEventArgs> MoveEndedEvent = RoutedEvent.Register<SelectAndMove, MoveEventArgs>(nameof(MoveEnded), RoutingStrategies.Direct);
    
    private PointerEventArgs? _previousPanArgs;
    
    private bool _blockRenderRecalculation;
    private bool _lastClickOnSelectedElement;
    
    private InputElement? _gestureBackground;
    private Visual? _transformRoot;
    private Canvas? _selectionGestureLayer;
    
    private List<SelectAndMoveItem> _lastClickedItems = [];
    private MoveSession? _cursorMoveSession;
    private PointerEventArgs? _cursorMoveSessionInitialArgs;

    static SelectAndMove()
    {
        ViewZoomProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        ViewTranslateXProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        ViewTranslateYProperty.Changed.AddClassHandler<SelectAndMove>((sam, _) => sam.RecalculateRenderTransform());
        SelectionGesturesProperty.Changed.AddClassHandler<SelectAndMove>((sam, args) => sam.OnSelectionGesturesChanged(args));
        SelectAndMoveItem.IsSelectedProperty.Changed.AddClassHandler<SelectAndMoveItem>(OnItemIsSelectedChanged);
        
        TwoPointerMoveGestureRecognizer.TwoPointerMoveEvent.AddClassHandler<SelectAndMove>((sam, args) => sam.OnTwoPointerGesture(args));
        ScrollGestureEvent.AddClassHandler<SelectAndMove>((sam, args) => sam.OnScroll(args));
        ScrollGestureEndedEvent.AddClassHandler<SelectAndMove>((sam, args) => sam.OnScrollEnded(args));
        
        ItemsPanelProperty.OverrideDefaultValue<SelectAndMove>(DefaultPanel);
        BackgroundProperty.OverrideDefaultValue<SelectAndMove>(Brush.Parse("#00000000"));
        FocusableProperty.OverrideDefaultValue<SelectAndMove>(true);
        
        ResourceInclude selectAndMoveTheme = new((Uri?)null)
        {
            Source = new Uri("avares://Laminar.Avalonia.SelectAndMove/SelectAndMoveTheme.axaml")
        };
        Application.Current?.Resources.MergedDictionaries.Add(selectAndMoveTheme);
    }

    private static void OnItemIsSelectedChanged(SelectAndMoveItem container, AvaloniaPropertyChangedEventArgs args)
    {
        if (ItemsControlFromItemContainer(container) is not SelectAndMove parent ||
            parent.ItemFromContainer(container) is not { } item) return;
        parent.SelectionModel.SetIsSelected(item, args.GetNewValue<bool>());
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

    public SnapMode SnapMode
    {
        get => GetValue(SnapModeProperty);
        set => SetValue(SnapModeProperty, value);
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
    /// The pan distance that occurs when the arrow keys are pressed
    /// </summary>
    public double ArrowKeyMovementDistance
    {
        get => GetValue(ArrowKeyMovementDistanceProperty);
        set => SetValue(ArrowKeyMovementDistanceProperty, value);
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

    public CanvasSelectionModel SelectionModel
    {
        get
        {
            if (field is not null) return field;

            field = new CanvasSelectionModel(ItemsView);
            field.ItemSelected += OnItemSelected;
            field.ItemDeselected += OnItemDeselected;
            return field;
        }
    }
    
    public event EventHandler<MoveEventArgs> MoveStarted 
    {
        add => AddHandler(MoveStartedEvent, value);
        remove => RemoveHandler(MoveStartedEvent, value);
    }
    
    public event EventHandler<MoveEventArgs> Move
    {
        add => AddHandler(MoveEvent, value);
        remove => RemoveHandler(MoveEvent, value);
    } 
    
    public event EventHandler<MoveEventArgs> MoveEnded
    {
        add => AddHandler(MoveEndedEvent, value);
        remove => RemoveHandler(MoveEndedEvent, value);
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

    public void MoveSelection(Vector distance)
    {
        using var session = GetMoveSession(0);
        session.OverallMoveDistance = distance;
    }
    
    public MoveSession GetMoveSession(double minimumMoveDistance = -1) => new(this, SnapMode, SnapGrid, minimumMoveDistance);

    public IDisposable StartCursorMove(double minimumMoveDistance)
    {
        _cursorMoveSession = GetMoveSession(minimumMoveDistance);
        return _cursorMoveSession;
    }

    internal void UpdateSelectionFromEvent(RoutedEventArgs args)
    {
        if (args is PointerPressedEventArgs pointerPressedEventArgs)
        {
            if (pointerPressedEventArgs.Handled || !pointerPressedEventArgs.Properties.IsLeftButtonPressed) return;
            _lastClickOnSelectedElement = false;
            _lastClickedItems = GetSelectableChildrenAtPointerPress(pointerPressedEventArgs)
                .Reverse()
                .OrderBy(x => x.ZIndex)
                .ToList();

            if (_lastClickedItems.Count == 0)
            {
                return;
            }
            
            if (_lastClickedItems[^1].IsSelected)
            {
                _lastClickOnSelectedElement = true;
                StartPotentialMoveFromClick(pointerPressedEventArgs);
                return;
            }

            if (pointerPressedEventArgs.KeyModifiers != SelectManyKeyModifiers)
            {
                SelectionModel.DeselectAll();
            }

            if (_lastClickedItems.Count > 0)
            {
                ZIndexLayerManger.BringToFront(_lastClickedItems[^1]);
                _lastClickedItems[^1].IsSelected = true;
                StartPotentialMoveFromClick(pointerPressedEventArgs);
            }
        }
        else if (args is PointerReleasedEventArgs)
        {
            if (_lastClickOnSelectedElement && _lastClickedItems.Count > 1)
            {
                _lastClickedItems[^1].IsSelected = false;
                _lastClickedItems[0].IsSelected = true;
                ZIndexLayerManger.BringToFront(_lastClickedItems[0]);
            }

            if (_cursorMoveSessionInitialArgs is PointerPressedEventArgs)
            {
                _cursorMoveSession?.Dispose();
                _cursorMoveSessionInitialArgs = null;
                _cursorMoveSession = null;
            }

            TryStopPanning();
            
            args.Handled = true;
        }
    }

    private void StartPotentialMoveFromClick(PointerPressedEventArgs args)
    {
        if (!_lastClickedItems[^1].IsMovable) return;
        
        StartCursorMove(6); 
        _cursorMoveSessionInitialArgs = args;
        args.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        bool handled = true;
        switch (e.Key)
        {
            case Key.Up:
                Move(new Vector(0, ArrowKeyMovementDistance / ViewZoom));
                break;
            case Key.Down:
                Move(new Vector(0, -ArrowKeyMovementDistance / ViewZoom));
                break;
            case Key.Left:
                Move(new Vector(ArrowKeyMovementDistance / ViewZoom, 0));
                break;
            case Key.Right:
                Move(new Vector(-ArrowKeyMovementDistance / ViewZoom, 0));
                break;
            case Key.A:
                if (e.KeyModifiers == KeyModifiers.Control)
                {
                    SelectionModel.SelectAll();
                }
                break;
            case Key.OemMinus:
                if (e.KeyModifiers == KeyModifiers.Control)
                {
                    ViewZoom *= 0.8;
                }
                break;
            case Key.OemPlus:
                if (e.KeyModifiers == KeyModifiers.Control)
                {
                    ViewZoom /= 0.8;
                }
                break;
            default:
                handled = false;
                break;
        }
        e.Handled = handled;

        return;

        void Move(Vector movement)
        {
            if (SelectionModel.SelectedItems.Count == 0)
            {
                ViewTranslateX += movement.X;
                ViewTranslateY += movement.Y;
            }
            else
            {
                if (SnapMode != SnapMode.None)
                {
                    movement = new Vector(movement.X == 0 ? 0 : SnapGrid.Width * Math.Sign(movement.X), movement.Y == 0 ? 0 : SnapGrid.Height * Math.Sign(movement.Y));
                }

                MoveSelection(-movement);
            }
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
        if (TryStopPanning())
        {
            e.Handled = true;
            return;
        }

        if (SelectionModel.DeselectAll())
        {
            e.Handled = true;
        }
    }

    private bool TryStopPanning()
    {
        if (_previousPanArgs is null) return false;
        _previousPanArgs = null;
        PseudoClasses.Remove(IsPanningPseudoclass);
        return true;
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
        if (ButtonIsPressed(e.Properties, PanMouseButton))
        {
            _previousPanArgs ??= e;
            PseudoClasses.Add(IsPanningPseudoclass);
            using var _ = new ChangeTransformScope(this);
            Point delta = e.GetPosition(_transformRoot) - _previousPanArgs.GetPosition(_transformRoot);
            ViewTranslateX += delta.X;
            ViewTranslateY += delta.Y;
            e.Handled = true;
            _previousPanArgs = e;
            return;
        }

        _previousPanArgs = null;
        if (_cursorMoveSession is not null)
        {
            _cursorMoveSessionInitialArgs ??= e;
            _cursorMoveSession.OverallMoveDistance = e.GetPosition(TransformRoot) - _cursorMoveSessionInitialArgs.GetPosition(TransformRoot);
            if (_cursorMoveSession.IsActive) e.Handled = true;
            return;
        }
        
        _cursorMoveSessionInitialArgs = null;
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
        ZIndexLayerManger.BringToFront(container);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        if (!IsLoaded) return;
        using var _ = new ChangeTransformScope(this);
        if (ResizeBehavior.HasFlag(ResizeBehavior.KeepHorizontalCenterline) && e.WidthChanged)
        {
            ViewTranslateX += (e.NewSize.Width - e.PreviousSize.Width) / 2;
        }

        if (ResizeBehavior.HasFlag(ResizeBehavior.KeepVerticalCenterLine) && e.HeightChanged)
        {
            ViewTranslateY += (e.NewSize.Height - e.PreviousSize.Height) / 2;
        }

        if (ResizeBehavior.HasFlag(ResizeBehavior.KeepZoom))
        {
            double widthChangeFactor = (e.NewSize.Width - e.PreviousSize.Width) / e.PreviousSize.Width;
            double heightChangeFactor = (e.NewSize.Height - e.PreviousSize.Height) / e.PreviousSize.Height;
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

    private IEnumerable<SelectAndMoveItem> GetSelectableChildrenAtPointerPress(PointerPressedEventArgs point)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            yield break;
        }
        
        foreach (var child in topLevel.GetVisualsAt(point.GetPosition(topLevel))
                     .Select(visualAtCursor => visualAtCursor
                         .GetSelfAndVisualAncestors()
                         .FirstOrDefault(ancestor => 
                             ancestor is SelectAndMoveItem {IsSelectable: true}))
                     .OfType<SelectAndMoveItem>())
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

    private void OnItemDeselected(object? sender, CanvasSelectionModel.ItemDeselectedEventArgs e)
        => ContainerFromIndex(e.IndexInItemsView)?.SetCurrentValue(SelectingItemsControl.IsSelectedProperty, false);

    private void OnItemSelected(object? sender, CanvasSelectionModel.ItemSelectedEventArgs e)
        => ContainerFromIndex(e.IndexInItemsView)?.SetCurrentValue(SelectingItemsControl.IsSelectedProperty, true);
    
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
