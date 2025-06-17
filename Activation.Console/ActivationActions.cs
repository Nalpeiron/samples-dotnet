using System.Text.Json;
using System.Text.Json.Serialization;
using Sharprompt;
using Spectre.Console;
using Spectre.Console.Json;
using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.Api;
using Output = System.Console;

namespace Activation.Console;

public static class ActivationActions
{
    private static readonly ActivationAction ActivateWithCode = new(
        "Activate license with code",
        async (activation, _) =>
        {
            var activationCode = Prompt.Input<string>("Enter activation code");
            var seatName =  Prompt.Input<string>("Enter seat name (keep empty for no seat name)");
            await activation.ActivateWithCode(activationCode, string.IsNullOrWhiteSpace(seatName) ? null : seatName);
            DisplayHelper.ShowActivationInfoPanel(activation);
        },
        [null]);

    private static readonly ActivationAction ActivateWithToken = new(
        "Activate license with OpenID token",
        async (activation, host) =>
        {
            var accessToken = await UserAuthenticator.AuthenticateAndReturnIdentityToken(host);
            if (accessToken == null)
            {
                return;
            }
            var seatName = Prompt.Input<string>("Enter seat name (keep empty for no seat name)");
            Output.WriteLine("Activating...");
            try
            {
                await activation.ActivateWithOpenIdToken(accessToken, seatName);
                DisplayHelper.ShowActivationInfoPanel(activation);
            }
            catch (LicensingApiException<ApiError> ex)
            {
                var apiError = ex.Result;
                DisplayHelper.WriteError($"Activation failed ({apiError.ErrorCode}): {apiError.Details}");
            }
            catch (LicensingApiException ex)
            {
                DisplayHelper.WriteError($"Activation failed: {ex.Message}");
            }
        },
        [null]);

    private static readonly ActivationAction GenerateOfflineActivationRequest = new(
        "Generate offline activation request (for End User Portal)",
        async (activation, _) =>
        {
            DisplayHelper.WriteWarning(
                "Make sure that the product/entitlement that you want to use for the offline activation has the " +
                "[Offline Lease Period] initialized, " +
                "otherwise the offline seat activation will not be possible.");

            var activationCode = Prompt.Input<string>("Enter activation code");
            var seatName =  Prompt.Input<string>("Enter seat name (keep empty for no seat name)");
            Output.WriteLine("Generating activation request token...");
            var token = await activation.GenerateOfflineActivationRequestToken(activationCode, seatName);
            Output.WriteLine("Activation request token (copy and use in the End User Portal):");
            DisplayHelper.WriteSuccess(token);
        },
        [null]);

    private static readonly ActivationAction ActivateOffline = new(
        "Activate offline (with activation response from End User Portal)",
        async (activation, _) =>
        {
            var offlineActivationResponseToken =
                Prompt.Input<string>("Enter offline activation response token");
            Output.WriteLine("Activating offline...");
            await activation.ActivateOffline(offlineActivationResponseToken);
            DisplayHelper.ShowActivationInfoPanel(activation);
        },
        [null]);

    private static readonly ActivationAction ShowActivationInfo = new(
        "Show activation info",
        (activation, _) =>
        {
            DisplayHelper.ShowActivationInfoPanel(activation);
            return Task.CompletedTask;
        },
        [ActivationMode.Online, ActivationMode.Offline, null]);

    private static readonly ActivationAction PullActivationStateFromServer = new(
        "Pull activation state from the server",
        async (activation, _) =>
        {
            Output.WriteLine("Pulling current activation state from the server...");
            await activation.PullRemoteState();
            DisplayHelper.ShowActivationInfoPanel(activation);
        },
        [ActivationMode.Online]);

    private static readonly ActivationAction PullActivationStateFromLocalStorage = new(
        "Pull activation state from the local storage",
        async (activation, _) =>
        {
            Output.WriteLine("Pulling current activation state from the local storage...");
            await activation.PullPersistedState();
            DisplayHelper.ShowActivationInfoPanel(activation);
        },
        [ActivationMode.Online, ActivationMode.Offline, null]);

