using Avalonia;
using Avalonia.Controls.Shapes;

namespace Laminar.Avalonia.SelectAndMove.UnitTests;

public class SelectAndMoveTests
{
    readonly SelectAndMove _sut = new();

    public static IEnumerable<object[]> FitToViewTestData => new List<object[]>
    {
            new object[] { new Rect(100, 200, 100, 200), new Rect(0, 0, 400, 600), new Matrix(3.0, 0.0, 0.0, 3.0, -250.0, -600.0) },
            new object[] { new Rect(-100, -200, 1000, 2000), new Rect(100, 100, 500, 600), new Matrix(0.3, 0.0, 0.0, 0.3, 130.0, 60.0) },
            new object[] { new Rect(1000, 2000, 20, 10), new Rect(200, 200, -100, -200), new Matrix(-20, 0.0, 0.0, -20.0, 20150.0, 40000.0) },
    };

    [Theory, MemberData(nameof(FitToViewTestData))]
    public void FitViewToRect(Rect viewRect, Rect arrangeRect, Matrix newTransform)
    {
        Rectangle testChild = new();

        _sut.Children.Add(testChild);

        _sut.FitToViewRectWithManualBounds(viewRect, arrangeRect);

        Assert.Equal(newTransform, testChild.RenderTransform?.Value);
    }
}
