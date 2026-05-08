using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.LogicalTree;

namespace Laminar.Avalonia.SelectAndMove;

public class Selection
{
    public static readonly AttachedProperty<bool> IsSelectedProperty = AvaloniaProperty.RegisterAttached<Selection, InputElement, bool>("IsSelected", defaultValue: false, inherits: true);
    public static bool GetIsSelected(InputElement element) => element.GetValue(IsSelectedProperty);
    public static void SetIsSelected(InputElement element, bool value) => element.SetValue(IsSelectedProperty, value);
    
    public static readonly AttachedProperty<bool> IsSelectableProperty = AvaloniaProperty.RegisterAttached<Selection, InputElement, bool>("IsSelectable", defaultValue: false);
    public static bool GetIsSelectable(InputElement element) => element.GetValue(IsSelectableProperty);
    public static void SetIsSelectable(InputElement element, bool value) => element.SetValue(IsSelectableProperty, value);
    
    public static readonly AttachedProperty<bool> IsScopeProperty = AvaloniaProperty.RegisterAttached<Selection, InputElement, bool>("IsScope", defaultValue: false);
    public static bool GetIsScope(InputElement target) => target.GetValue(IsScopeProperty);
    public static void SetIsScope(InputElement target, bool value) => target.SetValue(IsScopeProperty, value);
    
    public static readonly AttachedProperty<IAvaloniaReadOnlyList<InputElement>?> SelectedElementsProperty = AvaloniaProperty.RegisterAttached<Selection, InputElement, IAvaloniaReadOnlyList<InputElement>?>("SelectedElements");
    public static IAvaloniaReadOnlyList<InputElement>? GetSelectedElements(InputElement target) => target.GetValue(SelectedElementsProperty);
    public static void SetSelectedElements(InputElement _, IAvaloniaReadOnlyList<InputElement>? __) => throw new InvalidOperationException("This value is read-only");
    
    private static readonly ConditionalWeakTable<InputElement, SelectionScope> Scopes = [];
    
    static Selection()
    {
        IsScopeProperty.Changed.AddClassHandler<InputElement>(IsScopeChanged);
        IsSelectedProperty.Changed.AddClassHandler<InputElement>(IsSelectedChanged);
        IsSelectableProperty.Changed.AddClassHandler<InputElement>(IsSelectableChanged);
        
        IsScopeProperty.OverrideDefaultValue<TopLevel>(true);
    }

    public static IAvaloniaReadOnlyList<InputElement>? GetSelectedSiblings(InputElement target) => FindScope(target)?.GetSelected();

    public static IAvaloniaReadOnlyList<InputElement>? GetSiblings(InputElement target) => FindScope(target)?.GetSelectable();
    
    public static void ClearSiblingSelection(InputElement target) => FindScope(target)?.ClearSelection();
    
    private static void IsSelectableChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (!args.GetNewValue<bool>()) SetIsSelected(element, false);
        
        if (args.GetNewValue<bool>())
        {
            element.AttachedToVisualTree += SelectableElementAttachedToVisualTree;
            element.DetachedFromVisualTree += SelectableElementDetachedToVisualTree;
            FindScope(element)?.RegisterSelectable(element);
        }
        else
        {
            element.AttachedToVisualTree -= SelectableElementAttachedToVisualTree;
            element.DetachedFromVisualTree -= SelectableElementDetachedToVisualTree;
            FindScope(element)?.UnregisterSelectable(element);
        }
    }

    private static void SelectableElementAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (e.AttachmentPoint is InputElement parent)
        {
            FindScope(parent)?.RegisterSelectable(parent);
        }
    }

    private static void SelectableElementDetachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (e.AttachmentPoint is InputElement parent)
        {
            FindScope(parent)?.UnregisterSelectable(parent);
        }
    }

    private static void IsSelectedChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Priority is BindingPriority.Inherited) return;
        FindScope(element)?.SetIsSelected(element, args.GetNewValue<bool>());
    }

    private static void IsScopeChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.GetNewValue<bool>())
        {
            GetOrCreateScope(element);
        }
        else
        {
            Scopes.Remove(element);
            element.SetValue(SelectedElementsProperty, null);
        }
    }

    private static SelectionScope? FindScope(InputElement element)
        => element.GetSelfAndLogicalAncestors().Cast<InputElement>().FirstOrDefault(x =>
        {
            if (GetIsScope(x))
            {
                return true;
            }

            if (x is TopLevel topLevelScope)
            {
                SetIsScope(topLevelScope, true);
                return true;
            }

            return false;
        }) is { } scopeOwner ? GetOrCreateScope(scopeOwner) : null;

    private static SelectionScope GetOrCreateScope(InputElement element) => !GetIsScope(element) ? throw new InvalidOperationException() 
        : Scopes.GetValue(element, e =>
    {
        SelectionScope newScope = new();
        element.SetValue(SelectedElementsProperty, newScope.GetSelected());
        return newScope;
    });
}