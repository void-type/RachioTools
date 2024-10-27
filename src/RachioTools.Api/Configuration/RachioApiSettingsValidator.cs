using Microsoft.Extensions.Options;

namespace RachioTools.Api.Configuration;

public class RachioApiSettingsValidator : IValidateOptions<RachioApiSettings>
{
    public ValidateOptionsResult Validate(string? name, RachioApiSettings options)
    {
        if (string.IsNullOrEmpty(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail("BaseUrl must be provided.");
        }

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            return ValidateOptionsResult.Fail("ApiKey must be provided.");
        }

        return ValidateOptionsResult.Success;
    }
}
