# Zentitle .NET samples

This repository contains C# examples for integrating Zentitle2 licensing and
webhooks into .NET applications. The licensing samples use the Zentitle .NET SDK,
`Zentitle.Licensing.Client` 3.5.0.

## Samples

| Project | Framework | What it demonstrates |
| --- | --- | --- |
| [Activation.Console](Activation.Console/README.md) | .NET 10 | Pre-authentication, entitlement discovery and product/edition selection, license activation, lease renewal, feature usage, and deactivation, with optional native support for offline activation and secure storage. |
| [SharedActivation.Console](SharedActivation.Console/README.md) | .NET 10 | Sharing activation storage between processes and coordinating licensing operations with a system lock. |
| [Net48.Sdk.Sample](Net48.Sdk.Sample/README.md) | .NET Framework 4.8 | Using the licensing SDK with the native Zentitle2Core library in a .NET Framework application. |
| [Webhooks.Console](Webhooks.Console/README.md) | .NET 10 | Receiving Zentitle2 webhook events and verifying their signatures. |

## Configure the NuGet feed

Before restoring the licensing samples, add the Zentitle GitLab Package Registry
as a NuGet source using the official
[Configure NuGet Source guide](https://docs.nalpeiron.com/zentitle2-docs/developers/the-licensing-client-.net/configure-nuget-source).
The guide covers setup with the .NET CLI, NuGet CLI, and Visual Studio.

Create package registry credentials in Zentitle2 under **Administration > API
Credentials > Package Registry**, then use them when configuring the source as
described in the guide.

## Run the samples

Choose a sample from the table above and follow its project's README for
prerequisites, configuration, and instructions to build and run it.
