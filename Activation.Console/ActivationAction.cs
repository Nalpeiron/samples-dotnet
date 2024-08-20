using Microsoft.Extensions.Hosting;
using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.Api;

namespace Activation.Console;

public sealed class ActivationAction(
    string name, Func<IActivation, IHost, Task> action, ActivationMode?[] availableInModes)
{
    public string Name { get; } = name;
    public Func<IActivation, IHost, Task> Action { get; } = action;
    
    public ActivationMode?[] AvailableInModes { get; } = availableInModes; 
}