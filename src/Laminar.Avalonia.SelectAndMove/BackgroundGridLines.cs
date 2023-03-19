using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove;

public class BackgroundGridLines : Control
{
    public static readonly AttachedProperty<double> MajorLineSeparationProperty = AvaloniaProperty.RegisterAttached<BackgroundGridLines, double>(nameof(MajorLineSeparation), typeof(BackgroundGridLines), 200);

    public static readonly AttachedProperty<int> MinorLineCountProperty = AvaloniaProperty.RegisterAttached<BackgroundGridLines, int>(nameof(MinorLineCount), typeof(BackgroundGridLines), 4);

    public static readonly AttachedProperty<double> MajorLineThicknessProperty = AvaloniaProperty.RegisterAttached<BackgroundGridLines, double>(nameof(MajorLineThickness), typeof(BackgroundGridLines), 5);

    public static readonly AttachedProperty<IBrush> LineBrushProperty = AvaloniaProperty.RegisterAttached<BackgroundGridLines, IBrush>(nameof(LineBrush), typeof(BackgroundGridLines), Brushes.Gray);

    public static readonly DirectProperty<BackgroundGridLines, Rect> SnapGridProperty = AvaloniaProperty.RegisterDirect<BackgroundGridLines, Rect>(nameof(SnapGrid), o => o.SnapGrid);

    private Rect _snapGrid;

    public Rect SnapGrid
    {
        get { return _snapGrid; }
        private set { SetAndRaise(SnapGridProperty, ref _snapGrid, value); }
    }

    private readonly Pen _majorLinePen = new();
    private readonly Pen _minorLinePen = new();

    static BackgroundGridLines()
    {
        AffectsRender<BackgroundGridLines>(MajorLineSeparationProperty, MinorLineCountProperty, MajorLineThicknessProperty, LineBrushProperty, RenderTransformProperty);
        ZIndexProperty.OverrideDefaultValue<BackgroundGridLines>(-1000);
        FocusableProperty.OverrideDefaultValue<BackgroundGridLines>(false);
        IsEnabledProperty.OverrideDefaultValue<BackgroundGridLines>(false);
        SelectAndMove.IsSelectableProperty.OverrideDefaultValue<BackgroundGridLines>(false);
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
        if (_majorLinePen.Brush != LineBrush)
        {
            _majorLinePen.Brush = LineBrush;
        }

        if (_majorLinePen.Thickness != MajorLineThickness)
        {
            _majorLinePen.Thickness = MajorLineThickness;
        }

        if (_minorLinePen.Brush != LineBrush)
        {
            _minorLinePen.Brush = LineBrush;
        }

        Rect drawingBounds = GetRectInLocal(Parent!.Bounds);
        Vector renderScale = drawingBounds.Size / Parent.Bounds.Size;

        // Mutliplying by xRenderScale ensures the line thickness remains constant from the perspective of the parent
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

    public Rect GetSnapGrid(double xScale, double yScale) => new(Bounds.TopLeft, new Size(GetSpacing(xScale), GetSpacing(yScale)));

    //public Rect GetSnapGrid(double xScale, double yScale) => new(Bounds.TopLeft * GetValueOrIdentity(RenderTransform), new Size(GetSpacing(xScale) / xScale, GetSpacing(yScale) / yScale));

    public Rect GetRectInLocal(Rect rect)
    {
        Matrix transformToParent = GetValueOrIdentity(RenderTransform).Invert();
        Point TopLeftInParent = new Point(-Bounds.Left, -Bounds.Top) * transformToParent;
        Point BottomRightInParent = new Point(rect.Width - Bounds.Left, rect.Height - Bounds.Top) * transformToParent;
        return new Rect(TopLeftInParent, BottomRightInParent);
    }

    public IEnumerable<(double localCoordinate, bool isMajor)> GetLines(double start, double end, double scale)
    {
        double trueSpacing = GetSpacing(scale);
        double position = start - (start % trueSpacing);
        while (position < end)
        {
            yield return (position, ((int)Math.Round(position / trueSpacing)) % MinorLineCount == 0);
            position += trueSpacing;
        }
    }

    public double GetSpacing(double scale)
    {
        int subdivisionLevel = (int)Math.Round(Math.Log(scale, MinorLineCount));
        return MajorLineSeparation * Math.Pow(MinorLineCount, subdivisionLevel - 1);
    }

    private static Matrix GetValueOrIdentity(ITransform? transform) => transform is null ? Matrix.Identity : transform.Value;

    private static double TrueModulus(double x, double y) => ((x % y) + y) % y;
}
