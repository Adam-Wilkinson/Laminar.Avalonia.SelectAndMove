using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public class SelectGesture : GestureRecognizerBase
{
    public static readonly StyledProperty<KeyModifiers> SelectManyKeyModifiersProperty = 
        AvaloniaProperty.Register<SelectGesture, KeyModifiers>(nameof(SelectManyKeyModifiers), KeyModifiers.Shift);

    private int _maxZIndex = 1;

    public KeyModifiers SelectManyKeyModifiers
    {
        get => GetValue(SelectManyKeyModifiersProperty);
        set => SetValue(SelectManyKeyModifiersProperty, value);
    }

    public override void PointerPressed(PointerPressedEventArgs e)
    {
        if (Target is not IPanel targetPanel)
        {
            return;
        }

        if (!e.GetCurrentPoint(Target).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Control? clickedControl = GetSelectedChildAtPointerPress(e) as Control;
        if (clickedControl is not null && SelectAndMove.GetIsSelected(clickedControl))
        {
            return;
        }

        if (!(e.KeyModifiers == SelectManyKeyModifiers))
        {
            foreach (IControl selectedControl in targetPanel.Children)
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

    protected override void TrackedPointerMoved(PointerEventArgs e)
    {
    }

    private IControl? GetSelectedChildAtPointerPress(PointerPressedEventArgs point)
    {
        if (Target is not IPanel targetPanel)
        {
            return null;
        }

        IControl? currentControl = null;

        foreach (IControl child in targetPanel.Children)
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

    private static bool HitTest(PointerPressedEventArgs point, IControl child)
    {
        if (child is ICustomHitResolver customHitResolver)
        {
            return customHitResolver.ContainsPoint(point.GetPosition(child));
        }

        return child.InputHitTest(point.GetPosition(child)) is not null;
    }
}
