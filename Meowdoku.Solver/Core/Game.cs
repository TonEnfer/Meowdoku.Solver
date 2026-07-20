namespace Meowdoku.Solver.Pages;

internal class Game(int size)
{
    public int Size { get; } = size;

    public List<Cell> Cells { get; } =
        Enumerable
            .Range(0, size * size)
            .Select(i => new Cell((i / size, i % size), "1"))
            .ToList();

    private bool IsSolved => Cells.All(x => x.IsEmpty.HasValue);

    private readonly List<OnStateChangedHandler> _onStateChangedHandlers = [];

    private readonly List<TryApplySolveAction> _solveActions =
    [
        static game => _solveSingles(game),
        static game => _solveGroups(game),
        static game => _solveDoubles(game),
    ];

    public IDisposable SubscribeOnStateChanged(Action invocable)
    {
        var handler = new OnStateChangedHandler(_onStateChangedHandlers, invocable);
        _onStateChangedHandlers.Add(handler);

        return handler;
    }

    public async Task<bool> SolveAsync()
    {
        var attempts = 0;

        Cells.ForEach(x =>
        {
            x.IsEmpty = null;
            x.Rule = null;
        });

        InvokeOnStateChangedHandlers();

        while (!IsSolved && attempts < 1000)
        {
            var any = false;
            foreach (var tryApplySolveAction in _solveActions)
            {
                if (!tryApplySolveAction(this)) continue;

                any = true;
                InvokeOnStateChangedHandlers();
            }

            if (!any && !IsSolved)
            {
                return false;
            }

            attempts++;
        }

        return IsSolved;
    }

    private void InvokeOnStateChangedHandlers()
    {
        _onStateChangedHandlers.ForEach(x => x.Invocable.Invoke());
    }

    private static bool _solveGroups(Game game)
    {
        var cellsWithUnknownState = game.Cells.Where(x => !x.IsEmpty.HasValue).ToList();

        var cellsByColor = cellsWithUnknownState
            .GroupBy(x => x.Color)
            .OrderBy(x => x.Count())
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var cellColorGroup in cellsByColor)
        {
            var cols = cellColorGroup.Value.Select(x => x.Index.Y).Distinct().ToList();

            //Выбираем другие цвета, целиком расположенные в тех же столбцах
            var otherColorCellInSameCols = cellsByColor
                .Where(x => x.Value.All(y => cols.Contains(y.Index.Y)))
                .ToDictionary(y => y.Key, y => y.Value);

            if (otherColorCellInSameCols.Count == cols.Count)
            {
                var emptyCells = cellsWithUnknownState
                    .Except(cellColorGroup.Value)
                    .Except(otherColorCellInSameCols.Values.SelectMany(x => x))
                    .Where(x => cols.Contains(x.Index.Y))
                    .ToList();

                foreach (var emptyCell in emptyCells)
                {
                    emptyCell.IsEmpty = true;
                    emptyCell.Rule = "Group";
                    Console.WriteLine(
                        $"{emptyCell.Index.X},{emptyCell.Index.Y} пустая из-за того, что находится в {cols.Count} столбцах {(string.Join(", ", cols))}, занятых {otherColorCellInSameCols.Count} цветами: {string.Join(", ", otherColorCellInSameCols.Keys)}");

                    game.InvokeOnStateChangedHandlers();
                }

                if (emptyCells.Any())
                    return true;
            }

            var rows = cellColorGroup.Value.Select(x => x.Index.X).Distinct().ToList();

            //Выбираем другие цвета, целиком расположенные в тех же строках
            var otherColorCellInSameRows = cellsByColor
                .Where(x => x.Value.All(y => rows.Contains(y.Index.X)))
                .ToDictionary(y => y.Key, y => y.Value);

            if (otherColorCellInSameRows.Count == rows.Count)
            {
                var emptyCells = cellsWithUnknownState
                    .Except(cellColorGroup.Value)
                    .Except(otherColorCellInSameRows.Values.SelectMany(x => x))
                    .Where(x => rows.Contains(x.Index.X))
                    .ToList();

                foreach (var emptyCell in emptyCells)
                {
                    emptyCell.IsEmpty = true;
                    emptyCell.Rule = "Group";
                    Console.WriteLine(
                        $"{emptyCell.Index.X},{emptyCell.Index.Y} пустая из-за того, что находится в {rows.Count} строках {(string.Join(", ", rows))}, занятых {otherColorCellInSameRows.Count} цветами: {string.Join(", ", otherColorCellInSameRows.Keys)}");
                    game.InvokeOnStateChangedHandlers();
                }

                if (emptyCells.Any())
                    return true;
            }
        }

