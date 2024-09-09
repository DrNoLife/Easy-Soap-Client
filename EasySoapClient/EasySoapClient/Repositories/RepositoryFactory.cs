using EasySoapClient.Interfaces;

namespace EasySoapClient.Repositories;

public class RepositoryFactory(IHttpClientFactory httpClientFactory, ISoapEnvelopeService soapEnvelopeService) : IRepositoryFactory
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ISoapEnvelopeService _soapEnvelopeService = soapEnvelopeService;

    public IRepository<T> CreateRepository<T>(Uri webserviceUrl, ICredentialsProvider credentials) 
        where T : IWebServiceElement, new()
    {
        return new Repository<T>(_httpClientFactory, credentials, webserviceUrl, _soapEnvelopeService);
    }
}

