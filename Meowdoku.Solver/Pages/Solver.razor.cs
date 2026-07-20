using Microsoft.AspNetCore.Components;

namespace Meowdoku.Solver.Pages;

public partial class Solver : ComponentBase, IDisposable
{
    private IDisposable? _subscription;

    [Parameter]
    public string? Locale
    {
        get;
        set => field = value ?? "en";
    } = "en";

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

    private Dictionary<string, string> Colors = new()
    {
        ["1"] = "#f89be5",
        ["2"] = "#d36f90",
        ["3"] = "#8cd57d",
        ["4"] = "#37a9c0",
        ["5"] = "#8979da",
        ["6"] = "#a76d4a",
        ["7"] = "#298c53",
        ["8"] = "#cda400",
        ["9"] = "#fbd983",
        ["10"] = "#FFA500",
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
            field = value;
            Game = new Game(field);
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
}