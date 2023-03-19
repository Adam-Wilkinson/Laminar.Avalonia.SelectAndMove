namespace Laminar.Avalonia.SelectAndMove.UnitTests;

public class BackgroundGridLinesTests
{
    readonly BackgroundGridLines _sut = new();

    [Theory]
    [InlineData(2.0001, 4, 1)]
    [InlineData(1.9999, 4, 0)] // Either side of a switch from 0 to 1
    [InlineData(10, 6, 0.094)] 
    [InlineData(0.1, 3, 0.462)]
    public void GetMinorLineThicknessScale(double scale, int minorLineCount, double expectedThicknessScale)
    {
        _sut.MinorLineCount = minorLineCount;

        double returnedThicknessScale = _sut.GetMinorLineThicknessScale(scale);

        Assert.Equal(expectedThicknessScale, returnedThicknessScale, 0.01);
    }

    [Theory]
    [InlineData(1, 4, 200, 200 / 4)] // A scale of one means we just subdivide MajorLineSeparation by MinorLineCount
    [InlineData(2, 4, 200, 200 / 4)] // This scale shouldn't be big enough to change the number of subdivisions
    [InlineData(3, 2, 200, 400)] // If there are only two minor lines for each major line, a scale of 3x should change behaviour
    [InlineData(5, 4, 200, 200)] // At a scale of 5x, the spacing should be equal to MajorLineSeparation
    [InlineData(50, 5, 100, 100 * 5)] // At this scale, the spacing should be bigger than MajorLineSeparation
    [InlineData(0.2, 4, 200, 200.0 / 16)] // Zoomed in, the spacing should decrease by another factor of MinorLineSeparation
    [InlineData(0.001, 6, 100, 100.0 / 7776)] // A more extreme zoomed in test, should return 100 * 6^(-5)
    public void GetSpacing(double scale, int minorLineCount, double majorLineSeparation, double expectedSpacing)
    {
        _sut.MinorLineCount = minorLineCount;
        _sut.MajorLineSeparation = majorLineSeparation;

        double returnedSpacing = _sut.GetSpacing(scale);

        Assert.Equal(expectedSpacing, returnedSpacing);
    }
}