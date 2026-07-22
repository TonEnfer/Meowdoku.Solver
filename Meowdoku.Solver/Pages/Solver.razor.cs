using Meowdoku.Solver.ImageRecognitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Meowdoku.Solver.Pages;

public partial class Solver : ComponentBase, IDisposable
{
    private IDisposable? _subscription;

    [Parameter] public string? DetectionResult { get; set; }

    private string? Result { get; set; }

    private byte[] ImgBytes
    {
        get;
        set
        {
            field = value;
            StateHasChanged();
        }
    } = [];

    private Dictionary<string, string> DefaultColors = new()
    {
        ["1"] = "#fbd983",
        ["2"] = "#d36f90",
        ["3"] = "#8cd57d",
        ["4"] = "#37a9c0",
        ["5"] = "#8979da",
        ["6"] = "#a76d4a",
        ["7"] = "#298c53",
        ["8"] = "#cda400",
        ["9"] = "#f89be5",
        ["10"] = "#FFA500",
        ["11"] = "#00A5FF",
        ["12"] = "#A5FF00",
    };

    private Dictionary<string, string> Colors = new()
    {
        ["1"] = "#fbd983",
        ["2"] = "#d36f90",
        ["3"] = "#8cd57d",
        ["4"] = "#37a9c0",
        ["5"] = "#8979da",
        ["6"] = "#a76d4a",
        ["7"] = "#298c53",
        ["8"] = "#cda400",
        ["9"] = "#f89be5",
        ["10"] = "#FFA500",
        ["11"] = "#00A5FF",
        ["12"] = "#A5FF00",
    };


    private string CurrentColor = "1";

    private Game Game
    {
        get;
        set
        {
            field = value;
            _subscription?.Dispose();
            _subscription = field.SubscribeOnStateChanged(StateHasChanged);
        }
    } = new(4);

    private int GameSize
    {
        get;
        set
        {
            field = Math.Clamp(value, 4, 12);
            Colors = new Dictionary<string, string>(DefaultColors);
            Game = new Game(field);
            Result = null;
            DetectionResult = null;
        }
    } = 4;

    private string GetColor(string color)
    {
        return Colors.GetValueOrDefault(color, "white");
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }

    private async Task OnClickSolveAsync()
    {
        Result = null;
        Result = await Game.SolveAsync()
            ? Localizer[nameof(Localization.SuccessSolutionResultText)].Value
            : Localizer[nameof(Localization.ErrorSolutionResultText)].Value;
    }

    private async Task LoadFileAsync(InputFileChangeEventArgs e)
    {
        Result = null;
        DetectionResult = null;

        // Get the selected file
        var file = e.File;

        // Define a maximum allowed size (e.g., 50 MB) to protect memory
        const long maxFileSize = 1024 * 1024 * 50;

        // Open a stream to read the file contents
        await using var stream = file.OpenReadStream(maxFileSize);
        using var memoryStream = new MemoryStream();

        await stream.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        ImgBytes = fileBytes;

        StateHasChanged();

        try
        {
            var detector = new GridDetector(Localizer);
            var result = detector.DetectGrid( fileBytes);
            
            if (result.Cols != result.Rows)
            {
                DetectionResult = Localizer[
                    nameof(Localization.ErrorGridDetectionText),
                    Localizer[nameof(Localization.GridDetectionRowColCountNotEqualText)].Value
                ].Value;
            }

            GameSize = result.Cols;

            Colors.Clear();

            foreach (var color in result.Cells.Select(x => x.Color).Distinct()
                         .Select((x, i) => (index: (i + 1).ToString(), name: $"#{x.Name[2..]}")))
            {
                Colors.Add(color.index, color.name);
            }

            Game = new Game(result.Cols);

            foreach (var gameCell in Game.Cells)
            {
                var originalCell = result.Cells.First(x => x.Col == gameCell.Index.Y && x.Row == gameCell.Index.X);
                var colorIndex = Colors
                    .FirstOrDefault(x => x.Value == $"#{originalCell.Color.Name[2..]}");
                gameCell.Color = colorIndex.Key;
            }

            await OnClickSolveAsync();
        }
        catch (Exception ex)
        {
            DetectionResult = Localizer[nameof(Localization.ErrorGridDetectionText), ex.Message].Value;
        }
    }
}