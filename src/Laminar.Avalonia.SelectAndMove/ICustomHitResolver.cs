using Avalonia.Media;

namespace Laminar.Avalonia.SelectAndMove;

public interface ICustomHitResolver
{
    public Geometry GetCustomSelectionGeometry();
}
