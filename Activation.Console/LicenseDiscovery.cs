using Microsoft.Extensions.Hosting;
using Sharprompt;
using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.ActivationCredentials;
using Zentitle.Licensing.Client.ActivationCredentials.Exceptions;

namespace Activation.Console;

// Coordinates authentication, entitlement browsing, and credentials-session activation.
public sealed class LicenseDiscovery(IActivationCredentialsClient client)
{
    private EntitlementMenu? _entitlements;

    public async Task<DiscoveredActivation?> Run(IHost host)
    {
        var interaction = new DiscoveryInteraction(host);
        while (!interaction.IsCancellationRequested)
        {
            if (_entitlements == null)
            {
                var session = await new AuthenticationMenu(client).Run(host, interaction);
                if (session == null)
                {
                    return null;
                }
                _entitlements = new EntitlementMenu(session);
            }

            try
            {
                var selection = await _entitlements.Run(interaction);
                if (_entitlements.RequiresAuthentication)
                {
                    ResetExpiredSession();
                    continue;
                }
                if (selection == null)
                {
                    _entitlements = null;
                }
                return selection;
            }
            catch (ActivationCredentialsSessionExpiredException)
            {
                ResetExpiredSession();
            }
        }
        return null;
    }

    public async Task<bool> ActivateSelectedProduct(DiscoveredActivation selection, ISharedActivation activation,
        CancellationToken stopping)
    {
        try
        {
            await activation.ExecuteWithLock(async a =>
            {
                var seatName = Prompt.Input<string>("Enter seat name (keep empty for no seat name)");
                await a.ActivateWithCredentialsSession(selection.Session,
                    string.IsNullOrWhiteSpace(seatName) ? null : seatName, selection.EditionId);
                DisplayHelper.ShowActivationInfoPanel(a);
            }, stopping);
            return true;
        }
        catch (ActivationCredentialsSessionExpiredException)
        {
            ResetExpiredSession();
            return false;
        }
    }

    private void ResetExpiredSession()
    {
        _entitlements = null;
        DisplayHelper.WriteError("The credentials session expired. Authenticate again to list or activate.");
    }
}

public sealed record DiscoveredActivation(ActivationCredentialsSession Session, string ProductId, string? EditionId);
