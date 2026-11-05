using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Activation.Console;
using Activation.Console.Options;
using Sharprompt;
using System.Runtime.InteropServices;
using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.ActivationCredentials;

if (args.Contains("--help"))
{
    Console.WriteLine("Online activation console. Run from this directory with your appsettings.json configuration.");
    Console.WriteLine("Main menu: Activate configured product, Discover available entitlements, or Quit.");
    Console.WriteLine("If a saved activation initializes to a state other than NotActivated, choose to resume it or activate again.");
    Console.WriteLine("Discovery requires authentication: choose activation code, username/password, OpenID token, discover a user's authentication method, or return to the main menu.");
    Console.WriteLine("Authenticated menu: list entitlements, activate a selected product/edition, or end the session and return to the main menu.");
    return;
}

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

using var host = builder.Build();
await host.StartAsync();
var stopping = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

var config = host.Services.GetRequiredService<IConfiguration>();
var useCoreLibrary = config.GetValue<bool>("UseCoreLibrary");
var licensingOptions = host.Services.GetRequiredService<IOptions<LicensingOptions>>().Value;
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
if (useCoreLibrary)
{
    if (string.IsNullOrWhiteSpace(licensingOptions.TenantRsaKeyModulus))
    {
        throw new ArgumentException("Configure Licensing.TenantRsaKeyModulus before using core-library activation support.");
    }
    NativeLibrary.SetDllImportResolver(typeof(IActivation).Assembly, Zentitle2CoreLibResolver.DllImportResolver);
}
var licenseStorage = LicenseStorage.Create(useCoreLibrary);
LicenseDiscovery? discovery = null;

while (!stopping.IsCancellationRequested)
{
    try
    {
        if (discovery == null)
        {
            var storedData = await licenseStorage.Load();
            if (!string.IsNullOrWhiteSpace(storedData.ActivationState?.ProductId))
            {
                using var savedActivation = await CreateActivation(storedData.ActivationState.ProductId);
                var canResume = await savedActivation.ExecuteWithLock(a => a.State != ActivationState.NotActivated,
                    CancellationToken.None);
                if (canResume)
                {
                    DisplayHelper.WriteWarning("Activate again clears the saved local activation data. It does not deactivate the server-side seat.");
                    var choice = Prompt.Select("Existing activation found", ["Resume existing activation", "Activate again", "Quit"],
                        defaultValue: "Resume existing activation");
                    if (choice == "Quit")
                    {
                        break;
                    }
                    if (choice == "Resume existing activation")
                    {
                        if (await RunActivationMenu(savedActivation))
                        {
                            break;
                        }
                        continue;
                    }
                }
                await licenseStorage.Clear();
            }

            var flow = Prompt.Select("Main menu", ["Activate configured product", "Discover available entitlements", "Quit"],
                defaultValue: "Activate configured product");
            if (flow == "Quit")
            {
                break;
            }
            if (flow == "Activate configured product" && string.IsNullOrWhiteSpace(licensingOptions.ProductId))
            {
                DisplayHelper.WriteError("Configure Licensing.ProductId before activating the configured product, or choose Discover available entitlements to select a product.");
                continue;
            }
            if (flow == "Discover available entitlements")
            {
                discovery = new LicenseDiscovery(new ActivationCredentialsClient(options => options
                    .WithTenant(licensingOptions.TenantId)
                    .UseLicensingApi(new Uri(licensingOptions.ApiUrl))
                    .UseHttpClientFactory(() => httpClientFactory.CreateClient())
                    .UseLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>())));
            }
        }

        var selection = discovery == null ? null : await discovery.Run(host);
        if (discovery != null && selection == null)
        {
            discovery = null;
            continue;
        }
        using var activation = await CreateActivation(selection?.ProductId ?? licensingOptions.ProductId);
        if (discovery != null && selection != null)
        {
            try
            {
                if (!await discovery.ActivateSelectedProduct(selection, activation))
                {
                    continue;
                }
            }
            catch (Exception error) when (ErrorHandler.IsRecoverable(error, stopping))
            {
                ErrorHandler.Report(error, host);
                continue;
            }
        }
        discovery = null;
        if (await RunActivationMenu(activation))
        {
            break;
        }
    }
    catch (OperationCanceledException) when (stopping.IsCancellationRequested)
    {
        break;
    }
}

async Task<SharedActivation> CreateActivation(string productId)
{
    var activation = new SharedActivation(
        opts =>
        {
            opts.WithTenant(licensingOptions.TenantId)
                .WithProduct(productId)
                .WithSeatId(() =>
                {
                    if (useCoreLibrary && Prompt.Confirm("Use device fingerprint for seat ID generation?"))
                    {
                        Console.WriteLine("Generating device fingerprint...");
                        return Zentitle2Core.DeviceFingerprint.GenerateForCurrentMachine();
                    }

                    Console.WriteLine("Your application chooses the seat ID to identify the licensed machine, user, or other entity.");
                    while (true)
                    {
                        var seatId = Prompt.Input<string>("Enter license seat ID");
                        if (!string.IsNullOrWhiteSpace(seatId))
                        {
                            return seatId;
                        }
                        DisplayHelper.WriteError("A license seat ID is required.");
                    }
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
                .ConfigureTestServices(services =>
                {
                    // services.LicensingApiClientFactory =
                    //     (options, httpClient) => new ExpiringActivationLicensingApiClient(options, httpClient)
                    //     {
                    //         LeaseExpiry = TimeSpan.FromSeconds(5)
                    //     };
                });

            opts.UseLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>());
        },
        lockingOptions =>
            lockingOptions.UseSystemLock(
                $"Global\\Zentitle.Licensing.Client-{Assembly.GetExecutingAssembly().GetName().Name}"
                )
    );
    try
    {
        Console.WriteLine($"Initializing activation for product {productId}...");
        await activation.InitializeWithLock(CancellationToken.None);
        await activation.ExecuteWithLock(a =>
        {
            if (a.Info.ProductId != productId)
            {
                throw new InvalidOperationException("The stored product changed while initializing. Restart to load the current activation.");
            }
            return Task.CompletedTask;
        }, CancellationToken.None);
        return activation;
    }
    catch
    {
        activation.Dispose();
        throw;
    }
}

async Task<bool> RunActivationMenu(ISharedActivation activation)
{
    const string back = "Return to main menu";
    const string quit = "Quit";
    while (!stopping.IsCancellationRequested)
    {
        var (availableActions, state, mode) = await activation.ExecuteWithLock(a =>
        {
            var activationMode = a.Info.Mode;
            return (ActivationActions.AvailableActions[a.State]
                .Where(action => action.AvailableInModes.Contains(activationMode)).ToArray(), a.State, activationMode);
        }, CancellationToken.None);
        var selected = Prompt.Select("Activation operations", availableActions.Select(action => action.Name).Concat([back, quit]),
            defaultValue: availableActions[0].Name);
        if (selected == quit)
        {
            return true;
        }
        if (selected == back)
        {
            return false;
        }
        var action = availableActions.First(candidate => candidate.Name == selected);
        try
        {
            await activation.ExecuteWithLock(async a =>
            {
                if (state != a.State || mode != a.Info.Mode)
                {
                    DisplayHelper.WriteError("Local activation state or mode changed, operation aborted and state refreshed");
                    return;
                }
                await action.Action(a, host);
            }, CancellationToken.None);
        }
        catch (Exception error) when (ErrorHandler.IsRecoverable(error, stopping))
        {
            ErrorHandler.Report(error, host);
        }
    }
    return true;
}
