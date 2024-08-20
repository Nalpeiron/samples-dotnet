using Zentitle.Licensing.Client.Api;

namespace Activation.Console.Testing;

/// <summary>
/// A client that will override the activation lease expiry returned from the Licensing API, so it expires in a given timespan.
/// </summary>
public sealed class ExpiringActivationLicensingApiClient : LicensingApiClient
{
    public ExpiringActivationLicensingApiClient(LicensingApiOptions configuration, HttpClient httpClient) : base(
        configuration, httpClient)
    {
        ReadResponseAsString = true;
    }

    public required TimeSpan LeaseExpiry { get; init; }

    public override async Task<ActivationStateModel> Activation_GetActivationStateAsync(
        CancellationToken cancellationToken)
    {
        var result = await base.Activation_GetActivationStateAsync(cancellationToken);
        result.LeaseExpiry = DateTime.UtcNow.Add(LeaseExpiry);
        return result;
    }

    public override async Task<ActivationModel> Activate_ActivateAsync(ActivateEntitlementApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await base.Activate_ActivateAsync(request, cancellationToken);
        result.LeaseExpiry = DateTime.UtcNow.Add(LeaseExpiry);
        return result;
    }

    public override async Task<ActivationModel> Activation_RefreshAsync(CancellationToken cancellationToken)
    {
        var result = await base.Activation_RefreshAsync(cancellationToken);
        result.LeaseExpiry = DateTime.UtcNow.Add(LeaseExpiry);
        return result;
    }
}