namespace Laminar.Avalonia.SelectAndMove;

/// <summary>
/// Defines how selections will be snapped to the grid. If several controls are selected, the smallest box containing all of them is used for alignment
/// </summary>
public enum SnapMode
{
    /// <summary>
    /// No snapping
    /// </summary>
    None,

    /// <summary>
    /// Selections will be snapped to align their top left corner to the grid
    /// </summary>
    TopLeft,

    /// <summary>
    /// Selections will be snapped to align their top right corner to the grid
    /// </summary>
    TopRight,

    /// <summary>
    /// Selections will be snapped to align their bottom left corner to the grid
    /// </summary>
    BottomLeft,

    /// <summary>
    /// Selections will be snapped to align their bottom right corner to the grid
    /// </summary>
    BottomRight,

    /// <summary>
    /// Selections will be snapped to align their center to the grid
    /// </summary>
    Center,
}
