using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Zentitle.Licensing.Client;

namespace Net48.Sdk.Sample
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            if (!File.Exists("Zentitle2Core.dll"))
            {
                await DisplayError("Core library not found. Follow the instructions in the README file please.");
                return;
            }
            
            Console.WriteLine("Generating a unique device fingerprint for this machine using the Zentitle2Core C++ library...");
            var deviceFingerprint = Zentitle2Core.DeviceFingerprint.GenerateForCurrentMachine();
            Console.Write($"Device fingerprint: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(deviceFingerprint);
            Console.ResetColor();
            
            var tenantId = ConfigurationManager.AppSettings["TenantId"];
            var productId = ConfigurationManager.AppSettings["ProductId"];
            var apiUrl = ConfigurationManager.AppSettings["LicensingApiUrl"];

            Console.WriteLine();
            Console.WriteLine("Running Zentitle SDK sample with following configuration:");
            Console.WriteLine("---------------------------------------------------------");
            Console.WriteLine($"Tenant Id: {tenantId}");
            Console.WriteLine($"Product Id: {productId}");
            Console.WriteLine($"Licensing Api Url: {apiUrl}");
            Console.WriteLine("---------------------------------------------------------");
            Console.WriteLine();

            if (string.IsNullOrWhiteSpace(tenantId) || 
                string.IsNullOrWhiteSpace(productId) ||
                string.IsNullOrWhiteSpace(apiUrl))
            {
                await DisplayError(
                    "Some configuration values are missing, please provide valid configuration in the App.config file.");
                return;
            }

            var activation = new Activation(
                opts =>
                {
                    opts.WithTenant(tenantId)
                        .WithProduct(productId)
                        .WithSeatId(() => deviceFingerprint)
                        .WithOnlineActivationSupport(onl => onl
                            .UseLicensingApi(new Uri(apiUrl))
                            .UseHttpClientFactory(() => new HttpClient()))
                        .UseStorage(Zentitle2Core.SecureActivationStorage.WithFile(Path.Combine(Directory.GetCurrentDirectory(), "activation.dat")))
                        .UseStateTransitionCallback(
                            (oldState, updatedActivation) =>
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine(
                                    $"Activation state changed from [{oldState}] to [{updatedActivation.State}]");
                                Console.ResetColor();
                                return Task.CompletedTask;
                            });
                });

            await activation.Initialize();

            if(activation.State != ActivationState.NotActivated)
            {
                Console.WriteLine("License already active, deactivating...");
                await activation.Deactivate();
            }

            Console.WriteLine("Enter the activation code:");
            var activationCode = Console.ReadLine();

            Console.WriteLine("Activating license...");
            await activation.ActivateWithCode(activationCode);
            WaitForExit();
        }

        private static async Task DisplayError(string errorMessage)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            await Console.Error.WriteLineAsync(errorMessage);
            Console.ResetColor();
            WaitForExit();
        }

        private static void WaitForExit()
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}