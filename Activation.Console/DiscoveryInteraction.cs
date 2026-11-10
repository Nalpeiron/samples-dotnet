using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sharprompt;

namespace Activation.Console;

// Shares input validation and recoverable API errors between the two menus.
public sealed class DiscoveryInteraction(IHost host)
{
    public const string BackToMainMenu = "Return to main menu";
    private readonly CancellationToken _stopping = host.Services
        .GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

    public bool IsCancellationRequested => _stopping.IsCancellationRequested;

    public string Input(string message, bool secret = false, string? defaultValue = null)
    {
        while (true)
        {
            var value = secret ? Prompt.Password(message) : Prompt.Input<string>(message, defaultValue: defaultValue);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
            DisplayHelper.WriteError("A value is required.");
        }
    }

    public async Task<T?> TryRequest<T>(Func<CancellationToken, Task<T>> action) where T : class?
    {
        _stopping.ThrowIfCancellationRequested();
        try
        {
            var result = await action(_stopping);
            _stopping.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception error) when (ErrorHandler.IsRecoverable(error, _stopping))
        {
            ErrorHandler.Report(error, host);
        }
        return null;
    }
}
