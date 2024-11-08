﻿using EasySoapClient.Enums;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace EasySoapClient.Services;

public class EasySoapService(
    IHttpClientFactory httpClientFactory,
    ISoapEnvelopeService soapEnvelopeService,
    ICredentialsProvider credentials,
    ILogger<EasySoapService> logger,
    IOptions<EasySoapClientOptions> options) : IEasySoapService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ISoapEnvelopeService _soapEnvelopeService = soapEnvelopeService;
    private readonly ICredentialsProvider _credentials = credentials;
    private readonly Uri _serviceUrl = new(options.Value.BaseUri);
    private readonly ILogger<EasySoapService> _logger = logger;  

    private string Credentials => _credentials.GenerateBase64Credentials();

    public async Task<List<T>> GetAsync<T>(IEnumerable<ReadMultipleFilter> filters, int size = 10, string? bookmarkKey = null) where T : IWebServiceElement, new()
    {
        var instance = new T();
        string serviceUrl = $"{_serviceUrl}/Page/{instance.ServiceName}";
        string soapMessage = _soapEnvelopeService.CreateReadMultipleEnvelope(filters, size, null, instance);
        string soapResponse = await SendSoapRequestAsync(CallMethod.ReadMultiple, soapMessage, instance, serviceUrl);

        return ParseSoapResponseList<T>(soapResponse, instance);
    }

    public async Task<T> CreateAsync<T>(T item) where T : IWebServiceElement, new()
    {
        var instance = item;
        string serviceUrl = $"{_serviceUrl}/Page/{instance.ServiceName}";
        string soapMessage = _soapEnvelopeService.CreateCreateEnvelope(item);
        string soapResponse = await SendSoapRequestAsync(CallMethod.Create, soapMessage, instance, serviceUrl);

        return ParseSoapResponseSingle<T>(soapResponse, instance);
    }

    public async Task<T> UpdateAsync<T>(T item) where T : IUpdatableWebServiceElement, new()
    {
        var instance = item;
        string serviceUrl = $"{_serviceUrl}/Page/{instance.ServiceName}";
        string soapMessage = _soapEnvelopeService.CreateUpdateEnvelope(item);
        string soapResponse = await SendSoapRequestAsync(CallMethod.Update, soapMessage, instance, serviceUrl);

        return ParseSoapResponseSingle<T>(soapResponse, instance);
    }

    private static string GetSoapAction(CallMethod methodToCall, IWebServiceElement instance)
        => $"{instance.Namespace}/{methodToCall}";

    private async Task<string> SendSoapRequestAsync(CallMethod soapMethod, string soapEnvelope, IWebServiceElement instance, string serviceUrl)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

        content.Headers.Add("SOAPAction", GetSoapAction(soapMethod, instance));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Credentials);

        HttpResponseMessage response = await httpClient.PostAsync(serviceUrl, content);

        _logger.LogDebug("Response from WebService: ({StatusCode}) {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);

        response.EnsureSuccessStatusCode(); 
        string result = await response.Content.ReadAsStringAsync();

        return result;
    }

    private List<T> ParseSoapResponseList<T>(string result, IWebServiceElement instance) where T : IWebServiceElement, new()
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

    private T ParseSoapResponseSingle<T>(string result, IWebServiceElement instance) where T : IWebServiceElement, new()
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
