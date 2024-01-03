namespace Webhooks.Console;

public sealed class WebhooksOptions
{
    public bool ValidateSignature { get; set; }
    public string PublicKeyModulus { get; set; } = default!;
}