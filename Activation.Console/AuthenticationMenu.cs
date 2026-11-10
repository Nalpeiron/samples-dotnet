using Microsoft.Extensions.Hosting;
using Sharprompt;
using Zentitle.Licensing.Client.ActivationCredentials;
using Zentitle.Licensing.Client.Api;
using Output = System.Console;

namespace Activation.Console;

public sealed class AuthenticationMenu(IActivationCredentialsClient client)
{
    public async Task<ActivationCredentialsSession?> Run(IHost host, DiscoveryInteraction interaction)
    {
        Output.WriteLine("Authentication is required to discover available entitlements. Choose an authentication method or return to the main menu.");
        while (!interaction.IsCancellationRequested)
        {
            var method = Prompt.Select("Authentication required", [
                "Activation code", "Username and password", "OpenID token",
                "Discover authentication method for a user", DiscoveryInteraction.BackToMainMenu
            ], defaultValue: "Activation code");
            ActivationCredentialsSession? session;
            switch (method)
            {
                case "Activation code":
                    var productId = interaction.Input("Enter product ID for this activation code");
                    var code = interaction.Input("Enter activation code", secret: true);
                    session = await interaction.TryRequest(ct => client.AuthenticateWithActivationCode(code, productId, ct));
                    break;
                case "Username and password":
                    var username = interaction.Input("Enter username");
                    var password = interaction.Input("Enter password", secret: true);
                    session = await interaction.TryRequest(ct => client.AuthenticateWithPassword(username, password, ct));
                    break;
                case "OpenID token":
                    var token = await interaction.TryRequest(ct => UserAuthenticator.AuthenticateAndReturnIdentityToken(host, ct));
                    if (token == null)
                    {
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        DisplayHelper.WriteError("An external provider OpenID token is required.");
                        continue;
                    }
                    session = await interaction.TryRequest(ct => client.AuthenticateWithOpenIdToken(token, ct));
                    break;
                case "Discover authentication method for a user":
                    var email = interaction.Input("Enter user email");
                    var result = await interaction.TryRequest(ct => client.DiscoverAuthentication(email, ct));
                    if (result != null)
                    {
                        Output.WriteLine($"Authentication route: {result.Status}");
                        if (result.Status == CustomerAuthenticationDiscoveryStatus.IdP)
                        {
                            Output.WriteLine($"Authorization endpoint: {result.IdentityProvider.AuthorizationEndpoint}");
                            Output.WriteLine($"Client ID: {result.IdentityProvider.ClientId}");
                        }
                    }
                    continue;
                default:
                    return null;
            }
            if (session == null)
            {
                continue;
            }
            DisplayHelper.WriteSuccess($"Authenticated. Credentials session expires at {session.ExpiresAt:u}.");
            return session;
        }
        return null;
    }
}
