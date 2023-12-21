using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.Api;
using Zentitle.Licensing.Client.Persistence.Storage;
using Zentitle.Licensing.Client.States;

var builder = new HostBuilder()
    .ConfigureAppConfiguration(cfg => cfg.AddJsonFile("appsettings.json"))
    .ConfigureServices((_, services) =>
    {
        services.AddHttpClient();
    }).UseConsoleLifetime();

var host = builder.Build();
var config = host.Services.GetRequiredService<IConfiguration>();
var licensingApiUrl = config.GetValue<string>("LicensingApiUrl");
if (string.IsNullOrWhiteSpace(licensingApiUrl))
{
    throw new Exception("Invalid configuration, 'LicensingApiUrl' not set in appsettings.json");
}
var tenantId = config.GetValue<string>("TenantId");
if (string.IsNullOrWhiteSpace(tenantId))
{
    throw new Exception("Invalid configuration, TenantId not set in appsettings.json");
}

var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

//activator instance should be a singleton, reused across the codebase
var storageFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "licenseData.json");
Console.WriteLine($"Using storage file: {storageFile}");

if (File.Exists(storageFile))
{
    Console.WriteLine("Deleting already persisted activation data...");
    File.Delete(storageFile);
}

Console.Write("Enter product id: ");
var productId = Console.ReadLine();

Console.Write("Enter seat id: ");
var seatId = Console.ReadLine();

var activation = new Activation(
    opts =>
    {
        opts.WithTenant(tenantId)
            .WithProduct(productId)
            .WithSeatId(() => seatId);
        
        opts.WithOnlineActivationSupport(onl => onl
                .UseLicensingApi(new Uri(licensingApiUrl))
                .UseHttpClientFactory(() => httpClientFactory.CreateClient()))
            .UseStorage(new FileActivationStorage(storageFile))
            .UseStateTransitionCallback(
                (oldState, updatedActivation) =>
                {
                    Console.WriteLine($"Activation changed state from {oldState} to {updatedActivation.State}");
                    return Task.CompletedTask;
                });
    }
    //.UseLoggerFactory()
);
Console.WriteLine("Initializing...");
await activation.Initialize();

if (activation.State != ActivationState.Active)
{
    Console.Write("Enter seat activation code: ");
    var activationCode = Console.ReadLine();
    
    
    Console.Write("Enter seat name: (keep empty for no seat name)");
    var seatName = Console.ReadLine();
    
    Console.WriteLine("Activating...");
    await activation.Activate(activationCode, string.IsNullOrWhiteSpace(seatName) ? null : seatName);
}

Console.WriteLine("Activation info:");
Console.WriteLine(activation.Info?.ToString());
Console.WriteLine();
Console.WriteLine("Features:");
Console.WriteLine(activation.Features);
Console.WriteLine();

//consumption token
var consumptionTokenFeature = activation.Features.FirstOrDefault(f => f.Type == FeatureType.Consumption);
if (consumptionTokenFeature is not null)
{
    Console.WriteLine($"Checking out consumption token feature {consumptionTokenFeature.Key}");
    await activation.Features.Checkout(consumptionTokenFeature.Key, 1);
    Console.WriteLine("Checkout successful");

    var feature = activation.Features.Get(consumptionTokenFeature.Key);
    Console.WriteLine($"Consumption token feature state: {feature}");
}
else
{
    Console.Error.WriteLine("There is no consumption token feature");
}

//element pool
var elementPoolFeature = activation.Features.FirstOrDefault(f => f.Type == FeatureType.ElementPool);
if (elementPoolFeature is not null)
{
    Console.WriteLine($"Checking out element pool feature {elementPoolFeature.Key}");
    await activation.Features.Checkout(elementPoolFeature.Key, 1);
    Console.WriteLine("Checkout successful");

    var feature = activation.Features.Get(elementPoolFeature.Key);
    Console.WriteLine($"Element pool feature state: {feature}");

    Console.WriteLine("Returning element pool feature...");
    await activation.Features.Return(elementPoolFeature.Key, 1);
    feature = activation.Features.Get(elementPoolFeature.Key);
    Console.WriteLine($"Element pool feature state: {feature}");
}
else
{
    Console.Error.WriteLine("There is no element pool feature");
}

Console.WriteLine("Pulling activation state from the server...");
await activation.PullRemoteState();

Console.WriteLine("Pulling the entitlement metadata associated with the activation...");
var activationEntitlement = await activation.GetActivationEntitlement();
Console.WriteLine("Activation entitlement:");
Console.WriteLine(JsonSerializer.Serialize(activationEntitlement, new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
}));
Console.WriteLine();

Console.WriteLine("Deactivating...");
await activation.Deactivate();