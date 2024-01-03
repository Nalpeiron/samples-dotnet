using Webhooks.Console;

PublicRsaKey? rsaKey = null;
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json").AddEnvironmentVariables();
builder.Services.AddOptions<WebhooksOptions>()
    .Bind(builder.Configuration.GetSection("Webhooks"))
    .Validate(
        config => !config.ValidateSignature || !string.IsNullOrWhiteSpace(config.PublicKeyModulus), 
        "TenantKeyModulus must be set when VerifySignature is enabled")
    .Validate(
        config => !config.ValidateSignature || PublicRsaKey.TryCreate(config.PublicKeyModulus, out rsaKey),
        "TenantKeyModulus is not valid")
    .ValidateOnStart();

var app = builder.Build();

app.MapPost("/", async (HttpRequest request) =>
{
    string webhookRequestBody;
    using (var stream = new StreamReader(request.Body))
    {
        webhookRequestBody = await stream.ReadToEndAsync();
    }
    
    Console.WriteLine("Received webhook with request body:");
    Console.WriteLine(webhookRequestBody);
    Console.WriteLine();
    
    if (rsaKey is not null)
    {
        var signature = request.Headers["N-Signature"].Single();
        Console.WriteLine($"Validating webhook signature {signature}");
        Console.WriteLine(rsaKey.VerifySignature(webhookRequestBody, signature!)
            ? "Signature is valid"
            : "Signature is not valid");
    }
});

app.Run("http://localhost:5003");