# .NET Framework 4.8 SDK sample

This Windows console application demonstrates the Zentitle .NET SDK in a .NET
Framework 4.8 project. It generates a device fingerprint for the seat ID, stores
activation data with the native Zentitle2Core secure storage API, reports state
changes, and activates a license with a code.

## Requirements

- 64-bit Windows with the .NET Framework 4.8 runtime.
- Visual Studio or Visual Studio Build Tools with .NET Framework 4.8 development
  tools and its targeting pack.
- The Zentitle NuGet source configured as described in the
  [repository setup instructions](../README.md#configure-the-nuget-feed).
- A Zentitle2 tenant, product, Licensing API URL, and an entitlement with an
  activation code.
- Network access to the Licensing API.
- The Windows x64 `Zentitle2Core.dll` native library.

## Configure

Obtain the native binaries using the
[Zentitle2Core documentation](https://docs.nalpeiron.com/zentitle2-docs/developers/the-zentitle2core-library-c++).
Copy `Zentitle2Core.dll` from the `Windows_x86_64` folder into this project
directory. The project copies it to the build output.

Set the following keys in [App.config](App.config):

| Key | Value |
| --- | --- |
| `LicensingApiUrl` | Your Zentitle2 Licensing API base URL. |
| `TenantId` | Your tenant ID. |
| `ProductId` | The product ID associated with your entitlement. |

The native library is placed beside the executable, following the
[Zentitle2Core library integration guide](https://docs.nalpeiron.com/zentitle2-docs/developers/the-licensing-client-.net/zentitle2core-library#zentitle2core-library-integration).
No custom DLL resolver is needed.

## Build and run

From this directory in a Visual Studio Developer Command Prompt:

```bat
msbuild Net48.Sdk.Sample.csproj -restore -p:RestorePackagesConfig=true -p:Configuration=Debug
cd bin\Debug
Net48.Sdk.Sample.exe
```

The Debug configuration targets x64 to match the native library. Run from the
output directory so the application can find `Zentitle2Core.dll` in its working
directory. You can also build and run the project from Visual Studio.

The application displays the device fingerprint and configuration, then prompts
for an activation code. It saves encrypted activation data in `activation.dat`
in the working directory. On subsequent runs, it deactivates any existing
activation whose state is not `NotActivated` before asking for a new code.
