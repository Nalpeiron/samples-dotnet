using Spectre.Console;
using Zentitle.Licensing.Client;
using Output = System.Console;

namespace OnlineActivation.Console;

public static class DisplayHelper
{
    public static void ShowActivationInfoPanel(IActivation activation) 
        => AnsiConsole.Write(new Panel(activation.Info.ToString()));

    public static void ShowFeaturesTable(IEnumerable<ActivationFeature> features, string? keyToHighlight = null)
    {
        var table = new Table { ShowRowSeparators = true };
        table.AddColumn("Feature Key");
        table.AddColumn("Feature Type");
        table.AddColumn("Active");
        table.AddColumn("Available");
        table.AddColumn("Total");

        foreach (var feature in features)
        {
            table.AddRow(
                keyToHighlight == feature.Key ? $"[blue]{feature.Key}[/]" : feature.Key, 
                feature.Type.ToString(), 
                feature.Active?.ToString() ?? string.Empty, 
                feature.Available?.ToString() ?? string.Empty, 
                feature.Total?.ToString() ?? string.Empty);
        }
        
        AnsiConsole.Write(table);
    }

    public static void WriteError(string message)
    {
        Output.ForegroundColor = ConsoleColor.Red;
        Output.WriteLine(message);
        Output.ResetColor();
    }
}