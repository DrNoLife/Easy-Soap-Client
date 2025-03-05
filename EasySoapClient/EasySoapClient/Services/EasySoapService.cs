using EasySoapClient.Enums;
using EasySoapClient.Exceptions;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace EasySoapClient.Services;

public class EasySoapService : IEasySoapService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISoapEnvelopeService _soapEnvelopeService;
    private readonly ICredentialsProvider _credentials;
    private readonly Uri _serviceUrl;
    private readonly ILogger<EasySoapService> _logger;

    public EasySoapService(
        IHttpClientFactory httpClientFactory,
        ISoapEnvelopeService soapEnvelopeService,
        ILogger<EasySoapService> logger,
        IOptionsMonitor<EasySoapClientOptions> optionsMonitor,
        IServiceProvider serviceProvider,
        [ServiceKey] string? serviceKey = null)
    {
        _httpClientFactory = httpClientFactory;
        _soapEnvelopeService = soapEnvelopeService;
        _logger = logger;

        var options = serviceKey is not null
            ? optionsMonitor.Get(serviceKey) // Keyed configuration.
            : optionsMonitor.CurrentValue;   // Non-keyed (default) configuration.

        _serviceUrl = new Uri(options.BaseUri);

        // Resolve the correct credentials provider.
        _credentials = serviceKey is not null
            ? serviceProvider.GetRequiredKeyedService<ICredentialsProvider>(serviceKey) 
            : serviceProvider.GetRequiredService<ICredentialsProvider>();              
    }

    private string Credentials => _credentials.GenerateBase64Credentials();

    public async Task<List<T>> GetAsync<T>(IEnumerable<ReadMultipleFilter>? filters = null, int size = 10, string? bookmarkKey = null, CancellationToken cancellationToken = default) where T : IWebServiceElement, new()
    {
        if (filters is null || !filters.Any())
        {
            ReadMultipleFilter emptyReadAllFilter = new("", "");
            filters = [emptyReadAllFilter];
        }

        var instance = new T();
        string serviceUrl = $"{_serviceUrl}/Page/{instance.ServiceName}";
        string soapMessage = _soapEnvelopeService.CreateReadMultipleEnvelope(filters, size, null, instance);
        string soapResponse = await SendSoapRequestAsync(CallMethod.ReadMultiple, soapMessage, instance, serviceUrl, cancellationToken);

        return ParseSoapResponseList<T>(soapResponse, instance);
    }

    public async Task<List<T>> GetAsync<T>(ReadMultipleFilter filter, int size = 10, string? bookmarkKey = null, CancellationToken cancellationToken = default) where T : IWebServiceElement, new()
        => await GetAsync<T>(
            filters: [filter],
            size: size,
            bookmarkKey: bookmarkKey,
            cancellationToken: cancellationToken);

    public async Task<T> CreateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IWebServiceElement, new()
    {
        var instance = item;
        string serviceUrl = $"{_serviceUrl}/Page/{instance.ServiceName}";
        string soapMessage = _soapEnvelopeService.CreateCreateEnvelope(item);
        string soapResponse = await SendSoapRequestAsync(CallMethod.Create, soapMessage, instance, serviceUrl, cancellationToken);

        return ParseSoapResponseSingle<T>(soapResponse, instance);
    }

    public async Task<T> UpdateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IUpdatableWebServiceElement, new()
    {
        var instance = item;
        string serviceUrl = $"{_serviceUrl}/Page/{instance.ServiceName}";
        string soapMessage = _soapEnvelopeService.CreateUpdateEnvelope(item);
        string soapResponse = await SendSoapRequestAsync(CallMethod.Update, soapMessage, instance, serviceUrl, cancellationToken);

        return ParseSoapResponseSingle<T>(soapResponse, instance);
    }

    private static string GetSoapAction(CallMethod methodToCall, IWebServiceElement instance)
        => $"{instance.Namespace}/{methodToCall}";

    private async Task<string> SendSoapRequestAsync(CallMethod soapMethod, string soapEnvelope, IWebServiceElement instance, string serviceUrl, CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

        content.Headers.Add("SOAPAction", GetSoapAction(soapMethod, instance));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Credentials);

        HttpResponseMessage response = await httpClient.PostAsync(serviceUrl, content, cancellationToken);

        _logger.LogDebug("Response from WebService: ({StatusCode}) {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new SoapRequestException(
                $"HTTP Error: {response.StatusCode}. Details: {errorContent}",
                errorContent,
                soapEnvelope);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static List<T> ParseSoapResponseList<T>(string result, IWebServiceElement instance) where T : IWebServiceElement, new()
    {
        XDocument xmlDoc = XDocument.Parse(result);
        XNamespace ns = instance.Namespace;

        var elements = xmlDoc.Descendants(ns + instance.ServiceName);

        if (!elements.Any())
        {
            return [];
        }

        var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(instance.ServiceName) { Namespace = instance.Namespace });
        List<T> list = [];

        foreach (var element in elements)
        {
            using var reader = element.CreateReader();
            T obj = (T)serializer.Deserialize(reader)!;
            list.Add(obj);
        }

        return list;
    }

    private static T ParseSoapResponseSingle<T>(string result, IWebServiceElement instance) where T : IWebServiceElement, new()
    {
        XDocument xmlDoc = XDocument.Parse(result);
        XNamespace ns = instance.Namespace;

        var singleElement = xmlDoc.Descendants(ns + instance.ServiceName).FirstOrDefault()
            ?? throw new InvalidOperationException("No valid element found in the SOAP response." + result);

        var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(instance.ServiceName) { Namespace = instance.Namespace });
        using var reader = singleElement.CreateReader();
        return (T)serializer.Deserialize(reader)!;
    }

}
