using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using EasySoapClient.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EasySoapClient.Extensions;

public static class IServiceCollectionExtensions
{
    public static void AddEasySoapClient(this IServiceCollection services, Action<EasySoapClientOptions> configureOptions)
    {
        services.AddHttpClient();

        services.Configure(configureOptions);

        services.AddTransient<ICredentialsProvider, CredentialsService>();
        services.AddTransient<IEasySoapService, EasySoapService>();
        services.AddTransient<ISoapEnvelopeService, SoapEnvelopeService>();


        //services.AddTransient<IRepositoryFactory, RepositoryFactory>();
    }
}
