using Sharprompt;
using Zentitle.Licensing.Client.ActivationCredentials;
using Zentitle.Licensing.Client.Api;
using Output = System.Console;

namespace Activation.Console;

public sealed class EntitlementMenu(ActivationCredentialsSession session)
{
    private const int PageSize = 10;
    private const string List = "List available entitlements";
    private const string Activate = "Activate selected product";
    private const string BackToSession = "Return to authenticated menu";
    private EntitlementListItemModel? _selected;
    private string? _editionId;

    public bool RequiresAuthentication { get; private set; }

    public async Task<DiscoveredActivation?> Run(DiscoveryInteraction interaction)
    {
        while (!interaction.IsCancellationRequested)
        {
            if (!CheckSession())
            {
                return null;
            }
            Output.WriteLine($"Credentials session active until {session.ExpiresAt:u}.");
            var actions = new List<string> { List };
            if (_selected != null)
            {
                actions.Add(Activate);
                Output.WriteLine($"Selected product: {_selected.ProductName} ({_selected.ProductId}), edition: {_editionId ?? "server selection"}.");
            }
            actions.Add(DiscoveryInteraction.BackToMainMenu);
            switch (Prompt.Select("Authenticated session", actions, defaultValue: List))
            {
                case List:
                    if (await BrowseEntitlements(interaction))
                    {
                        return null;
                    }
                    break;
                case Activate:
                    if (!CheckSession())
                    {
                        return null;
                    }
                    return new DiscoveredActivation(session, _selected!.ProductId, _editionId);
                default:
                    return null;
            }
        }
        return null;
    }

    // Returns true when the user chooses to return to the main menu.
    private async Task<bool> BrowseEntitlements(DiscoveryInteraction interaction)
    {
        var pageNumber = 1;
        DisplayHelper.WriteWarning("Listing shows authorized entitlements and does not reserve seats or guarantee activation eligibility.");
        while (true)
        {
            var page = await interaction.TryRequest(ct => session.ListEntitlements(pageNumber, PageSize, ct));
            if (page == null)
            {
                return false;
            }
            Output.WriteLine($"Page {page.PageNumber}, total {page.TotalElements} entitlements.");
            DisplayHelper.ShowEntitlementsTable(page.Items);
            var choices = new List<string>();
            if (page.Items.Count > 0)
            {
                choices.Add("Select product and edition from this page");
            }
            if ((long)page.PageNumber * page.PageSize < page.TotalElements)
            {
                choices.Add("Next page");
            }
            if (pageNumber > 1)
            {
                choices.Add("Previous page");
            }
            choices.Add(BackToSession);
            choices.Add(DiscoveryInteraction.BackToMainMenu);
            switch (Prompt.Select("Entitlements", choices, defaultValue: choices[0]))
            {
                case "Next page": pageNumber++; break;
                case "Previous page": pageNumber--; break;
                case "Select product and edition from this page":
                    var rows = page.Items.Select((item, index) => $"{index + 1}. {item.ProductName} / {item.EditionName} / {item.Name}").ToList();
                    rows.Add("Return to entitlement list");
                    rows.Add(DiscoveryInteraction.BackToMainMenu);
                    var row = Prompt.Select("Select entitlement row for product and edition", rows, defaultValue: rows[0]);
                    if (row == DiscoveryInteraction.BackToMainMenu)
                    {
                        return true;
                    }
                    var index = rows.IndexOf(row);
                    if (index >= page.Items.Count)
                    {
                        break;
                    }
                    var selected = page.Items.ElementAt(index);
                    var edition = Prompt.Select("Edition for activation", [
                        "Use selected edition", "Let server choose edition", "Return to entitlement list", DiscoveryInteraction.BackToMainMenu
                    ], defaultValue: "Use selected edition");
                    if (edition == DiscoveryInteraction.BackToMainMenu)
                    {
                        return true;
                    }
                    if (edition == "Return to entitlement list")
                    {
                        break;
                    }
                    _selected = selected;
                    _editionId = edition == "Use selected edition" ? selected.EditionId : null;
                    Output.WriteLine("Selection chooses a product and optional edition. The server will choose the entitlement using its normal rules.");
                    return false;
                case DiscoveryInteraction.BackToMainMenu: return true;
                default: return false;
            }
        }
    }

    private bool CheckSession()
    {
        RequiresAuthentication = session.IsExpired;
        return !RequiresAuthentication;
    }
}
