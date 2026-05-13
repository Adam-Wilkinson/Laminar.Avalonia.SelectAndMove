using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace Laminar.Avalonia.SelectAndMove;

public class BackgroundGridLines : Control
{
    public static readonly AttachedProperty<double> MajorLineSeparationProperty = AvaloniaProperty.RegisterAttached<BackgroundGridLines, Visual, double>(nameof(MajorLineSeparation), 200);
    public static double GetMajorLineSeparation(Visual visual) => visual.GetValue(MajorLineSeparationProperty);
    public static void SetMajorLineSeparation(Visual visual, double value) => visual.SetValue(MajorLineSeparationProperty, value);
    
    public static readonly AttachedProperty<int> MinorLineCountProperty = AvaloniaProperty.RegisterAttached<BackgroundGridLines, Visual, int>(nameof(MinorLineCount), 4);
    public static int GetMinorLineCount(Visual visual) => visual.GetValue(MinorLineCountProperty);
    public static void SetMinorLineCount(Visual visual, int value) => visual.SetValue(MinorLineCountProperty, value);

    public static readonly AttachedProperty<double> MajorLineThicknessProperty = AvaloniaProperty.RegisterAttached<BackgroundGridLines, Visual, double>(nameof(MajorLineThickness), 5);
    public static double GetMajorLineThickness(Visual visual) => visual.GetValue(MajorLineThicknessProperty);
    public static void SetMajorLineThickness(Visual visual, double value) => visual.SetValue(MajorLineThicknessProperty, value);
    
    public static readonly AttachedProperty<IBrush> LineBrushProperty = AvaloniaProperty.RegisterAttached<BackgroundGridLines, Visual, IBrush>(nameof(LineBrush), new SolidColorBrush(new Color(255, 5, 5, 5)));
    public static IBrush GetLineBrush(Visual visual) => visual.GetValue(LineBrushProperty);
    public static void SetLineBrush(Visual visual, IBrush value) => visual.SetValue(LineBrushProperty, value);

    public static readonly DirectProperty<BackgroundGridLines, Rect> SnapGridProperty = AvaloniaProperty.RegisterDirect<BackgroundGridLines, Rect>(nameof(SnapGrid), o => o.SnapGrid);

    public static readonly StyledProperty<Visual?> DrawWithinAncestorProperty = AvaloniaProperty.Register<BackgroundGridLines, Visual?>(nameof(DrawWithinAncestor));
    
    public Rect SnapGrid
    {
        get;
        private set => SetAndRaise(SnapGridProperty, ref field, value);
    }

    public Visual? DrawWithinAncestor
    {
        get => GetValue(DrawWithinAncestorProperty);
        set => SetValue(DrawWithinAncestorProperty, value);
    }

    private readonly Pen _majorLinePen = new();
    private readonly Pen _minorLinePen = new();
    private readonly List<IDisposable> _renderTransformListeners = [];

    static BackgroundGridLines()
    {
        AffectsRender<BackgroundGridLines>(MajorLineSeparationProperty, MinorLineCountProperty, MajorLineThicknessProperty, LineBrushProperty, BoundsProperty, RenderTransformProperty);
        ZIndexProperty.OverrideDefaultValue<BackgroundGridLines>(-1000);
        FocusableProperty.OverrideDefaultValue<BackgroundGridLines>(false);
        IsEnabledProperty.OverrideDefaultValue<BackgroundGridLines>(false);
        ClipToBoundsProperty.OverrideDefaultValue<BackgroundGridLines>(false);
    }

    public double MajorLineSeparation
    {
        get => GetValue(MajorLineSeparationProperty);
        set => SetValue(MajorLineSeparationProperty, value);
    }

    public int MinorLineCount
    {
        get => GetValue(MinorLineCountProperty);
        set => SetValue(MinorLineCountProperty, value);
    }

    public double MajorLineThickness
    {
        get => GetValue(MajorLineThicknessProperty);
        set => SetValue(MajorLineThicknessProperty, value);
    }

    public IBrush LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (!Equals(_majorLinePen.Brush, LineBrush))
        {
            _majorLinePen.Brush = LineBrush;
        }

        if (Math.Abs(_majorLinePen.Thickness - MajorLineThickness) > double.Epsilon)
        {
            _majorLinePen.Thickness = MajorLineThickness;
        }

