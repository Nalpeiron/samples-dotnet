using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Activation.Console.Options;

/// <summary>
/// https://andrewlock.net/adding-validation-to-strongly-typed-configuration-objects-using-flentvalidation/
/// </summary>
public static class FluentValidationExtensions
{
    public static void ConfigureWithValidator<TOptions, TValidator>(
        this IServiceCollection services, HostBuilderContext context, string sectionName) 
        where TOptions : class
        where TValidator : class, IValidator<TOptions>
    {
        services.AddScoped<IValidator<TOptions>, TValidator>();
        services.AddOptions<TOptions>()
            .Bind(context.Configuration.GetSection(sectionName))
            .ValidateFluentValidation()
            .ValidateOnStart();
    }

    private static OptionsBuilder<TOptions> ValidateFluentValidation<TOptions>(
        this OptionsBuilder<TOptions> optionsBuilder) where TOptions : class
    {
        optionsBuilder.Services.AddSingleton<IValidateOptions<TOptions>>(
            provider => new FluentValidationOptions<TOptions>(
                optionsBuilder.Name, provider));
        return optionsBuilder;
    }
}

public sealed class FluentValidationOptions<TOptions> 
    : IValidateOptions<TOptions> where TOptions : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string? _name;
    public FluentValidationOptions(string? name, IServiceProvider serviceProvider)
    {
        // we need the service provider to create a scope later
        _serviceProvider = serviceProvider; 
        _name = name; // Handle named options
    }

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // Null name is used to configure all named options.
        if (_name != null && _name != name)
        {
            // Ignored if not validating this instance.
            return ValidateOptionsResult.Skip;
        }

        // Ensure options are provided to validate against
        ArgumentNullException.ThrowIfNull(options);
        
        // Validators are typically registered as scoped,
        // so we need to create a scope to be safe, as this
        // method is be called from the root scope
        using IServiceScope scope = _serviceProvider.CreateScope();

        // retrieve an instance of the validator
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<TOptions>>();

        // Run the validation
        var results = validator.Validate(options);
        if (results.IsValid)
        {
            // All good!
            return ValidateOptionsResult.Success;
        }

        // Validation failed, so build the error message
        var typeName = options.GetType().Name;
        var errors = new List<string>();
        foreach (var result in results.Errors)
        {
            errors.Add($"'{typeName}.{result.PropertyName}': '{result.ErrorMessage}'.");
        }

        return ValidateOptionsResult.Fail(errors);
    }
}