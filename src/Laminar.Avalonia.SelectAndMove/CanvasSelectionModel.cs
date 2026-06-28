using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Collections;
using Avalonia.Controls;

namespace Laminar.Avalonia.SelectAndMove;

public class CanvasSelectionModel : INotifyPropertyChanged
{
    private readonly ItemsSourceView _itemsView;
    private readonly HashSet<object> _selectedItemSet = [];

    private bool _selectionMutating;
    
    public CanvasSelectionModel(ItemsSourceView itemSource)
    {
        _itemsView = itemSource;
        itemSource.CollectionChanged += ItemSourceOnCollectionChanged;
        ItemSourceOnCollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public event EventHandler<ItemSelectedEventArgs>? ItemSelected;
    
    public event EventHandler<ItemDeselectedEventArgs>? ItemDeselected;

    public event PropertyChangedEventHandler? PropertyChanged;
    
    private void ItemSourceOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Reset:
                foreach (var item in SelectedItems)
                {
                    EnsureDeselected(item);
                }
                break;
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Remove:
                int currentIndex = e.OldStartingIndex;
                foreach (object item in e.OldItems!)
                {
                    EnsureDeselected(item, currentIndex++);
                }
                break;
        }
    }
    
    [AllowNull]
    public IList SelectedItems
    {
        get
        {
            if (field is not null) return field;
            
            field = new AvaloniaList<object>();
            OnSelectedItemsChanged(null, field);
            return field;
        }
        set
        {
            value ??= new AvaloniaList<object>();
            field = value;
            OnSelectedItemsChanged(field, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItems)));
        }
    }

    public void SelectAll()
    {
        foreach (var (index, item) in _itemsView.Index())
        {
            EnsureSelected(item, index);
        }
    }

    public bool DeselectAll()
    {
        if (SelectedItems.Count == 0) return false;
        foreach (var (index, item) in _itemsView.Index())
        {
            EnsureDeselected(item, index);
        }

        return true;
    }

    public void Select(IEnumerable items)
    {
        foreach (var item in items)
        {
            Select(item);
        }
    }
    
    public void Select(object item) => EnsureSelected(item);

    public void Deselect(IEnumerable items)
    {
        foreach (var item in items)
        {
            Deselect(item);
        }
    }
    
    public void Deselect(object item) => EnsureDeselected(item);

    public void SetIsSelected(object item, bool isSelected)
    {
        if (isSelected)
            Select(item); 
        else
            Deselect(item);
    }

    public void SetIsSelected(IEnumerable items, bool isSelected)
    {
        foreach (var item in items)
        {
            SetIsSelected(item, isSelected);
        }
    }
        
    public bool IsSelected(object item) => _selectedItemSet.Contains(item);

    public ICollection AllSelectableItems => _itemsView.Source;
    
    private void OnSelectedItemsChanged(IList? oldValue, IList? newValue)
    {
        if (oldValue is INotifyCollectionChanged oldNotifier)
        {
            oldNotifier.CollectionChanged -= OnSelectedItemsCollectionChanged;
        }
        
        if (newValue is INotifyCollectionChanged notifier)
        {
            notifier.CollectionChanged += OnSelectedItemsCollectionChanged;
        }
        
        OnSelectedItemsCollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private void OnSelectedItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_selectionMutating) return;
        
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                foreach (var item in e.OldItems ?? Array.Empty<object>())
                {
                    _selectedItemSet.Remove(item);
                    ItemDeselected?.Invoke(this,
                        new ItemDeselectedEventArgs { Item = item, IndexInItemsView = _itemsView.IndexOf(item) });
                }
                
                foreach (var item in e.NewItems ?? Array.Empty<object>())
                {
                    _selectedItemSet.Add(item);
                    ItemSelected?.Invoke(this,
                        new ItemSelectedEventArgs { Item = item, IndexInItemsView = _itemsView.IndexOf(item) });
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                var selectedItems = SelectedItems.Cast<object>().ToList();
                foreach (var item in _selectedItemSet)
                {
                    ItemDeselected?.Invoke(this,
                        new ItemDeselectedEventArgs { Item = item, IndexInItemsView = _itemsView.IndexOf(item) });
                }
                _selectedItemSet.Clear();

                foreach (var item in selectedItems)
                {
                    _selectedItemSet.Add(item);
                    ItemSelected?.Invoke(this,
                        new ItemSelectedEventArgs { Item = item, IndexInItemsView = _itemsView.IndexOf(item) });
                }
                
                break;
        }
    }

    private void EnsureDeselected(object? item, int indexInItemView = -1)
    {
        if (item is not null && !IsSelected(item))
            return;

        if (indexInItemView == -1)
        {
            indexInItemView = _itemsView.IndexOf(item);
        }
        
        _selectionMutating = true;
        try
        {
            SelectedItems.Remove(item);
            if (item is not null)
                _selectedItemSet.Remove(item);

            ItemDeselected?.Invoke(this, new ItemDeselectedEventArgs { Item = item, IndexInItemsView = indexInItemView });
        }
        finally
        {
            _selectionMutating = false;
        }
    }
    
    private void EnsureSelected(object? item, int indexInItemView = -1)
    {
        if (item is not null && IsSelected(item))
            return;

        if (indexInItemView == -1)
        {
            indexInItemView = _itemsView.IndexOf(item);
        }
        
        _selectionMutating = true;
        try
        {
            SelectedItems.Add(item);
            if (item is not null)
                _selectedItemSet.Add(item);

            ItemSelected?.Invoke(this, new ItemSelectedEventArgs { Item = item, IndexInItemsView = indexInItemView });
        }
        finally
        {
            _selectionMutating = false;
        }
    }
    
    public class ItemSelectedEventArgs : EventArgs
    {
        public required object? Item { get; init; }
        public required int IndexInItemsView { get; init; }
    }

    public class ItemDeselectedEventArgs : EventArgs
    {
        public required object? Item { get; init; }
        public required int IndexInItemsView { get; init; }
    }

}