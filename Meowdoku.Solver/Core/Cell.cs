using System.Diagnostics;

namespace Meowdoku.Solver.Pages;

[DebuggerDisplay("[{Color}] = ({Index.X}; {Index.Y})")]
internal class Cell((int X, int Y) index, string color, bool? isEmpty = null)
{
    public (int X, int Y) Index { get; } = index;
    public string Color { get; set; } = color;
    public bool? IsEmpty { get; set; } = isEmpty;
    
    public string? Rule { get; set; }
}