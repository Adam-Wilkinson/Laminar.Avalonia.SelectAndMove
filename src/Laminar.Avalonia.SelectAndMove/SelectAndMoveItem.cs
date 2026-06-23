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

[PseudoClasses(":selected")]
public class SelectAndMoveItem : ContentControl, ISelectable
{
    public static readonly StyledProperty<bool> IsSelectedProperty = SelectingItemsControl.IsSelectedProperty.AddOwner<ListBoxItem>();

    public static readonly StyledProperty<double> LeftProperty = Canvas.LeftProperty.AddOwner<SelectAndMoveItem>();

    public static readonly StyledProperty<double> TopProperty = Canvas.TopProperty.AddOwner<SelectAndMoveItem>();
    
    private SelectAndMove? _selectAndMoveOwner;
    private IDisposable? _childChangedSubscription;
    
    static SelectAndMoveItem()
    {
        SelectableMixin.Attach<SelectAndMoveItem>(IsSelectedProperty);
    }

    public SelectAndMoveItem()
    {
        this[!Selection.IsSelectedProperty] = this[(!IsSelectedProperty).WithMode(BindingMode.TwoWay)];
    }
    
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
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

                if (!child.IsSet(Selection.IsSelectedProperty))
                {
                    child.SetValue(Selection.IsSelectableProperty, true, BindingPriority.Style);
                }

                if (!child.IsSet(MoveSelectionGesture.IsMovableProperty))
                {
                    child.SetValue(MoveSelectionGesture.IsMovableProperty, true, BindingPriority.Style);
                }
                
                this[!LeftProperty] = child[(!Canvas.LeftProperty).WithMode(BindingMode.TwoWay)];
                this[!TopProperty] = child[(!Canvas.TopProperty).WithMode(BindingMode.TwoWay)];
                this[!ZIndexLayerManger.ZIndexLayerProperty] = child[(!ZIndexLayerManger.ZIndexLayerProperty).WithMode(BindingMode.TwoWay)];
                this[!Selection.IsSelectableProperty] = child[(!Selection.IsSelectableProperty).WithMode(BindingMode.TwoWay)];
                this[!MoveSelectionGesture.IsMovableProperty] = child[(!MoveSelectionGesture.IsMovableProperty).WithMode(BindingMode.TwoWay)];
                child[!SelectingItemsControl.IsSelectedProperty] = this[(!SelectingItemsControl.IsSelectedProperty).WithMode(BindingMode.OneWay)];
                child[!Selection.IsSelectedProperty] = this[(!Selection.IsSelectedProperty).WithMode(BindingMode.OneWay)];
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
}