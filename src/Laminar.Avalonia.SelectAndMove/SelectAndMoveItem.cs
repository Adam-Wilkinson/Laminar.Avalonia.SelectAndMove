using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Reactive;

namespace Laminar.Avalonia.SelectAndMove;

[PseudoClasses(":selected", ":dragging")]
public class SelectAndMoveItem : ContentControl, ISelectable
{
    public static readonly StyledProperty<bool> IsSelectedProperty = SelectingItemsControl.IsSelectedProperty.AddOwner<ListBoxItem>();

    public static readonly StyledProperty<bool> IsSelectableProperty = AvaloniaProperty.Register<SelectAndMoveItem, bool>(nameof(IsSelectable), true);

    public static readonly StyledProperty<bool> IsMovableProperty = AvaloniaProperty.Register<SelectAndMoveItem, bool>(nameof(IsMovable), true);
    
    public static readonly StyledProperty<double> LeftProperty = Canvas.LeftProperty.AddOwner<SelectAndMoveItem>();
    
    public static readonly StyledProperty<double> TopProperty = Canvas.TopProperty.AddOwner<SelectAndMoveItem>();
    
    private SelectAndMove? _selectAndMoveOwner;
    private IDisposable? _childChangedSubscription;
    
    static SelectAndMoveItem()
    {
        SelectableMixin.Attach<SelectAndMoveItem>(IsSelectedProperty);
        ClipToBoundsProperty.OverrideDefaultValue<SelectAndMoveItem>(false);
    }
    
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsSelectable
    {
        get => GetValue(IsSelectableProperty);
        set => SetValue(IsSelectableProperty, value);
    }

    public bool IsMovable
    {
        get => GetValue(IsMovableProperty);
        set => SetValue(IsMovableProperty, value);
    }

    public double Left
    {
        get => GetValue(LeftProperty);
        set => SetValue(LeftProperty, value);
    }

    public double Top
    {
        get => GetValue(TopProperty);
        set => SetValue(TopProperty, value);
    }

    internal void SetIsDragging(bool isDragging)
    {
        PseudoClasses.Set(":dragging", isDragging);
    }
    
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _selectAndMoveOwner = this.GetLogicalAncestors().OfType<SelectAndMove>().FirstOrDefault();
        base.OnAttachedToLogicalTree(e);
    }

    protected override bool RegisterContentPresenter(ContentPresenter presenter)
    {
        _childChangedSubscription?.Dispose();
        if (!base.RegisterContentPresenter(presenter))
        {
            return false;
        }
        
        _childChangedSubscription = Presenter?.GetObservable(ContentPresenter.ChildProperty).Subscribe(
            new AnonymousObserver<Control?>(child =>
            {
                if (child is null) return;

                BindIfSet(child, Canvas.LeftProperty, propertyOnSamItem: LeftProperty);
                BindIfSet(child, Canvas.TopProperty, propertyOnSamItem: TopProperty);
                BindIfSet(child, ZIndexLayerManger.ZIndexLayerProperty);
            }));
        return true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _selectAndMoveOwner?.UpdateSelectionFromEvent(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _selectAndMoveOwner?.UpdateSelectionFromEvent(e);
    }

    private void BindIfSet(Control target, AvaloniaProperty propertyOnTarget, AvaloniaProperty? propertyOnSamItem = null, BindingMode mode = BindingMode.TwoWay)
    {
        propertyOnSamItem ??= propertyOnTarget;
        if (target.IsSet(propertyOnTarget))
        {
            this[!propertyOnSamItem] = target[(!propertyOnTarget).WithMode(mode).WithPriority(BindingPriority.Style)];
        }
    }
}