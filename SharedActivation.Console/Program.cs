using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedActivation.Console;
using SharedActivation.Console.Options;
using Sharprompt;
using Zentitle.Licensing.Client;

var builder = new HostBuilder()
    .ConfigureAppConfiguration((hostContext, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json");
        cfg.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);

    })
    .ConfigureServices((hostBuilder, services) =>
    {
        services.AddHttpClient();
        services.ConfigureWithValidator
            <LicensingOptions, LicensingOptionsValidator>(hostBuilder, LicensingOptions.SectionName);
    })
    .ConfigureLogging((hostingContext, logging) => {
        logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
        logging.AddConsole();
    })
    .UseConsoleLifetime();

var host = builder.Build();
await host.StartAsync();

var config = host.Services.GetRequiredService<IConfiguration>();

var licensingOptions = host.Services.GetRequiredService<IOptions<LicensingOptions>>().Value;
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

var licenseStorage = await LicenseStorage.Initialize();

var activation = new Zentitle.Licensing.Client.SharedActivation(
    opts =>
    {
        opts.WithTenant(licensingOptions.TenantId)
            .WithProduct(licensingOptions.ProductId)
            .WithSeatId(() =>
            {
                return Prompt.Input<string>("Enter license seat ID");
            });

        opts.WithOnlineActivationSupport(onl => onl
            .UseLicensingApi(new Uri(licensingOptions.ApiUrl))
            .UseHttpClientFactory(() => httpClientFactory.CreateClient()));

        opts.UseStorage(licenseStorage)
            .UseStateTransitionCallback(
                (oldState, updatedActivation) =>
                {
                    DisplayHelper.WriteSuccess($"Activation state changed from [{oldState}] to [{updatedActivation.State}]");
                    return Task.CompletedTask;
                })
            ;

        opts.UseLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>());
    },
    lockingOptions =>
        lockingOptions.UseSystemLock(
            $"Global\\Zentitle.Licensing.Client-{Assembly.GetExecutingAssembly().GetName().Name}"
        )
);


Console.WriteLine("Initializing activation...");
await activation.InitializeWithLock(CancellationToken.None);
const string quitAction = "Quit";
string? selectedAction;
do
{
    var (availableActions, state) = await activation.ExecuteWithLock(a =>
    {
        var activationMode = a.Info.Mode;
        return (AvailableActions: ActivationActions.AvailableActions[a.State]
            .Where(action => action.AvailableInModes.Contains(activationMode))
            .Select(x => x.Name).Append(quitAction), a.State);
    }, CancellationToken.None);

    selectedAction = Prompt.Select("What do you want to do?", availableActions);

    switch (selectedAction)
    {
        case quitAction:
            break;
        default:
            // ExecuteExclusiveAction
            await activation.ExecuteWithLock(async a =>
            {
                if (state != a.State)
                {
                    DisplayHelper.WriteError("Local activation state changed, operation aborted and state refreshed");
                    return;
                }
                var action = ActivationActions.AvailableActions[a.State].First(x => x.Name == selectedAction);
                await action.Action(a, host);
            }, CancellationToken.None);
            break;
    }
} while (selectedAction != quitAction);