        return false;
    }

    private static bool _solveDoubles(Game game)
    {
        var cellsWithUnknownState = game.Cells
            .Where(x => !x.IsEmpty.HasValue)
            .ToList();

        var doubles = cellsWithUnknownState
            .GroupBy(x => x.Color)
            .Where(x => x.Count() == 2)
            .Select(x => x.ToList())
            .Where(x => (x[0].Index.X == x[1].Index.X &&
                         (x[0].Index.Y == x[1].Index.Y + 1 || x[0].Index.Y == x[1].Index.Y - 1)) ||
                        (x[0].Index.Y == x[1].Index.Y &&
                         (x[0].Index.X == x[1].Index.X + 1 || x[0].Index.X == x[1].Index.X - 1)))
            .ToList();

        if (!doubles.Any())
        {
            return false;
        }

        foreach (var doubleCells in doubles)
        {
            var isRow = doubleCells[0].Index.Y == doubleCells[1].Index.Y;

            if (!isRow)
            {
                var emptyCells = cellsWithUnknownState
                    .Except(doubleCells)
                    .Where(x =>
                        (x.Index.X == doubleCells[0].Index.X
                         || x.Index.X == doubleCells[1].Index.X) &&
                        (x.Index.Y == doubleCells[0].Index.Y + 1 ||
                         x.Index.Y == doubleCells[0].Index.Y - 1)
                    ).ToList();

                if (emptyCells.Count == 0) continue;

                foreach (var emptyCell in emptyCells)
                {
                    emptyCell.IsEmpty = true;
                    emptyCell.Rule = "Double";
                    Console.WriteLine(
                        $"{emptyCell.Index.X},{emptyCell.Index.Y} пустая из-за того, что граничит с группой из двух клеток: [{doubleCells[0].Index.X},{doubleCells[0].Index.Y}], [{doubleCells[1].Index.X},{doubleCells[1].Index.Y}]");
                    game.InvokeOnStateChangedHandlers();
                }
            }
            else
            {
                var emptyCells = cellsWithUnknownState
                    .Except(doubleCells)
                    .Where(x =>
                        (x.Index.X == doubleCells[0].Index.X - 1
                         || x.Index.X == doubleCells[0].Index.X + 1) &&
                        (x.Index.Y == doubleCells[0].Index.Y ||
                         x.Index.Y == doubleCells[1].Index.Y)
                    ).ToList();

                if (emptyCells.Count == 0) continue;

                foreach (var emptyCell in emptyCells)
                {
                    emptyCell.IsEmpty = true;
                    emptyCell.Rule = "Double";
                    Console.WriteLine(
                        $"{emptyCell.Index.X},{emptyCell.Index.Y} пустая из-за того, что граничит с группой из двух [{doubleCells[0].Index.X},{doubleCells[0].Index.Y}], [{doubleCells[1].Index.X},{doubleCells[1].Index.Y}]");
                    game.InvokeOnStateChangedHandlers();
                }
            }

            return true;
        }

        return false;
    }

    private static bool _solveSingles(Game game)
    {
        var singles = game.Cells
            .Where(x => !x.IsEmpty.HasValue)
            .GroupBy(x => x.Color)
            .Where(x => x.Count() == 1)
            .Select(x => new KeyValuePair<string, Cell>(x.Key, x.First()!))
            .ToDictionary();

        if (singles.Count == 0)
            return false;

        foreach (var singleColorCell in singles.Values)
        {
            singleColorCell.IsEmpty = false;
            singleColorCell.Rule = "Single";
            game.InvokeOnStateChangedHandlers();
            var row = singleColorCell.Index.X;
            var col = singleColorCell.Index.Y;

            foreach (var cell in game.Cells
                         .Where(x => !x.IsEmpty.HasValue)
                         .Where(cell => cell.Index != singleColorCell.Index))
            {
                if (cell.Index.X == row ||
                    cell.Index.Y == col ||
                    ((cell.Index.X == row - 1 || cell.Index.X == row + 1) &&
                     (cell.Index.Y == col - 1 || cell.Index.Y == col + 1)))
                {
                    cell.IsEmpty = true;
                    cell.Rule = "Single";
                    Console.WriteLine(
                        $"{cell.Index.X},{cell.Index.Y} пустая из-за того, что граничит с {singleColorCell.Index.X}, {singleColorCell.Index.Y}");
                    game.InvokeOnStateChangedHandlers();
                }
            }
        }


        return true;
    }

    private delegate bool TryApplySolveAction(Game cells);

    internal class OnStateChangedHandler(List<OnStateChangedHandler> list, Action invocable) : IDisposable
    {
        internal readonly Action Invocable = invocable;

        public void Dispose()
        {
            list.Remove(this);
        }
    }
}