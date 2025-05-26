using System.Runtime.InteropServices;
using Activation.Console;
using Activation.Console.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        services.ConfigureWithValidator<AccountBasedLicensingOptions, AccountBasedLicensingOptionsValidator>(
            hostBuilder, AccountBasedLicensingOptions.SectionName);
    })
    .ConfigureLogging((hostingContext, logging) => {
        logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
        logging.AddConsole();
    })
    .UseConsoleLifetime();

var host = builder.Build();
await host.StartAsync();

var config = host.Services.GetRequiredService<IConfiguration>();

var useCoreLibrary = config.GetValue<bool>("UseCoreLibrary");
if (useCoreLibrary)
{
    DisplayHelper.WriteWarning(
        "- Using Zentitle2Core C++ library for device fingerprint, secure license storage and offline activation operations");
    NativeLibrary.SetDllImportResolver(typeof(IActivation).Assembly, Zentitle2CoreLibResolver.DllImportResolver);
}
else
{
    DisplayHelper.WriteWarning(
        "- Zentitle2Core C++ library usage is disabled in 'appsettings.json', it won't be loaded and its features won't be available.");
}

var licensingOptions = host.Services.GetRequiredService<IOptions<LicensingOptions>>().Value;
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

var licenseStorage = await LicenseStorage.Initialize(useCoreLibrary);

//activation instance should be a singleton, reused across the codebase
var activation = new Zentitle.Licensing.Client.Activation(
    opts =>
    {
        opts.WithTenant(licensingOptions.TenantId)
            .WithProduct(licensingOptions.ProductId)
            .WithSeatId(() =>
            {
                if (useCoreLibrary && Prompt.Confirm("Use device fingerprint for seat ID generation?"))
                {
                    Console.WriteLine("Generating device fingerprint...");
                    return Zentitle2Core.DeviceFingerprint.GenerateForCurrentMachine();
                }

                return Prompt.Input<string>("Enter license seat ID");
            });

        opts.WithOnlineActivationSupport(onl => onl
                .UseLicensingApi(new Uri(licensingOptions.ApiUrl))
                .UseHttpClientFactory(() => httpClientFactory.CreateClient()));

        if (useCoreLibrary)
        {
            opts.WithOfflineActivationSupport(
                ofl => ofl.UseTenantRsaKeyModulus(licensingOptions.TenantRsaKeyModulus));
        }

        opts.UseStorage(licenseStorage)
            .UseStateTransitionCallback(
                (oldState, updatedActivation) =>
                {
                    DisplayHelper.WriteSuccess($"Activation state changed from [{oldState}] to [{updatedActivation.State}]");
                    return Task.CompletedTask;
                })
            // .ConfigureTestServices(services =>
            // {
            //     services.LicensingApiClientFactory =
            //         (options, httpClient) => new ExpiringActivationLicensingApiClient(options, httpClient)
            //         {
            //             LeaseExpiry = TimeSpan.FromSeconds(5)
            //         };
            // })
            ;

        opts.UseLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>());
    }
);

Console.WriteLine("Initializing activation...");
await activation.Initialize();
const string quitAction = "Quit";
string? selectedAction;
do
{
    var activationMode = activation.Info.Mode;
    selectedAction = Prompt.Select("What do you want to do?",
        ActivationActions.AvailableActions[activation.State]
            .Where(action => action.AvailableInModes.Contains(activationMode))
            .Select(x => x.Name).Append(quitAction));

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