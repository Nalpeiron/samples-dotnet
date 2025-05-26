using FluentValidation;

namespace SharedActivation.Console.Options;

public sealed class LicensingOptions
{
    public const string SectionName = "Licensing";
    
    public string ApiUrl { get; set; } = default!;
    
    public string TenantId { get; set; } = default!;
        
    public string ProductId { get; set; } = default!;
}

public sealed class LicensingOptionsValidator : AbstractValidator<LicensingOptions>
{
    public LicensingOptionsValidator()
    {
        RuleFor(x => x.ApiUrl)
            .NotEmpty()
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .When(x => !string.IsNullOrEmpty(x.ApiUrl))
                .WithMessage("{PropertyName} must be a valid URL");
        
        RuleFor(x => x.TenantId)
            .NotEmpty();
        
        RuleFor(x => x.ProductId)
            .NotEmpty();
    }
}