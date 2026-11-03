# Webhooks console sample

This .NET 10 ASP.NET Core application receives Zentitle2 webhook requests at
`POST /` and prints their JSON payloads. It also demonstrates verifying the
`N-Signature` request header with your tenant's public RSA key and printing the
verification result.

## Requirements

- The .NET 10 SDK and ASP.NET Core runtime.
- A Zentitle2 tenant and access to its webhook configuration.
- A public URL that forwards requests to `http://localhost:5003`, so Zentitle2 can
  reach the sample running on your machine. Use your preferred tunnel service.
- Your tenant's public RSA key modulus if you enable signature verification.

This project restores its dependencies from NuGet.org and does not use the
Zentitle SDK package or the native Zentitle2Core library.

## Configure

Set the following values in [appsettings.json](appsettings.json):

| Setting | Value |
| --- | --- |
| `Webhooks.ValidateSignature` | Set `true` to verify incoming signatures, or leave `false` to inspect payloads without verification. |
| `Webhooks.PublicKeyModulus` | Your tenant's public RSA key modulus, required when verification is enabled. |

The sample prints whether a signature is valid. It does not reject a request
based on that result.

## Run

From this directory:

```shell
dotnet run
```

The application listens on `http://localhost:5003`. Configure your tunnel to
forward its public HTTPS URL to this address and keep both processes running.

## Configure and test the webhook

1. Open the webhook settings in Zentitle2 and add a webhook.
2. Enter your tunnel's public URL, using the root path `/`.
3. Select the events you want to receive and save the webhook.
4. Use the webhook's **Test** action and check the console for the received
   request, signature verification result if enabled, and JSON payload.
5. Trigger a selected event, such as creating an entitlement or activating a
   seat, and inspect its payload in the console.

For a local check without a Zentitle2 event or tunnel, leave signature
verification disabled and send a request from another terminal:

```shell
curl -X POST http://localhost:5003/ -H "Content-Type: application/json" -d '{"sample":true}'
```

