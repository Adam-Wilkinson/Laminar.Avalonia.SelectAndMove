using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;

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
        if (Target is not Panel targetPanel || !e.GetCurrentPoint(targetPanel).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Control? clickedControl = GetSelectedChildAtPointerPress(e);
        if (clickedControl is not null && SelectAndMove.GetIsSelected(clickedControl))
        {
            return;
        }

        if (!(e.KeyModifiers == SelectManyKeyModifiers))
        {
            foreach (Control selectedControl in targetPanel.Children)
            {
                SelectAndMove.SetIsSelected(selectedControl, false);
            }
        }

        if (clickedControl is not null)
        {
            clickedControl.ZIndex = _maxZIndex++;
            SelectAndMove.SetIsSelected(clickedControl, true);
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

    private Control? GetSelectedChildAtPointerPress(PointerPressedEventArgs point)
    {
        if (Target is not Panel targetPanel)
        {
            return null;
        }

        Control? currentControl = null;

        foreach (Control child in targetPanel.Children)
        {
            if (SelectAndMove.GetIsSelectable(child) 
                && HitTest(point, child) 
                && (currentControl is null || currentControl.ZIndex <= child.ZIndex))
            {
                currentControl = child;
            }
        }

        return currentControl;
    }

    private static bool HitTest(PointerPressedEventArgs point, Control child)
    {
        if (child is ICustomHitResolver customHitResolver)
        {
            return customHitResolver.ContainsPoint(point.GetPosition(child));
        }

        return child.InputHitTest(point.GetPosition(child)) is not null;
    }
}
