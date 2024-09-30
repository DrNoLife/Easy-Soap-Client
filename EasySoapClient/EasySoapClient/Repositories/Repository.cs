using EasySoapClient.Enums;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml;

namespace EasySoapClient.Repositories;

public class Repository<T>(
    IHttpClientFactory httpClientFactory,
    ICredentialsProvider credentials,
    Uri serviceUrl,
    ISoapEnvelopeService soapEnvelopeService)
    : IRepository<T> where T : IWebServiceElement, new()
{
    private readonly T _instance = new();
    private readonly ICredentialsProvider _credentials = credentials;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly Uri _serviceUrl = serviceUrl;
    private readonly ISoapEnvelopeService _soapEnvelopeService = soapEnvelopeService;

    private string Credentials => _credentials.GenerateBase64Credentials();
    protected virtual string ServiceUrl => $"{_serviceUrl}/Page/{_instance.ServiceName}";

    public virtual async Task<List<T>> ReadMultipleAsync(IEnumerable<ReadMultipleFilter> filters, int size = 10, string? bookmarkKey = null)
    {
        string soapMessage = _soapEnvelopeService.CreateReadMultipleEnvelope(filters, size, bookmarkKey, _instance);
        string soapResponse = await SendSoapRequestAsync(CallMethod.ReadMultiple, soapMessage);

        // Parse response as a list.
        return ParseSoapResponseList(soapResponse);
    }

    public virtual async Task<T> CreateAsync(T item)
    {
        string soapMessage = _soapEnvelopeService.CreateCreateEnvelope(item);
        string soapResponse = await SendSoapRequestAsync(CallMethod.Create, soapMessage);

        // Parse response as a single object.
        return ParseSoapResponseSingle(soapResponse);
    }


    protected virtual string GetSoapAction(CallMethod methodToCall)
        => $"{_instance.Namespace}/{methodToCall}";


    protected virtual async Task<string> SendSoapRequestAsync(CallMethod soapMethod, string soapEnvelope)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

        content.Headers.Add("SOAPAction", GetSoapAction(soapMethod));

        // Add the Authorization header with the encoded credentials.
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Credentials);

        HttpResponseMessage response = await httpClient.PostAsync(ServiceUrl, content);
        string result = await response.Content.ReadAsStringAsync();

        return result;
    }

    protected virtual List<T> ParseSoapResponseList(string result)
    {
        // Parse the XML response.
        XDocument xmlDoc = XDocument.Parse(result);
        XNamespace ns = _instance.Namespace;

        // Extract the specified elements.
        var elements = xmlDoc.Descendants(ns + _instance.ServiceName);

        // If no items found, just return an empty list.
        if (!elements.Any())
        {
            return [];
        }

        // Deserialize each element to an object of type T.
        List<T> list = [];
        XmlSerializer serializer = new(typeof(T), new XmlRootAttribute(_instance.ServiceName) { Namespace = _instance.Namespace });

        foreach (var element in elements)
        {
            using XmlReader reader = element.CreateReader();
            T obj = (T)serializer.Deserialize(reader)!;
            list.Add(obj);
        }

        return list;
    }

    protected virtual T ParseSoapResponseSingle(string result)
    {
        // Parse the XML response.
        XDocument xmlDoc = XDocument.Parse(result);
        XNamespace ns = _instance.Namespace;

        // Extract a single element.
        var singleElement = xmlDoc.Descendants(ns + _instance.ServiceName).FirstOrDefault() 
            ?? throw new InvalidOperationException("No valid element found in the SOAP response." + result);

        // Deserialize the single element to an object of type T.
        XmlSerializer serializer = new(typeof(T), new XmlRootAttribute(_instance.ServiceName) { Namespace = _instance.Namespace });
        using XmlReader reader = singleElement.CreateReader();
        return (T)serializer.Deserialize(reader)!;
    }
}
