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
    Uri serviceUrl)
    : IRepository<T> where T : IWebServiceElement, new()
{
    private readonly T _instance = new();
    private readonly ICredentialsProvider _credentials = credentials;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly Uri _serviceUrl = serviceUrl;

    private string Credentials => _credentials.GenerateBase64Credentials();
    private string ServiceUrl => $"{_serviceUrl}/Page/{_instance.ServiceName}";

    public virtual async Task<List<T>> ReadMultipleAsync(ReadMultipleFilter filter)
    {
        string soapMessage = CreateReadMultipleEnvelope(filter);
        return await SendSoapRequestAsync(CallMethod.ReadMultiple, soapMessage);
    }


    protected virtual string GetSoapAction(CallMethod methodToCall)
        => $"{_instance.Namespace}/{methodToCall}";


    protected virtual async Task<List<T>> SendSoapRequestAsync(CallMethod soapMethod, string soapEnvelope)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

        // Set the SOAPAction header if required by the service.
        content.Headers.Add("SOAPAction", GetSoapAction(soapMethod));

        // Add the Authorization header with the encoded credentials.
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Credentials);

        HttpResponseMessage response = await httpClient.PostAsync(ServiceUrl, content);
        string result = await response.Content.ReadAsStringAsync();

        return ParseSoapResponse(result);
    }

    protected virtual List<T> ParseSoapResponse(string result)
    {
        // Parse the XML response.
        XDocument xmlDoc = XDocument.Parse(result);
        XNamespace ns = _instance.Namespace;

        // Extract the specified elements.
        var elements = xmlDoc.Descendants(ns + _instance.ServiceName);

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

    protected virtual string CreateReadMultipleEnvelope(ReadMultipleFilter filter)
    {
        var soapMessage = new StringBuilder();
        soapMessage.Append($@"
            <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsns=""{_instance.Namespace}"">
                <soapenv:Header/>
                <soapenv:Body>
                    <wsns:ReadMultiple>
                        <wsns:filter>
                            <wsns:Field>{filter.Field}</wsns:Field>
                            <wsns:Criteria>{filter.Criteria}</wsns:Criteria>
                        </wsns:filter>
                        <wsns:setSize>{filter.Size}</wsns:setSize>");

        if (!String.IsNullOrEmpty(filter.BookmarkKey))
        {
            soapMessage.Append($@"<wsns:bookmarkKey>{filter.BookmarkKey}</wsns:bookmarkKey>");
        }

        soapMessage.Append(@"
                    </wsns:ReadMultiple>
                </soapenv:Body>
            </soapenv:Envelope>");

        return soapMessage.ToString();
    }

}
