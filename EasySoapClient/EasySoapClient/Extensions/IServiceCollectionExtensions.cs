using EasySoapClient.Delegates;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using EasySoapClient.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EasySoapClient.Extensions;

public static class IServiceCollectionExtensions
{
    private static IServiceCollection AddMaybeKeyedServiceResolvers(this IServiceCollection services)
    {
        // Register a resolver for IRequestSenderService.
        services.AddTransient<MaybeKeyedServiceResolver<IRequestSenderService>>(provider => (string? serviceKey) =>
            String.IsNullOrEmpty(serviceKey)
                ? provider.GetRequiredService<IRequestSenderService>()
                : provider.GetRequiredKeyedService<IRequestSenderService>(serviceKey));

        // Register a resolver for ICredentialsProvider.
        services.AddTransient<MaybeKeyedServiceResolver<ICredentialsProvider>>(provider => (string? serviceKey) =>
            String.IsNullOrEmpty(serviceKey)
                ? provider.GetRequiredService<ICredentialsProvider>()
                : provider.GetRequiredKeyedService<ICredentialsProvider>(serviceKey));

        // Register a resolver for IEasySoapService.
        services.AddTransient<MaybeKeyedServiceResolver<IEasySoapService>>(provider => (string? serviceKey) =>
            String.IsNullOrEmpty(serviceKey)
                ? provider.GetRequiredService<IEasySoapService>()
                : provider.GetRequiredKeyedService<IEasySoapService>(serviceKey));

        return services;
    }

    public static IServiceCollection AddEasySoapClient(this IServiceCollection services, Action<EasySoapClientOptions> configureOptions)
    {
        services.AddHttpClient();

        services.Configure(configureOptions);

        services.AddMaybeKeyedServiceResolvers();

        services.AddTransient<ICredentialsProvider, CredentialsService>();
        services.AddTransient<IEasySoapService, EasySoapService>();
        services.AddTransient<IRequestSenderService, RequestSenderService>();
        services.AddTransient<ISoapEnvelopeService, SoapEnvelopeService>();
        services.AddTransient<IParsingService, ParsingService>();

        return services;
    }


    public static IServiceCollection AddKeyedEasySoapClient(this IServiceCollection services, string key, Action<EasySoapClientOptions> configureOptions)
    {
        services.AddHttpClient();

        services.Configure<EasySoapClientOptions>(key, configureOptions);

        // Register the keyed implementations.
        services.AddKeyedTransient<ICredentialsProvider, CredentialsService>(key);
        services.AddKeyedTransient<IEasySoapService, EasySoapService>(key);
        services.AddKeyedTransient<IRequestSenderService, RequestSenderService>(key);

        // These registrations are independent of the key.
        services.AddTransient<ISoapEnvelopeService, SoapEnvelopeService>();
        services.AddTransient<IParsingService, ParsingService>();

        services.AddMaybeKeyedServiceResolvers();

        return services;
    }


}
