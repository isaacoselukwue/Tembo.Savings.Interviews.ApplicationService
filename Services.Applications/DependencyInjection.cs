using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Applications.Adapters;
using Services.Applications.Configuration;
using Services.Applications.Infrastructure;
using Services.Applications.Persistence;
using Services.Common.Abstractions.Abstractions;

namespace Services.Applications;
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IApplicationProcessor, ApplicationProcessor>();

        services.AddSingleton<IProductConfigProvider, ProductConfigProvider>();

        services.Configure<ValidationMessagesConfig>(configuration.GetSection("ValidationMessages"));

        services.AddScoped<ICurrencyConverter, CurrencyConverter>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();

        services.AddScoped<AdministratorOneAdapter>();
        services.AddScoped<AdministratorTwoAdapter>();

        services.AddScoped<IReadOnlyDictionary<ProductCode, IAdministrationServiceAdapter>>(provider =>
            new Dictionary<ProductCode, IAdministrationServiceAdapter>
            {
                [ProductCode.ProductOne] = provider.GetRequiredService<AdministratorOneAdapter>(),
                [ProductCode.ProductTwo] = provider.GetRequiredService<AdministratorTwoAdapter>()
            });

        return services;
    }
}
