using EasySoapClient.Interfaces;
using EasySoapClient.Repositories;
using EasySoapClient.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EasySoapClient.Extensions;

public static class IServiceCollectionExtensions
{
    public static void AddEasySoapClient(this IServiceCollection services)
    {
        services.AddTransient<IRepositoryFactory, RepositoryFactory>();
        services.AddTransient<ISoapEnvelopeService, SoapEnvelopeService>();
    }
}
