using Microsoft.Extensions.Hosting;
using Zentitle.Licensing.Client;

namespace OnlineActivation.Console;

public sealed class ActivationAction(string name, Func<IActivation, IHost, Task> action)
{
    public string Name { get; } = name;
    public Func<IActivation, IHost, Task> Action { get; } = action;
}