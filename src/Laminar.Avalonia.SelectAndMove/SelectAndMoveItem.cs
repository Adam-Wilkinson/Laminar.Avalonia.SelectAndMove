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
        this[!Selection.IsSelectedProperty] = this[!IsSelectedProperty];
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _childChangedSubscription?.Dispose();
        _childChangedSubscription = Presenter?.GetObservable(ContentPresenter.ChildProperty).Subscribe(
            new AnonymousObserver<Control?>(child =>
            {
                if (child is null) return;
                this[!LeftProperty] = child[(!Canvas.LeftProperty).WithMode(BindingMode.TwoWay)];
                this[!TopProperty] = child[(!Canvas.TopProperty).WithMode(BindingMode.TwoWay)];
                this[!ZIndexLayerManger.ZIndexLayerProperty] = child[(!ZIndexLayerManger.ZIndexLayerProperty).WithMode(BindingMode.TwoWay)];
                this[!ZIndexProperty] = child[(!ZIndexProperty).WithMode(BindingMode.TwoWay)];
            }));
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