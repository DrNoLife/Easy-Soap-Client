﻿using EasySoapClient.Enums;
using EasySoapClient.Exceptions;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Delegates;

namespace EasySoapClient.Services;

public class RequestSenderService : IRequestSenderService
{
    private readonly ILogger<RequestSenderService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialsProvider _credentials;
    private readonly Uri _serviceUrl;

    private string Credentials => _credentials.GenerateBase64Credentials();

    public RequestSenderService(
        ILogger<RequestSenderService> logger,
        IHttpClientFactory httpClientFactory,
        MaybeKeyedServiceResolver<ICredentialsProvider> resolveRequestSender,
        IOptionsMonitor<EasySoapClientOptions> optionsMonitor,
        [ServiceKey] string? serviceKey = null)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        var options = serviceKey is not null
            ? optionsMonitor.Get(serviceKey) // Keyed configuration.
            : optionsMonitor.CurrentValue;   // Non-keyed (default) configuration.

        _serviceUrl = new Uri(options.BaseUri);
        _credentials = resolveRequestSender(serviceKey);
    }

    public Task<string> SendWebServiceSoapRequestAsync(CallMethod soapMethod, string soapEnvelope, IWebServiceElement instance, CancellationToken cancellationToken = default)
    {
        string relativeUrl = $"/Page/{instance.ServiceName}";
        string soapAction = $"{instance.Namespace}/{soapMethod}";
        return SendSoapRequestAsync(relativeUrl, soapEnvelope, soapAction, cancellationToken);
    }

    public Task<string> SendCodeUnitSoapRequestAsync(CodeUnitRequest request, string soapEnvelope, CancellationToken cancellationToken = default)
    {
        string relativeUrl = $"/Codeunit/{request.CodeUnitName}";
        string soapAction = $"urn:microsoft-dynamics-schemas/codeunit/{request.CodeUnitName}:{request.MethodName}";
        return SendSoapRequestAsync(relativeUrl, soapEnvelope, soapAction, cancellationToken);
    }

    private async Task<string> SendSoapRequestAsync(string relativeUrl, string soapEnvelope, string soapAction, CancellationToken cancellationToken = default)
    {
        string serviceUrl = $"{_serviceUrl}{relativeUrl}";

        using var httpClient = _httpClientFactory.CreateClient();
        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", soapAction);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Credentials);

        HttpResponseMessage response = await httpClient.PostAsync(serviceUrl, content, cancellationToken);
        _logger.LogDebug("Response from SOAP Request: ({StatusCode}) {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);

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
}