    private static readonly ActivationAction RefreshActivationLease = new(
        "Refresh activation lease",
        async (activation, _) =>
        {
            Output.WriteLine("Refreshing current activation...");
            var previousLeaseExpiry = activation.Info.LeaseExpiry;
            var refreshed = await activation.RefreshLease();
            if (!refreshed)
            {
                Output.WriteLine($"Activation lease period could not be refreshed, please activate again. Current lease expiry is {activation.Info.LeaseExpiry}");
            }
            else
            {
                var newLeaseExpiry = activation.Info.LeaseExpiry;
                Output.WriteLine($"Activation lease successfully refreshed from [{previousLeaseExpiry:yyyy-MM-dd HH:mm:ss}] to [{newLeaseExpiry:yyyy-MM-dd HH:mm:ss}]");
            }
        },
        [ActivationMode.Online]);

    private static readonly ActivationAction RefreshOfflineActivationLease = new(
        "Refresh offline activation lease (with refresh token from End User Portal)",
        async (activation, _) =>
        {
            var previousLeaseExpiry = activation.Info.LeaseExpiry;
            var offlineRefreshToken = Prompt.Input<string>("Enter offline refresh token");
            Output.WriteLine("Refreshing current offline activation...");
            await activation.RefreshLeaseOffline(offlineRefreshToken);
            var newLeaseExpiry = activation.Info.LeaseExpiry;
            Output.WriteLine($"Activation lease successfully refreshed from [{previousLeaseExpiry:yyyy-MM-dd HH:mm:ss}] to [{newLeaseExpiry:yyyy-MM-dd HH:mm:ss}]");
        },
        [ActivationMode.Offline]);

    private static readonly ActivationAction CheckoutFeature = new(
        "Checkout advanced feature",
        async (activation, _) =>
        {
            var featuresToCheckout = activation.Info.Features
                .Where(x => x.Type != FeatureType.Bool && x.Available == null || x.Available > 0)
                .ToList();

            if (featuresToCheckout.Count == 0)
            {
                DisplayHelper.WriteError("There are no features eligible for checkout");
                return;
            }

            Output.WriteLine("Following features can be checked out:");
            DisplayHelper.ShowFeaturesTable(featuresToCheckout);

            var featureKey =
                Prompt.Select("Select feature to checkout", featuresToCheckout.Select(f => f.Key).Append("None"));
            if (featureKey == "None")
            {
                return;
            }

            var amountToCheckout = Prompt.Input<int>("Specify amount to checkout");
            Output.WriteLine($"Checking out {amountToCheckout} " +
                             $"{(amountToCheckout > 1 ? "features" : "feature")} with key '{featureKey}'");
            if (activation.Features.TryGet(featureKey, out var requestedFeature))
            {
                await activation.CheckoutFeature(requestedFeature, amountToCheckout);
                Output.WriteLine("Feature successfully checked out!");
            }
            else
            {
                DisplayHelper.WriteError($"Feature with key '{featureKey}' not found");
                return;
            }

            DisplayHelper.ShowFeaturesTable(activation.Info.Features.Where(x => x.Type != FeatureType.Bool), featureKey);
        },
        [ActivationMode.Online]);

    private static readonly ActivationAction ReturnFeature = new(
        "Return element-pool feature",
        async (activation, _) =>
        {
            var featuresToReturn = activation.Info.Features
                .Where(x => x.Active > 0 && x.Type == FeatureType.ElementPool).ToList();

            if (featuresToReturn.Count == 0)
            {
                DisplayHelper.WriteError("There are no features eligible for return");
                return;
            }

            Output.WriteLine("Following features can be returned:");
             DisplayHelper.ShowFeaturesTable(featuresToReturn);

            var featureKey =
                Prompt.Select("Select feature to return", featuresToReturn.Select(f => f.Key).Append("None"));
            if (featureKey == "None")
            {
                return;
            }

            var amountToCheckout = Prompt.Input<int>("Specify amount to return");

            Output.WriteLine($"Returning {amountToCheckout} " +
                             $"{(amountToCheckout > 1 ? "features" : "feature")} with key '{featureKey}'");

            if (activation.Features.TryGet(featureKey, out var requestedFeature))
            {
                await activation.ReturnFeature(requestedFeature, amountToCheckout);
                Output.WriteLine("Feature successfully returned!");
            }
            else
            {
                DisplayHelper.WriteError($"Feature with key '{featureKey}' not found");
                return;
            }

            DisplayHelper.ShowFeaturesTable(activation.Info.Features, featureKey);
        },
        [ActivationMode.Online]);

