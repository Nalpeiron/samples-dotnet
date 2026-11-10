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
            LicensingApiException<ApiError> apiError => FormatApiError(apiError),
            LicensingApiException apiError => $"Licensing API returned HTTP {apiError.StatusCode}.",
            HttpRequestException => "Could not reach the server. Check your connection and try again.",
            OperationCanceledException =>
                "The request timed out. If you were activating a license, check its activation state before retrying.",
            _ => "The operation failed."
        };
        DisplayHelper.WriteError(message);
    }

    private static string FormatApiError(LicensingApiException<ApiError> exception)
    {
        var error = exception.Result;
        var lines = new List<string>();
        var details = string.IsNullOrWhiteSpace(error?.Details) ? error?.Error : error.Details;
        if (!string.IsNullOrWhiteSpace(error?.ErrorCode) || !string.IsNullOrWhiteSpace(details))
        {
            var summary = "Licensing API error";
            if (!string.IsNullOrWhiteSpace(error?.ErrorCode))
                summary += $" ({error.ErrorCode})";
            if (!string.IsNullOrWhiteSpace(details))
                summary += $": {details}";
            lines.Add(summary);
        }

        if (error?.ValidationErrors is { } validationErrors)
        {
            foreach (var validationError in validationErrors)
            {
                var line = string.Join(": ", new[] { validationError?.PropertyName, validationError?.Message }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
                if (!string.IsNullOrWhiteSpace(validationError?.Code))
                    line += line.Length == 0 ? validationError.Code : $" ({validationError.Code})";
                if (line.Length > 0)
                    lines.Add(line);
            }
        }

        return lines.Count > 0
            ? string.Join(Environment.NewLine, lines)
            : $"Licensing API returned HTTP {exception.StatusCode}.";
    }
}
