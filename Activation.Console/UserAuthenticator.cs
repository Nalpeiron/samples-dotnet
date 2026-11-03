using System.Diagnostics;
using System.Net;
using IdentityModel.OidcClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Activation.Console.Options;
using Sharprompt;

namespace Activation.Console;

public static class UserAuthenticator
{
    public static async Task<string?> AuthenticateAndReturnIdentityToken(IHost host, CancellationToken cancellationToken = default)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            cancellationToken = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var source = Prompt.Select("Obtain external OpenID token", [
            "Sign in with configured identity provider", "Paste external provider token", "Return to previous menu"
        ], defaultValue: "Sign in with configured identity provider");
        cancellationToken.ThrowIfCancellationRequested();

        switch (source)
        {
            case "Sign in with configured identity provider":
                return await AuthenticateWithIdentityProvider(host, cancellationToken);
            case "Paste external provider token":
                var token = Prompt.Password("Enter external provider OpenID token");
                cancellationToken.ThrowIfCancellationRequested();
                return token;
            default:
                return null;
        }
    }

    private static async Task<string?> AuthenticateWithIdentityProvider(IHost host, CancellationToken cancellationToken)
    {
        var ablOptions = host.Services.GetRequiredService<IOptions<AccountBasedLicensingOptions>>().Value;

        if (!ablOptions.Enabled)
        {
            DisplayHelper.WriteError("Account based licensing is not enabled in the configuration.");
            return null;
        }
        
        // Create a local web server used to receive the authorization response.
        using var listener = new HttpListener();
        listener.Prefixes.Add(ablOptions.RedirectUrl);
        listener.Start();

        var client = new OidcClient(new OidcClientOptions
        {
            Authority = ablOptions.Authority,
            ClientId = ablOptions.ClientId,
            ClientSecret = ablOptions.ClientSecret,
            LoadProfile = false,
            RedirectUri = ablOptions.RedirectUrl,
            Scope = "openid profile"
        });
        
        var state = await client.PrepareLoginAsync(cancellationToken: cancellationToken);

        // Launch the system browser to initiate the authentication 
        Process.Start(new ProcessStartInfo
        {
            FileName = state.StartUrl,
            UseShellExecute = true
        });

        // Wait for an authorization response to be posted to the local server.
        try
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            context.Response.ContentType = "text/plain";
            context.Response.StatusCode = 200;
            var buffer = "Login completed. Please return to the Activation console app."u8.ToArray();
            await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
            await context.Response.OutputStream.FlushAsync(cancellationToken);
            context.Response.Close();

            var result = await client.ProcessResponseAsync(
                context.Request.Url?.Query, state, cancellationToken: cancellationToken);

            if (result.IsError)
            {
                DisplayHelper.WriteError($"Authentication error: {result.Error}");
                return null;
            }

            if (result.IdentityToken == null)
            {
                DisplayHelper.WriteError("No identity token received in the IDP response");
                return null;
            }

            return result.IdentityToken;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            DisplayHelper.WriteError("User authentication was cancelled");
            return null;
        }
    }
}
