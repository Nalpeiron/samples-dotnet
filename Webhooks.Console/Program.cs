using System.Text.Json;
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
    byte[] payload;
    using (var memoryStream = new MemoryStream())
    {
        await request.Body.CopyToAsync(memoryStream);
        payload = memoryStream.ToArray();
    }
    
    Console.WriteLine("Received webhook request");
  
    if (rsaKey is not null)
    {
        var signature = request.Headers["N-Signature"].Single();
        Console.WriteLine($"Validating webhook signature {signature}");
        Console.WriteLine(rsaKey.VerifySignature(payload, signature!)
            ? "Signature is valid"
            : "Signature is not valid");
    }

    var jsonPayload = JsonSerializer.Deserialize<dynamic>(payload);
    Console.WriteLine("Webhook payload:");
    Console.WriteLine(jsonPayload);
});

app.Run("http://localhost:5003");