using Avalonia;

namespace Laminar.Avalonia.SelectAndMove;

public interface ICustomHitResolver
{
    public bool ContainsPoint(Point point);

    public bool IntersectsWithRectangle(Rect rectangle);
}
