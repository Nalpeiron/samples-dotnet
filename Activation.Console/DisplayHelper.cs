using Spectre.Console;
using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.Api;
using Output = System.Console;

namespace Activation.Console;

public static class DisplayHelper
{
    public static void ShowActivationInfoPanel(IActivation activation)
    {
        var status = "State: " + activation.State switch
        {
            ActivationState.Active => "[green]Active[/]",
            ActivationState.LeaseExpired => "[yellow]Lease Expired[/]",
            ActivationState.EntitlementNotActive => "[darkorange]Entitlement Not Active[/]",
            ActivationState.NotActivated => "[red]Not Activated[/]",
            _ => throw new ArgumentOutOfRangeException(nameof(activation.State), activation.State.ToString()),
        };

        var storage = $"Storage: {activation.Info.LocalStorageId}";
        var info = string.Join(Environment.NewLine, status, storage, Environment.NewLine, activation.Info.ToString().EscapeMarkup());
        AnsiConsole.Write(new Panel(info));
    }

    public static void ShowFeaturesTable(IEnumerable<IActivationFeature> features, string? keyToHighlight = null)
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
                feature.Active == null ? "" : feature.Active.Value.ToString(),
                feature.Available == null ? "Unlimited" : feature.Available.Value.ToString(),
                feature.Total == null ? "Unlimited" : feature.Total.Value.ToString());
        }

        AnsiConsole.Write(table);
    }

    public static void ShowEntitlementsTable(IEnumerable<EntitlementListItemModel> entitlements)
    {
        var table = new Table { ShowRowSeparators = true };
        table.AddColumn("Entitlement ID");
        table.AddColumn("Name");
        table.AddColumn("Product / ID");
        table.AddColumn("Edition / ID");
        table.AddColumn("Status");
        table.AddColumn("Seats used / count");
        table.AddColumn("Expiry UTC");
        foreach (var item in entitlements)
        {
            table.AddRow(item.Id.EscapeMarkup(), item.Name.EscapeMarkup(),
                $"{item.ProductName.EscapeMarkup()}\n{item.ProductId.EscapeMarkup()}",
                $"{item.EditionName.EscapeMarkup()}\n{item.EditionId.EscapeMarkup()}",
                item.Status.ToString(), $"{item.SeatsUsed} / {item.SeatCount}",
                item.ExpiryDate?.ToString("u") ?? "No scheduled expiry");
        }
        AnsiConsole.Write(table);
    }

    public static void WriteError(string message)
    {
        AnsiConsole.Write(new Panel(new Text(message))
            .Header("[red]Operation failed[/]")
            .BorderColor(Color.Red));
    }

    public static void WriteSuccess(string message)
    {
        Output.ForegroundColor = ConsoleColor.Green;
        Output.WriteLine(message);
        Output.ResetColor();
    }

    public static void WriteWarning(string message)
    {
        Output.ForegroundColor = ConsoleColor.Yellow;
        Output.WriteLine(message);
        Output.ResetColor();
    }
}
