using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.VisualTree;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public class SelectGesture : GestureRecognizer
{
    public static readonly StyledProperty<KeyModifiers> SelectManyKeyModifiersProperty = 
        AvaloniaProperty.Register<SelectGesture, KeyModifiers>(nameof(SelectManyKeyModifiers), KeyModifiers.Shift);
    
    private int _maxZIndex = 1;
    
    public KeyModifiers SelectManyKeyModifiers
    {
        get => GetValue(SelectManyKeyModifiersProperty);
        set => SetValue(SelectManyKeyModifiersProperty, value);
    }

    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        if (Target is not InputElement target) return;
        
        if (!e.Properties.IsLeftButtonPressed)
        {
            return;
        }
        
        InputElement? clicked = GetSelectableChildAtPointerPress(e);
        if (clicked is not null && Selection.GetIsSelected(clicked))
        {
            return;
        }

        if (e.KeyModifiers != SelectManyKeyModifiers)
        {
            Selection.ClearSiblingSelection(target);
        }

        if (clicked is not null)
        {
            clicked.ZIndex = _maxZIndex++;
            Selection.SetIsSelected(clicked, true);
        }
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
    }

    protected override void PointerReleased(PointerReleasedEventArgs e)
    {
    }

    protected override void PointerCaptureLost(IPointer pointer)
    {
    }
    
    private InputElement? GetSelectableChildAtPointerPress(PointerPressedEventArgs point)
    {
        if (Target is not InputElement target || TopLevel.GetTopLevel(target) is not { } topLevel)
        {
            return null;
        }
        
        InputElement? currentControl = null;
        
        foreach (var child in topLevel.GetVisualsAt(point.GetPosition(topLevel))
                     .Select(visualAtCursor => visualAtCursor
                         .GetSelfAndVisualAncestors()
                         .FirstOrDefault(ancestor => ancestor is InputElement element &&  Selection.GetIsSelectable(element)))
                     .OfType<InputElement>())
        {
            if (HitTest(point, child) && (currentControl is null || currentControl.ZIndex >= child.ZIndex))
            {
                currentControl = child;
            }
        }

        return currentControl;
    }

    private static bool HitTest(PointerPressedEventArgs point, InputElement child)
    {
        if (child is ICustomHitResolver customHitResolver)
        {
            return customHitResolver.ContainsPoint(point.GetPosition(child));
        }
        
        return child.InputHitTest(point.GetPosition(child)) is not null;
    }
}
