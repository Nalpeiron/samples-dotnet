using Microsoft.Extensions.Configuration;
using Sharprompt;
using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.Persistence.Storage;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json").Build();

Console.WriteLine("Reading the configuration file...");
var tenantId = configuration.GetValue<string>("TenantId");
if (string.IsNullOrWhiteSpace(tenantId))
{
    throw new Exception("Invalid configuration, TenantId not set in appsettings.json");
}
var licensingApiUrl = configuration.GetValue<string>("LicensingApiUrl");
if (string.IsNullOrWhiteSpace(licensingApiUrl))
{
    throw new Exception("Invalid configuration, 'LicensingApiUrl' not set in appsettings.json");
}

Console.WriteLine($"Using tenant: {tenantId}");
var httpClient = new HttpClient();

var storageDirectory = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "persistence"));
Console.WriteLine($"Using '{storageDirectory.FullName}' as the persistent storage");

var activationDirectories = storageDirectory.EnumerateDirectories().ToList();
if (activationDirectories.Any())
{
    foreach (var directory in activationDirectories)
    {
        Console.WriteLine($"Found activation directory: {directory.Name}");
    }

    var deleteExistingFiles =
        Prompt.Confirm("Do you want to delete existing activation directories in the persistent storage?");
    if (deleteExistingFiles)
    {
        Directory.Delete(storageDirectory.FullName, true);
        Directory.CreateDirectory(storageDirectory.FullName);
    }
}
else
{
    Console.WriteLine("No existing activation directories found");
}

var activationsSet = new ActivationsSet(c => c
    .WithTenant(tenantId)
    .WithSeatId(() => "demo-machine-id-6as5d4a65s5")
    .WithOnlineActivationSupport(onl =>
        onl.UseLicensingApi(new Uri(licensingApiUrl))
            .UseHttpClientFactory(() => httpClient))
    .UseStorage(new FileActivationsSetStorage(storageDirectory.FullName))
    .UseStateTransitionCallback(
        (oldState, updatedActivation) =>
        {
            Console.WriteLine(
                $"Activation {updatedActivation.Info.LocalStorageId} changed state from {oldState} to {updatedActivation.State}");
            return Task.CompletedTask;
        }));

await activationsSet.Initialize();
if (activationsSet.Any())
{
    Console.WriteLine("Found existing activations:");
    foreach (var activation in activationsSet)
    {
        Console.WriteLine(activation.Info);
    }
}

string? executeAction;
do
{
    executeAction = Prompt.Select("What do you want to do?", new[]
    {
        "Add activation",
        "List activations",
        "Get activation",
        "Remove activation",
        "Exit"
    });

    switch (executeAction)
    {
        case "Add activation":
            var productId = Prompt.Input<string>("Enter product id for the activation");
            var newActivation = await activationsSet.CreateActivation(productId);
            Console.WriteLine("Activation added to the set");
            var activationCode = Prompt.Input<string>("Enter activation code");
            await newActivation.Activate(activationCode);
            Console.WriteLine($"Activation {newActivation.Info.Id} activated");
            break;
        case "List activations":
            foreach (var activation in activationsSet)
            {
                Console.WriteLine(activation.Info);
            }

            break;
        case "Get activation":
            var activationId = Prompt.Select("Which activation do you want to get?",
                activationsSet.Select(x => x.Info.Id).ToList());
            var activationToGet = activationsSet.First(x => x.Info.Id == activationId);
            Console.WriteLine(activationToGet.Info);
            break;
        case "Remove activation":
            var activationToRemoveId = Prompt.Select("Which activation do you want to remove?",
                activationsSet.Select(x => x.Info.Id).ToList());
            var activationToRemove = activationsSet.FirstOrDefault(x => x.Info.Id == activationToRemoveId);
            await activationsSet.RemoveActivation(activationToRemove);
            Console.WriteLine($"Activation with id '{activationToRemoveId}' removed");
            break;
    }
} while (executeAction != "Exit");