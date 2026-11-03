# Shared activation console sample

This interactive .NET 10 console application demonstrates the Zentitle .NET SDK's
`SharedActivation` API for processes that share one activation. It stores the
activation in a common file and uses a system lock to coordinate operations.
The menu includes online activation, lease renewal, feature operations,
deactivation, and an action that holds the lock so you can observe another
instance waiting.

## Requirements

- The .NET 10 SDK and runtime.
- The Zentitle NuGet source configured as described in the
  [repository setup instructions](../README.md#configure-the-nuget-feed).
- A Zentitle2 tenant, product, Licensing API URL, and an entitlement with an
  activation code.
- Network access to the Licensing API for online operations.
- For the sharing demonstration, two terminals running as the same operating
  system user on the same machine.

This sample uses plain-text file storage and online activation. It does not
require the native Zentitle2Core library.

## Configure

Set the following values in [appsettings.json](appsettings.json):

| Setting | Value |
| --- | --- |
| `Licensing.ApiUrl` | Your Zentitle2 Licensing API base URL. |
| `Licensing.TenantId` | Your tenant ID. |
| `Licensing.ProductId` | The product ID associated with your entitlement. |

Instances share `license.json` in `Z2_SharedActivation_Console` under the current
user's local application data directory. The sample prints the full path at
startup. Run instances with matching configuration and use the same seat ID.

## Run

From this directory:

```shell
dotnet run
```

Enter a seat ID, choose **Activate license with code**, and enter your activation
code. Feature operations require the corresponding features on your entitlement.

To observe sharing and locking:

1. Activate a license in the first instance.
2. Start the same command in a second terminal. When asked whether to delete
   existing activation data, answer **No** so both instances keep the shared
   activation.
3. Choose **Acquire lock** in one instance and leave it waiting for a keypress.
4. Attempt a licensing operation in the other instance. It waits for the lock.
5. Press a key in the first instance to release the lock and let the other
   instance continue.

Operations run through `ExecuteWithLock`, which coordinates access to the shared
activation state. If the activation state changes while an instance is waiting
for your menu selection, the sample aborts that selection and refreshes the menu.
