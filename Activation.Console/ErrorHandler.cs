using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zentitle.Licensing.Client.Api;

namespace Activation.Console;

public static class ErrorHandler
{
    public static bool IsRecoverable(Exception error, CancellationToken stopping)
    {
        return error is LicensingApiException or HttpRequestException ||
            error is OperationCanceledException && !stopping.IsCancellationRequested;
    }

    public static void Report(Exception error, IHost host)
    {
        if (error is not LicensingApiException<ApiError>)
        {
            host.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(ErrorHandler).FullName!)
                .LogError(error, "Operation failed.");
        }

        var message = error switch
        {
            LicensingApiException<ApiError> apiError =>
                $"Licensing API error ({apiError.Result.ErrorCode}): {apiError.Result.Details}",
            LicensingApiException apiError => $"Licensing API returned HTTP {apiError.StatusCode}.",
            HttpRequestException => "Could not reach the server. Check your connection and try again.",
            OperationCanceledException =>
                "The request timed out. If you were activating a license, check its activation state before retrying.",
            _ => "The operation failed."
        };
        DisplayHelper.WriteError(message);
    }
}