    private static readonly ActivationAction TrackBoolFeatureUsage = new(
        "Track usage of a bool feature",
        async (activation, _) =>
        {
            var boolFeatures = activation.Info.Features
                .Where(x => x.Type == FeatureType.Bool)
                .ToList();

            if (boolFeatures.Count == 0)
            {
                DisplayHelper.WriteError("There are no bool features");
                return;
            }

            Output.WriteLine("Usage can be tracked on the following features:");
            DisplayHelper.ShowFeaturesTable(boolFeatures);

            var featureKey =
                Prompt.Select("Select feature for tracking the usage", boolFeatures.Select(f => f.Key).Append("None"));
            if (featureKey == "None")
            {
                return;
            }

            if (activation.Features.TryGet(featureKey, out var requestedFeature))
            {
                await activation.TrackFeatureUsage(requestedFeature);
                Output.WriteLine("Feature usage successfully tracked!");
            }
            else
            {
                DisplayHelper.WriteError($"Feature with key '{featureKey}' not found");
            }
        },
        [ActivationMode.Online]);

    private static readonly ActivationAction GetActivationEntitlement = new(
        "Get entitlement associated with the activation",
        async (activation, _) =>
        {
            Output.WriteLine("Retrieving the entitlement...");
            var activationEntitlement = await activation.GetActivationEntitlement();
            var entitlementJson = JsonSerializer.Serialize(activationEntitlement, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });

            AnsiConsole.Write(
                new Panel(
                        new JsonText(entitlementJson).MemberColor(Color.Blue))
                    .Header("Activation Entitlement")
                    .Collapse()
                    .SquareBorder());
        },
        [ActivationMode.Online, ActivationMode.Offline]);

    private static readonly ActivationAction Deactivate = new(
        "Deactivate license",
        async (activation, _) =>
        {
            Output.WriteLine("Deactivating the license...");
            await activation.Deactivate();
        },
        [ActivationMode.Online]);

    private static readonly ActivationAction DeactivateOffline = new(
        "Deactivate offline license",
        async (activation, _) =>
        {
            Output.WriteLine("Deactivating the offline license...");
            var offlineDeactivationToken = await activation.DeactivateOffline();
            Output.WriteLine("Offline deactivation token (copy and use in the End User Portal):");
            DisplayHelper.WriteSuccess(offlineDeactivationToken);
        },
        [ActivationMode.Offline]);


    public static readonly Dictionary<ActivationState, ActivationAction[]> AvailableActions = new()
    {
        {
            ActivationState.Active,
            [
                ShowActivationInfo, PullActivationStateFromServer, PullActivationStateFromLocalStorage,
                CheckoutFeature, ReturnFeature, TrackBoolFeatureUsage, RefreshActivationLease, RefreshOfflineActivationLease,
                Deactivate, DeactivateOffline, GetActivationEntitlement
            ]
        },
        {
            ActivationState.LeaseExpired,
            [
                ShowActivationInfo, PullActivationStateFromServer, PullActivationStateFromLocalStorage,
                RefreshActivationLease, RefreshOfflineActivationLease, Deactivate, DeactivateOffline,
                GetActivationEntitlement
            ]
        },
        {
            ActivationState.NotActivated,
            [
                ShowActivationInfo, PullActivationStateFromLocalStorage,
                ActivateWithCode, ActivateWithToken, GenerateOfflineActivationRequest, ActivateOffline
            ]
        },
        {
            ActivationState.EntitlementNotActive,
            [
                ShowActivationInfo, PullActivationStateFromServer, PullActivationStateFromLocalStorage,
                GetActivationEntitlement,
                ActivateWithCode, ActivateWithToken, GenerateOfflineActivationRequest, ActivateOffline
            ]
        }
    };
}