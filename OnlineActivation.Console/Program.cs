using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OnlineActivation.Console;
using OnlineActivation.Console.Options;
using Sharprompt;
using Spectre.Console;
using Zentitle.Licensing.Client;

var builder = new HostBuilder()
    .ConfigureAppConfiguration(cfg => cfg.AddJsonFile("appsettings.json"))
    .ConfigureServices((hostBuilder, services) =>
    {
        services.AddHttpClient();
        services.ConfigureWithValidator
            <LicensingOptions, LicensingOptionsValidator>(hostBuilder, LicensingOptions.SectionName);
        services.ConfigureWithValidator<AccountBasedLicensingOptions, AccountBasedLicensingOptionsValidator>(
            hostBuilder, AccountBasedLicensingOptions.SectionName);
    }).UseConsoleLifetime();

var host = builder.Build();
await host.StartAsync();

var licensingOptions = host.Services.GetRequiredService<IOptions<LicensingOptions>>().Value;
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

//activation instance should be a singleton, reused across the codebase
var activation = new Activation(
    opts =>
    {
        opts.WithTenant(licensingOptions.TenantId)
            .WithProduct(licensingOptions.ProductId)
            // seat id should be unique for the host machine or given user
            .WithSeatId(() => "demo-machine-id-6as5d4a65s5-sdk-039");
        
        opts.WithOnlineActivationSupport(onl => onl
                .UseLicensingApi(new Uri(licensingOptions.ApiUrl))
                .UseHttpClientFactory(() => httpClientFactory.CreateClient()))
            .UseStorage(LicenseStorage.Initialize())
            .UseStateTransitionCallback(
                (oldState, updatedActivation) =>
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Activation state changed from '{oldState}' to '{updatedActivation.State}'");
                    Console.ResetColor();
                    return Task.CompletedTask;
                });
    }
    //.UseLoggerFactory()
);

Console.WriteLine("Initializing...");
await activation.Initialize();
const string quitAction = "Quit";
string? selectedAction;
do
{
    selectedAction = Prompt.Select("What do you want to do?",
        ActivationActions.AvailableActions[activation.State].Select(x => x.Name).Append(quitAction));
    switch (selectedAction)
    {
        case quitAction:
            break;
        default:
            var action = ActivationActions.AvailableActions[activation.State].First(x => x.Name == selectedAction);
            await action.Action(activation, host);
            break;
    }
} while (selectedAction != quitAction);