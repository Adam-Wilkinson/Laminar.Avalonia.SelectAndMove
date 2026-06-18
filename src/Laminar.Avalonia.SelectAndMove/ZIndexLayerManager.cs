using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Laminar.Avalonia.SelectAndMove;

public class ZIndexLayerManger
{
    public static readonly AttachedProperty<int?> ZIndexLayerProperty = AvaloniaProperty.RegisterAttached<ZIndexLayerManger, Visual, int?>("ZIndexLayer");
    public static int? GetZIndexLayer(Visual target) => target.GetValue(ZIndexLayerProperty);
    public static void SetZIndexLayer(Visual target, int? value) => target.SetValue(ZIndexLayerProperty, value);
    

    public static readonly AttachedProperty<int> ZIndexLayerIncrementProperty = AvaloniaProperty.RegisterAttached<ZIndexLayerManger, Visual, int>("ZIndexLayerIncrement", defaultValue: 100);
    public static int GetZIndexLayerIncrement(Visual target) => target.GetValue(ZIndexLayerIncrementProperty);
    public static void SetZIndexLayerIncrement(Visual target, int value) => target.SetValue(ZIndexLayerIncrementProperty, value);

    private static readonly ConditionalWeakTable<Visual, ZIndexLayerManger> AllManagers = [];
    private static readonly ConditionalWeakTable<Visual, VisualTreeMonitor> VisualTreeMonitors = [];
    
    private readonly SortedList<int, ZIndexLayer> _layers = [];
    private int _maxZIndex = 0;
    
    static ZIndexLayerManger()
    {
        ZIndexLayerProperty.Changed.AddClassHandler<Visual>(ZIndexLayerChanged);
    }

    private static void ZIndexLayerChanged(Visual visual, AvaloniaPropertyChangedEventArgs args)
    {
        var visualParent = visual.GetVisualParent();

        if (args.OldValue is int oldValue)
        {
            if (visualParent is not null)
            {
                GetLayerManager(visualParent).RemoveElementFromLayer(visual, oldValue);
            }
            
            if (args.NewValue is null && VisualTreeMonitors.TryGetValue(visual, out var monitor))
            {
                monitor.Dispose();
            }
        }

        if (args.NewValue is int newValue)
        {
            if (visualParent is not null)
            {
                GetLayerManager(visualParent).AddElementToLayer(visual, newValue);
            }
            
            VisualTreeMonitors.GetValue(visual, v => new VisualTreeMonitor(v));
        }
    }
    
    private static ZIndexLayerManger GetLayerManager(Visual visualParent) => AllManagers.GetValue(visualParent, _ => new ZIndexLayerManger());

    public static void BringToFront(Visual visual)
    {
        if (visual.GetVisualParent() is not { } visualParent) return;
        ZIndexLayerManger layerManager = GetLayerManager(visualParent);
        if (GetZIndexLayer(visual) is not { } zIndexLayer)
        {
            visual.ZIndex = ++layerManager._maxZIndex;
            return;
        }
        
        int layerIncrement = GetZIndexLayerIncrement(visualParent);
        layerManager.GetLayer(zIndexLayer, layerIncrement).BringToFrontOfLayer(visual);
    }

    private void RemoveElementFromLayer(Visual visual, int layer)
    {
        _layers[layer].RemoveVisual(visual);
    }

    private void AddElementToLayer(Visual visual, int layer)
    {
        if (visual.GetVisualParent() is not { } visualParent) return;
        int layerIncrement = GetZIndexLayerIncrement(visualParent);
        GetLayer(layer, layerIncrement).AddVisual(visual);
    }

    private ZIndexLayer GetLayer(int layerKey, int increment)
    {
        if (_layers.TryGetValue(layerKey, out var layer))
        {
            return layer;
        }

        _layers[layerKey] = new ZIndexLayer(0, 0);
        RebuildLayerRanges(increment);
        return _layers[layerKey];
    }
    
    private void RebuildLayerRanges(int increment)
    {
        int currentBottom = 0;

        foreach (var layer in _layers.Values)
        {
            layer.SetRange(currentBottom, currentBottom + increment);

            currentBottom += increment + 1;
        }

        _maxZIndex = Math.Max(currentBottom, _maxZIndex);
    }
    
    private class ZIndexLayer(int bottomOfRange, int topOfRange)
    {
        private readonly List<Visual> _children = [];
        private int _currentMaxValue = bottomOfRange;
        private bool _normalizeQueued; 
        
        public int BottomOfRange { get; private set; } = bottomOfRange;

        public int TopOfRange { get; private set; } = topOfRange;


        public void AddVisual(Visual visual)
        {
            if (!_children.Contains(visual))
            {
                _children.Add(visual);
            }
            visual.ZIndex = ++_currentMaxValue;
            QueuePotentialNormalize();
        }

        public void RemoveVisual(Visual visual)
        {
            _children.Remove(visual);
        }
        
        public void SetRange(int bottom, int top)
        {
            int delta = bottom - BottomOfRange;

            BottomOfRange = bottom;
            TopOfRange = top;

            foreach (var child in _children)
            {
                child.ZIndex += delta;
            }

            _currentMaxValue += delta;
            QueuePotentialNormalize();
        }

        public void BringToFrontOfLayer(Visual visual)
        {
            if (visual.ZIndex == _currentMaxValue) return;
            visual.ZIndex = ++_currentMaxValue;
            QueuePotentialNormalize();
        }

        private void QueuePotentialNormalize()
        {
            if (_currentMaxValue >= TopOfRange)
            {
                Normalize();
                return;
            }

            if (_currentMaxValue >= TopOfRange * 0.8 && !_normalizeQueued)
            {
                _normalizeQueued = true;
                Dispatcher.UIThread.Post(Normalize, DispatcherPriority.ApplicationIdle);
            }
        }
        
        private void Normalize()
        {
            _normalizeQueued = false;
            int offset = 0;
            _children.Sort((x, y) => x.ZIndex.CompareTo(y.ZIndex));
            foreach (var child in _children)
            {
                child.ZIndex = BottomOfRange + offset++;
            }

            _currentMaxValue = BottomOfRange + offset - 1;
            if (_currentMaxValue >= TopOfRange)
            {
                throw new InvalidOperationException("Z-index layer has exceeded capacity");
            }
        }
    }
    
    internal class VisualTreeMonitor : IDisposable
    {
        private readonly Visual _visual;
        
        public VisualTreeMonitor(Visual visual)
        {
            _visual = visual;
            visual.AttachedToVisualTree += AttachedToVisualTree;
            visual.DetachedFromVisualTree += DetachedFromVisualTree;
        }

        private void AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (e.AttachmentPoint is not { } visualParent || GetZIndexLayer(_visual) is not { } layer) return;
            GetLayerManager(visualParent).AddElementToLayer(_visual, layer);
        }

        private void DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (e.AttachmentPoint is not { } visualParent || GetZIndexLayer(_visual) is not { } layer) return;
            GetLayerManager(visualParent).RemoveElementFromLayer(_visual, layer);
        }

        public void Dispose()
        {
            _visual.AttachedToVisualTree -= AttachedToVisualTree;
            _visual.DetachedFromVisualTree -= DetachedFromVisualTree;
            VisualTreeMonitors.Remove(_visual);
        }
    }
}