using Avalonia;
using Laminar.Avalonia.SelectAndMove.GestureRecognizers;

namespace Laminar.Avalonia.SelectAndMove.UnitTests.GestureRecognisers.UnitTests;

public class MoveSelectionGestureTests
{
    public static IEnumerable<object[]> SnapToRectTestData => new List<object[]>
    {
        new object[] { new Point(0, 0), new Rect(10, 10, 50, 50), new Point(10, 10) },
        new object[] { new Point(50, 50), new Rect(-5, -20, 20, 20), new Point(55, 60) },
        new object[] { new Point(-100, -400), new Rect(25, 35, 100, 100), new Point(-75, -365) },
    };

    [Theory, MemberData(nameof(SnapToRectTestData))]
    public void SnapToGrid(Point originalLocation, Rect snapGrid, Point newLocation)
    {
        Point newPoint = MoveSelectionGesture.Snap(originalLocation, snapGrid);

        Assert.Equal(newLocation, newPoint);
    }
}
