# Activation console sample

This .NET 10 console sample demonstrates direct activation and entitlement
discovery with the Zentitle .NET SDK, plus lease renewal, feature usage, and
deactivation. Native support adds device fingerprinting, secure storage, and
offline activation.

## Requirements

- The .NET 10 SDK.
- The Zentitle NuGet source configured as described in the
  [repository setup instructions](../README.md#configure-the-nuget-feed).
- A Zentitle2 tenant and an accessible Licensing API. Activation requires an
  eligible entitlement; discovery requires the API's authentication and
  entitlement-listing endpoints.
- An activation code and its product ID, username/password credentials, or an
  external provider OpenID token.

## Configure

Set the following values in [appsettings.json](appsettings.json):

| Setting | Value |
| --- | --- |
| `Licensing.ApiUrl` | Your Zentitle2 Licensing API base URL. Copy it from Administration > API Credentials > Licensing API. |
| `Licensing.TenantId` | Your tenant ID. |
| `Licensing.ProductId` | Required for direct activation; optional for entitlement discovery. Activation-code authentication still requires the code's product ID. |
| `Licensing.EditionId` | Optional edition for direct activation; leave empty for server selection. Discovery selects its edition independently. |
| `Licensing.TenantRsaKeyModulus` | Your tenant's public RSA key modulus, required only when `UseCoreLibrary` is `true`. |
| `UseCoreLibrary` | Leave `false` for online activation with plain-text file storage, or set `true` to enable the native library. |

For browser-based OpenID login, set `AccountBasedLicensing.Enabled` to `true` and
configure `Authority`, `ClientId`, `ClientSecret`, and `RedirectUrl`. Register the
redirect URL with your provider; the default is `http://localhost:8739/`.
Browser configuration is unnecessary when pasting a token or using other credentials.

## Run

From this directory:

```shell
dotnet run
```

Follow the interactive prompts. Discovery selects a product and optional edition;
the server chooses the entitlement. Listing entitlements does not reserve a seat.

Your application chooses the seat ID to identify the licensed machine, user, or
other entity. Enter a nonempty value and reuse it for the same entity. See
[Configuring the Activation](https://docs.nalpeiron.com/zentitle2-docs/developers/the-licensing-client-.net/configuring-the-activation).
Empty or whitespace-only input prompts again without restarting the activation flow.

The sample prints its activation storage path at startup. Clearing local data
does not deactivate the server-side seat.

## Enable native and offline operations

Obtain binaries using the
[Zentitle2Core documentation](https://docs.nalpeiron.com/zentitle2-docs/developers/the-zentitle2core-library-c++)
and place the platform folders inside `Zentitle2CoreLibPlaceholder`, for example:

```text
Zentitle2CoreLibPlaceholder/Windows_x86_64/Zentitle2Core.dll
Zentitle2CoreLibPlaceholder/MacOS_arm64/libZentitle2Core.dylib
Zentitle2CoreLibPlaceholder/Linux_x86_64/libZentitle2Core.so
```

Use the folder matching your OS and architecture; supported paths are in
[Zentitle2CoreLibResolver.cs](Zentitle2CoreLibResolver.cs). Set `UseCoreLibrary` to
`true` and provide `Licensing.TenantRsaKeyModulus`.

Offline activation requires an offline lease period on the product or entitlement
and a token exchange through the End User Portal. See the
[integration guide](https://docs.nalpeiron.com/zentitle2-docs/developers/the-licensing-client-.net/zentitle2core-library).
