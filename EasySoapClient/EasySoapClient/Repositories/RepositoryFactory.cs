using EasySoapClient.Interfaces;
using System.Net.Http;

namespace EasySoapClient.Repositories;

public class RepositoryFactory : IRepositoryFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RepositoryFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public IRepository<T> CreateRepository<T>(Uri webserviceUrl, ICredentialsProvider credentials) where T : IWebServiceElement, new()
    {
        return new Repository<T>(_httpClientFactory, credentials, webserviceUrl);
    }
}