        if (!Equals(_minorLinePen.Brush, LineBrush))
        {
            _minorLinePen.Brush = LineBrush;
        }

        Rect drawingBounds = GetRectInLocal(FindRenderTransformAncestor().Bounds);
        Vector renderScale = drawingBounds.Size / FindRenderTransformAncestor().Bounds.Size;

        // Multiplying by xRenderScale ensures the line thickness remains constant from the perspective of the parent
        _majorLinePen.Thickness = MajorLineThickness * renderScale.X;
        _minorLinePen.Thickness = _majorLinePen.Thickness * GetMinorLineThicknessScale(renderScale.X);

        foreach ((double xCoord, bool isMajor) in GetLines(drawingBounds.Left, drawingBounds.Right, renderScale.X))
        {
            context.DrawLine(isMajor ? _majorLinePen : _minorLinePen, new Point(xCoord, drawingBounds.Top), new Point(xCoord, drawingBounds.Bottom));
        }

        _majorLinePen.Thickness = MajorLineThickness * renderScale.Y;
        _minorLinePen.Thickness = _majorLinePen.Thickness * GetMinorLineThicknessScale(renderScale.Y);

        foreach ((double yCoord, bool isMajor) in GetLines(drawingBounds.Top, drawingBounds.Bottom, renderScale.Y))
        {
            context.DrawLine(isMajor ? _majorLinePen : _minorLinePen, new Point(drawingBounds.Left, yCoord), new Point(drawingBounds.Right, yCoord));
        }

        SnapGrid = GetSnapGrid(renderScale.X, renderScale.Y);
        base.Render(context);
    }

    public double GetMinorLineThicknessScale(double scale) => (Math.Pow(MinorLineCount, TrueModulus(-Math.Log(scale, MinorLineCount) - 0.5, 1)) - 1) / (MinorLineCount - 1);

    public double GetSpacing(double scale)
    {
        int subdivisionLevel = (int)Math.Round(Math.Log(scale, MinorLineCount));
        return MajorLineSeparation * Math.Pow(MinorLineCount, subdivisionLevel - 1);
    }
    
    // We need to keep track of the render transform to our logical parent
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        foreach (var transformListener in _renderTransformListeners)
        {
            transformListener.Dispose();
        }
        _renderTransformListeners.Clear();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        foreach (var visualParent in this.GetSelfAndVisualAncestors())
        {
            _renderTransformListeners.Add(visualParent.GetObservable(RenderTransformProperty).Subscribe(new AnonymousObserver<ITransform?>(_ =>
            {
                InvalidateVisual();
            })));

            if (Equals(FindRenderTransformAncestor(), visualParent)) break;
        }
        
        InvalidateVisual();
    }
    
    private Rect GetSnapGrid(double xScale, double yScale) => new(Bounds.TopLeft, new Size(GetSpacing(xScale), GetSpacing(yScale)));
    
    private Rect GetRectInLocal(Rect rect)
    {
        var ancestor = FindRenderTransformAncestor();
        Matrix transformToParent = this.TransformToVisual(ancestor)?.Invert() ?? throw new InvalidOperationException($"RectangleGridLines does not have a RenderTransform with respect to its given ancestor {ancestor}");
        Point topLeftInParent = new Point(-Bounds.Left, -Bounds.Top) * transformToParent;
        Point bottomRightInParent = new Point(rect.Width - Bounds.Left, rect.Height - Bounds.Top) * transformToParent;
        return new Rect(topLeftInParent, bottomRightInParent);
    }

    private IEnumerable<(double localCoordinate, bool isMajor)> GetLines(double start, double end, double scale)
    {
        double trueSpacing = GetSpacing(scale);
        double position = start - (start % trueSpacing);
        while (position < end)
        {
            yield return (position, ((int)Math.Round(position / trueSpacing)) % MinorLineCount == 0);
            position += trueSpacing;
        }
    }
    
    private static double TrueModulus(double x, double y) => (x % y + y) % y;

    private Visual FindRenderTransformAncestor()
    {
        if (DrawWithinAncestor is not null) return DrawWithinAncestor;
        if (TemplatedParent is Visual templatedVisual) return templatedVisual;
        if (this.GetLogicalParent() is Visual logicalParentVisual) return logicalParentVisual;
        throw new InvalidOperationException("Could not find render transform ancestor");
    }
}
