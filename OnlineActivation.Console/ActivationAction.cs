using Microsoft.Extensions.Hosting;
using Zentitle.Licensing.Client;

namespace OnlineActivation.Console;

public sealed class ActivationAction(string name, Func<Activation, IHost, Task> action)
{
    public string Name { get; } = name;
    public Func<Activation, IHost, Task> Action { get; } = action;
}