using System.Text.Json;
using System.Text.Json.Serialization;
using Sharprompt;
using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.Api;
using Output = System.Console;

namespace OnlineActivation.Console;

public static class ActivationActions
{
    private static readonly ActivationAction ActivateWithCode = new(
        "Activate license with code",
        async (activation, _) =>
        {
            var activationCode = Prompt.Input<string>("Enter activation code");
            var seatName =  Prompt.Input<string>("Enter seat name (keep empty for no seat name)");
            Output.WriteLine("Activating...");
            await activation.ActivateWithCode(activationCode, string.IsNullOrWhiteSpace(seatName) ? null : seatName);
            DisplayHelper.ShowActivationInfoPanel(activation);
        });

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
        });

    private static readonly ActivationAction ShowActivationInfo = new(
        "Show activation info", 
        (activation, _) =>
        {
            DisplayHelper.ShowActivationInfoPanel(activation);
            return Task.CompletedTask;
        });

    private static readonly ActivationAction PullActivationStateFromServer = new(
        "Pull activation state from the server",
        async (activation, _) =>
        {
            Output.WriteLine("Pulling current activation state from the server...");
            await activation.PullRemoteState();
            DisplayHelper.ShowActivationInfoPanel(activation);
        });
    
    private static readonly ActivationAction PullActivationStateFromLocalStorage = new(
        "Pull activation state from the local storage",
        async (activation, _) =>
        {
            Output.WriteLine("Pulling current activation state from the local storage...");
            await activation.PullPersistedState();
            DisplayHelper.ShowActivationInfoPanel(activation);
        });

    private static readonly ActivationAction RefreshActivationLease = new(
        "Refresh activation lease",
        async (activation, _) =>
        {
            Output.WriteLine("Refreshing current activation...");
            var previousLeaseExpiry = activation.Info.LeaseExpiry;
            var refreshed = await activation.RefreshLease();
            if (!refreshed)
            {
                Output.WriteLine("Activation lease period could not be refreshed, please activate again");
            }
            
            var newLeaseExpiry = activation.Info.LeaseExpiry;
            Output.WriteLine($"Activation lease successfully refreshed from [{previousLeaseExpiry:yyyy-MM-dd HH:mm:ss}] to [{newLeaseExpiry:yyyy-MM-dd HH:mm:ss}]");
        });

    private static readonly ActivationAction CheckoutFeature = new(
        "Checkout consumption/element-pool feature",
        async (activation, _) =>
        {
            var featuresToCheckout = activation.Info.Features.Where(x => x.Available > 0).ToList();
            
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
            await activation.Features.Checkout(featureKey, amountToCheckout);
            
            Output.WriteLine("Feature successfully checked out!");
            DisplayHelper.ShowFeaturesTable(activation.Info.Features, featureKey);
        });

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
            await activation.Features.Return(featureKey, amountToCheckout);
            
            Output.WriteLine("Feature successfully returned!");
             DisplayHelper.ShowFeaturesTable(activation.Info.Features, featureKey);
        });

    private static readonly ActivationAction GetActivationEntitlement = new(
        "Get entitlement associated with the activation",
        async (activation, _) =>
        {
            Output.WriteLine("Retrieving the entitlement...");
            var activationEntitlement = await activation.GetActivationEntitlement();
            Output.WriteLine("Activation entitlement:");
            Output.WriteLine(JsonSerializer.Serialize(activationEntitlement, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            }));
        });

    private static readonly ActivationAction Deactivate = new(
        "Deactivate license",
        async (activation, _) =>
        {
            Output.WriteLine("Deactivating the license...");
            await activation.Deactivate();
        });

    public static readonly Dictionary<ActivationState, ActivationAction[]> AvailableActions = new()
    {
        {
            ActivationState.Active,
            [
                ShowActivationInfo, PullActivationStateFromServer, PullActivationStateFromLocalStorage,
                CheckoutFeature, ReturnFeature, RefreshActivationLease,
                Deactivate, GetActivationEntitlement 
            ]
        },
        {
            ActivationState.LeaseExpired,
            [
                ShowActivationInfo, PullActivationStateFromServer, PullActivationStateFromLocalStorage, 
                RefreshActivationLease, Deactivate, GetActivationEntitlement
            ]
        },
        {
            ActivationState.NotActivated,
            [ShowActivationInfo, PullActivationStateFromLocalStorage, ActivateWithCode, ActivateWithToken]
        },
        {
            ActivationState.EntitlementNotActive,
            [ShowActivationInfo, PullActivationStateFromServer, PullActivationStateFromLocalStorage, 
             ActivateWithCode, ActivateWithToken, GetActivationEntitlement]
        }
    };
}