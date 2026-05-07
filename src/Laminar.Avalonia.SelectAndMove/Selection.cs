using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
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
    
    private static readonly ConditionalWeakTable<InputElement, AvaloniaList<InputElement>> SelectedElementsTable = [];
    private static bool _isChangingSelection;
    
    static Selection()
    {
        IsScopeProperty.Changed.AddClassHandler<InputElement>(IsScopeChanged);
        IsSelectedProperty.Changed.AddClassHandler<InputElement>(IsSelectedChanged);
        IsSelectableProperty.Changed.AddClassHandler<InputElement>(IsSelectableChanged);
        
        IsScopeProperty.OverrideDefaultValue<TopLevel>(true);
    }
    
    public static void ClearSiblings(InputElement target)
    {
        _isChangingSelection = true;
        foreach (var child in FindScope(target))
        {
            SetIsSelected(child, false);
        }
        _isChangingSelection = false;
    }
    
    private static void IsSelectableChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (!args.GetNewValue<bool>()) SetIsSelected(element, false);
    }

    private static void IsSelectedChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (_isChangingSelection) return;
        var scopeSelection = FindScope(element);
        if (args.GetNewValue<bool>())
        {
            scopeSelection.Add(element);
        }
        else
        {
            scopeSelection.Remove(element);
        }
    }

    private static void IsScopeChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.GetNewValue<bool>())
        {
            GetScope(element);
        }
        else
        {
            SelectedElementsTable.Remove(element);
            element.SetValue(SelectedElementsProperty, null);
        }
    }

    private static AvaloniaList<InputElement> FindScope(InputElement element)
        => element.GetSelfAndLogicalAncestors().Cast<InputElement>().FirstOrDefault(x => GetIsScope(x) || x is TopLevel) is { } scope
            ? GetScope(scope)
            : throw new InvalidOperationException();

    private static AvaloniaList<InputElement> GetScope(InputElement element) => SelectedElementsTable.GetValue(element,
        e =>
        {
            var newValue = new AvaloniaList<InputElement>();
            e.SetValue(SelectedElementsProperty, newValue);
            return newValue;
        });
}