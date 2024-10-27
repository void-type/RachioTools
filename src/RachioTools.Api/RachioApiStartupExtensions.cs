using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RachioTools.Api.Configuration;
using System.Net.Http.Headers;

namespace RachioTools.Api;

public static class RachioApiStartupExtensions
{
    public static void AddRachioApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RachioApiSettings>(configuration.GetSection("RachioApi"));
        services.AddSingleton<IValidateOptions<RachioApiSettings>, RachioApiSettingsValidator>();

        services.AddSingleton<RachioApiService>();

        services.AddHttpClient<RachioApiService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RachioApiSettings>>().Value;

            client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
    }
}
