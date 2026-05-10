using Avalonia.Collections;
using Avalonia.Input;

namespace Laminar.Avalonia.SelectAndMove;

public class SelectionScope
{
    private readonly AvaloniaList<InputElement> _selectable = [];
    private readonly AvaloniaList<InputElement> _selected = [];

    private bool _mutating;
    
    public void ClearSelection()
    {
        _mutating = true;
        foreach (InputElement element in _selectable)
        {
            Selection.SetIsSelected(element, false);
        }
        _selected.Clear();
        _mutating = false;
    }

    public IAvaloniaReadOnlyList<InputElement> GetSelected() => _selected;

    public IAvaloniaReadOnlyList<InputElement> GetSelectable() => _selectable;

    public void RegisterSelectable(InputElement element)
    {
        _selectable.Add(element);
        if (Selection.GetIsSelected(element))
        {
            SetIsSelected(element, true);
        }
    }

    public void UnregisterSelectable(InputElement element)
    {
        _selectable.Remove(element);
        Selection.SetIsSelected(element, false);
    }

    public void SetIsSelected(InputElement element, bool isSelected)
    {
        if (_mutating) return;
        
#if DEBUG
        if (!_selectable.Contains(element)) throw new InvalidOperationException($"Cannot set unselectable element {element} to be selected");
#endif
        
        if (isSelected)
        {
            _selected.Add(element);
        }
        else
        {
            _selected.Remove(element);
        }
    }
}