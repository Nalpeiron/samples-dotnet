using FluentValidation;

namespace Activation.Console.Options;

public sealed class AccountBasedLicensingOptions
{
    public const string SectionName = "AccountBasedLicensing";
    
    public bool Enabled { get; set; }

    public string Authority { get; set; } = default!;

    public string RedirectUrl { get; set; } = default!;

    public string ClientId { get; set; } = default!;

    public string ClientSecret { get; set; } = default!;
}

public class AccountBasedLicensingOptionsValidator : AbstractValidator<AccountBasedLicensingOptions>
{
    public AccountBasedLicensingOptionsValidator()
    {
        When(x => x.Enabled, () =>
        {
            RuleFor(x => x.Authority)
                .NotEmpty();
            
            RuleFor(x => x.Authority)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                    .When(x => !string.IsNullOrEmpty(x.Authority))
                    .WithMessage("{PropertyName} must be a valid URL");

            RuleFor(x => x.RedirectUrl)
                .NotEmpty();
            
            RuleFor(x => x.RedirectUrl)
                .NotEmpty()
                    .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                    .When(x => !string.IsNullOrEmpty(x.RedirectUrl))
                    .WithMessage("{PropertyName} must be a valid URL");
        
            RuleFor(x => x.ClientId)
                .NotEmpty();
        
            RuleFor(x => x.ClientSecret)
                .NotEmpty();
        });
    }
}