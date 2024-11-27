using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using EasySoapClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EasySoapClient.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddEasySoapClient(this IServiceCollection services, Action<EasySoapClientOptions> configureOptions)
    {
        services.AddHttpClient();

        services.Configure(configureOptions);

        services.AddTransient<ICredentialsProvider, CredentialsService>();
        services.AddTransient<IEasySoapService, EasySoapService>();
        services.AddTransient<ISoapEnvelopeService, SoapEnvelopeService>();

        return services;
    }

    public static IServiceCollection AddKeyedEasySoapClient(this IServiceCollection services, string key, Action<EasySoapClientOptions> configureOptions)
    {
        services.AddHttpClient();

        services.Configure<EasySoapClientOptions>(key, configureOptions);

        services.AddKeyedTransient<ICredentialsProvider, CredentialsService>(key);
        services.AddKeyedTransient<IEasySoapService, EasySoapService>(key);
        services.AddTransient<ISoapEnvelopeService, SoapEnvelopeService>();

        return services;
    }

}
