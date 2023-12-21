using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.Persistence.Storage;
using Zentitle.Licensing.Client.States;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json").Build();

Console.WriteLine("""
Please make sure, that the tenant details are configured properly in the 'appsettings.json' file.
If the 'TenantKeyModulus' isn't matching the 'n' part from the API call '{ORION_API_URL}/api/account/key', 
update it accordingly and RESTART the app. 
Press any key to proceed...
""");
Console.ReadKey();

Console.WriteLine("Reading the configuration file...");
var tenantId = configuration.GetValue<string>("TenantId");
if (string.IsNullOrWhiteSpace(tenantId))
{
    throw new Exception("Invalid configuration, TenantId not set in appsettings.json");
}

Console.WriteLine($"Using tenant: {tenantId}");

var tenantKeyModulus = configuration.GetValue<string>("TenantKeyModulus");
if (string.IsNullOrWhiteSpace(tenantKeyModulus))
{
    throw new Exception("Invalid configuration, TenantKeyModulus not set in appsettings.json");
}

Console.WriteLine($"Using tenant key: {tenantKeyModulus}");

var activationPersistenceFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "licenseData.json");
Console.WriteLine($"Using storage file: {activationPersistenceFile}");

if (File.Exists(activationPersistenceFile))
{
    Console.WriteLine("Deleting already persisted activation data...");
    File.Delete(activationPersistenceFile);
}

Console.Write("""

Enter product id
(Make sure that the product/entitlement that you want to use for the offline activation has offline lease period initialized,
otherwise the offline seat activation functionality is considered as disabled): 
""");
var productId = Console.ReadLine();

Console.Write("Enter seat id: ");
var seatId = Console.ReadLine();

//activator instance should be a singleton, reused across the codebase
var activation = new Activation(
    opts =>
    {
        opts.WithTenant(tenantId)
            .WithProduct(productId)
            .WithSeatId(() => seatId);
        opts.UseStorage(new FileActivationStorage(activationPersistenceFile))
            .WithOfflineActivationSupport(
                ofl => ofl.UseTenantPublicKey(new JsonWebKey
                {
                    Alg = "RSA",
                    N = tenantKeyModulus,
                    E = "AQAB"
                })
            )
            .UseStateTransitionCallback(
                (oldState, updatedActivation) =>
                {
                    Console.WriteLine($"Activation changed state from {oldState} to {updatedActivation.State}");
                    return Task.CompletedTask;
                });
    });
Console.WriteLine("Initializing activation...");
await activation.Initialize();

if (activation.State != ActivationState.NotActive)
{
    Console.WriteLine($"Activation in unexpected state: '{activation.State}'. Should be 'NotActive'. " +
                      "Please check if the activation persistence file still holds some data.");
    Console.WriteLine("Program ends.");
    return;
}

Console.WriteLine("Generating offline activation token...");
Console.Write("Enter metadata that you want to return back with the offline activation token response: ");
var metadata = Console.ReadLine();

Console.Write("Enter seat activation code: ");
var activationCode = Console.ReadLine();

Console.Write("Enter seat name: (keep empty for no seat name)");
var seatName = Console.ReadLine();

var offlineActivationToken = await
    activation.GenerateOfflineActivationRequestToken(
        activationCode, string.IsNullOrWhiteSpace(seatName) ? null : seatName, metadata);

Console.WriteLine("Generated offline activation request token:");
Console.WriteLine(offlineActivationToken);
Console.WriteLine("""

Please copy the token provided above and use it in the following API call:
[POST] 'https://{management-api-host}/api/entitlements/activations/offline'

""");

Console.WriteLine("Enter offline activation response token received in the API call response:");
var offlineResponseToken = Console.ReadLine();

Console.WriteLine("Processing received offline activation token...");
var receivedMetadata = await activation.ActivateOffline(offlineResponseToken);

Console.WriteLine("Seat successfully activated");
Console.WriteLine("Activation info:");
Console.WriteLine(activation.Info?.ToString());
Console.WriteLine();
Console.WriteLine($"Received metadata: {receivedMetadata}");

Console.WriteLine();

Console.WriteLine("Do you want to try the offline refresh process, which extends the lease period? [y/n]");
var response = Console.ReadLine();
switch (response)
{
    case "n" or "no":
        Console.WriteLine("Skipping the offline refresh process");
        break;
    case "y" or "yes":
        Console.WriteLine("Follow the instructions below to proceed with the offline refresh process:");
        Console.WriteLine(
            $$"""
            Execute '[PATCH] https://{management-api-host}/api/entitlements/activations/offline' API call with the 
            { "activationId": "{{activation.Info!.Id}}" } body. 
            """);
        Console.WriteLine("Enter offline refresh token received in the API call response:");
        var offlineRefreshToken = Console.ReadLine();
        Console.WriteLine("Processing received offline refresh token...");
        var oldLeaseExpiration = activation.Info.LeaseExpiry;
        await activation.RefreshOffline(offlineRefreshToken);
        Console.WriteLine($"Activation lease period successfully refreshed from {oldLeaseExpiration} to {activation.Info.LeaseExpiry}");
        break;
    default:
        Console.WriteLine("Unexpected answer. Skipping the offline refresh process");
        break;
}

Console.WriteLine("Do you want to proceed with the offline deactivation process? [y/n]");
response = Console.ReadLine();
switch (response)
{
    case "n" or "no":
        Console.WriteLine("Program ends");
        return;
    case "y" or "yes":
        break;
    default:
        Console.WriteLine("Unexpected answer. Program ends");
        return;
}

Console.WriteLine("Generating offline deactivation token...");
var deactivationToken = await activation.DeactivateOffline();
Console.WriteLine("Offline deactivation request token:");
Console.WriteLine(deactivationToken);
Console.WriteLine("""

Please copy the token provided above and use it in the following API call:
[DELETE] 'https://{management-api-host}/api/entitlements/activations/offline'
to finish the offline seat deactivation process.

""");

Console.WriteLine("Activation info:");
Console.WriteLine(activation.Info?.ToString());

Console.WriteLine("Press any key to end the program");
Console.ReadKey();