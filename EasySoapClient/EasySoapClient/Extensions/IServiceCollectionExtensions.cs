using EasySoapClient.Delegates;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using EasySoapClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace EasySoapClient.Extensions;

public static class IServiceCollectionExtensions
{
    private static IServiceCollection AddMaybeKeyedServiceResolvers(this IServiceCollection services)
    {
        // Register a resolver for IRequestSenderService.
        services.AddTransient<MaybeKeyedServiceResolver<IRequestSenderService>>(provider => (string? serviceKey) =>
        {
            return String.IsNullOrEmpty(serviceKey)
                ? provider.GetRequiredKeyedService<IRequestSenderService>(String.Empty)
                : provider.GetRequiredKeyedService<IRequestSenderService>(serviceKey);
        });

        // Register a resolver for ICredentialsProvider.
        services.AddTransient<MaybeKeyedServiceResolver<ICredentialsProvider>>(provider => (string? serviceKey) =>
        {
            return String.IsNullOrEmpty(serviceKey)
                ? provider.GetRequiredService<ICredentialsProvider>()
                : provider.GetRequiredKeyedService<ICredentialsProvider>(serviceKey);
        });

        // Register a resolver for IEasySoapService.
        services.AddTransient<MaybeKeyedServiceResolver<IEasySoapService>>(provider => (string? serviceKey) =>
        {
            return String.IsNullOrEmpty(serviceKey)
                ? provider.GetRequiredService<IEasySoapService>()
                : provider.GetRequiredKeyedService<IEasySoapService>(serviceKey);
        });

        return services;
    }

    private static IServiceCollection AddCustomHttpClient(this IServiceCollection services, string key, Action<IHttpClientBuilder>? configureHttpClientBuilder)
    {
        var httpClientBuilder = services.AddHttpClient<IRequestSenderService, RequestSenderService>(key, (provider, client) =>
        {
            // Get the options.
            var options = provider.GetRequiredService<IOptionsMonitor<EasySoapClientOptions>>().Get(key);

            // Generate credentials.
            var maybeCredentialsResolver = provider.GetRequiredService<MaybeKeyedServiceResolver<ICredentialsProvider>>();
            var credentialsProvider = maybeCredentialsResolver(key);

            // Set values.
            client.BaseAddress = new Uri(options.BaseUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentialsProvider.GenerateBase64Credentials());
        });

        configureHttpClientBuilder?.Invoke(httpClientBuilder);

        services.AddKeyedTransient<IRequestSenderService>(key, (provider, _) =>
        {
            var logger = provider.GetRequiredService<ILogger<RequestSenderService>>();
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient(key);

            return new RequestSenderService(logger, client);
        });

        return services;
    }

    public static IServiceCollection AddEasySoapClient(this IServiceCollection services, Action<EasySoapClientOptions> configureOptions, Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
    {
        services.Configure(configureOptions);

        services.AddCustomHttpClient(String.Empty, configureHttpClientBuilder);

        services.AddMaybeKeyedServiceResolvers();

        services.AddTransient<ICredentialsProvider, CredentialsService>();
        services.AddTransient<IEasySoapService, EasySoapService>();
        services.AddTransient<ISoapEnvelopeService, SoapEnvelopeService>();
        services.AddTransient<IParsingService, ParsingService>();

        return services;
    }

    public static IServiceCollection AddKeyedEasySoapClient(this IServiceCollection services, string key, Action<EasySoapClientOptions> configureOptions, Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
    {
        services.Configure(key, configureOptions);

        services.AddCustomHttpClient(key, configureHttpClientBuilder);

        // Register the keyed implementations.
        services.AddKeyedTransient<ICredentialsProvider, CredentialsService>(key);
        services.AddKeyedTransient<IEasySoapService, EasySoapService>(key);

        services.AddMaybeKeyedServiceResolvers();

        // These registrations are independent of the key.
        services.AddTransient<ISoapEnvelopeService, SoapEnvelopeService>();
        services.AddTransient<IParsingService, ParsingService>();

        return services;
    }
}
