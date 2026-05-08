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

    public static IAvaloniaReadOnlyList<InputElement> GetSelectedSiblings(InputElement target) => FindScope(target).GetSelected();

    public static IAvaloniaReadOnlyList<InputElement> GetSiblings(InputElement target) => FindScope(target).GetSelectable();
    
    public static void ClearSiblingSelection(InputElement target) => FindScope(target).ClearSelection();
    
    private static void IsSelectableChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (!args.GetNewValue<bool>()) SetIsSelected(element, false);
        var scopeSelection = FindScope(element);
        if (args.GetNewValue<bool>())
        {
            scopeSelection.RegisterSelectable(element);
        }
        else
        {
            scopeSelection.UnregisterSelectable(element);
        }
    }

    private static void IsSelectedChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Priority is BindingPriority.Inherited) return;
        FindScope(element).SetIsSelected(element, args.GetNewValue<bool>());
    }

    private static void IsScopeChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.GetNewValue<bool>())
        {
            GetScope(element);
        }
        else
        {
            Scopes.Remove(element);
            element.SetValue(SelectedElementsProperty, null);
        }
    }

    private static SelectionScope FindScope(InputElement element)
        => element.GetSelfAndLogicalAncestors().Cast<InputElement>().FirstOrDefault(x => GetIsScope(x) || x is TopLevel) is { } scope
            ? GetScope(scope)
            : throw new InvalidOperationException();

    private static SelectionScope GetScope(InputElement element) => Scopes.GetValue(element,
        e =>
        {
            var newValue = new SelectionScope();
            e.SetValue(SelectedElementsProperty, newValue.GetSelected());
            return newValue;
        });
}