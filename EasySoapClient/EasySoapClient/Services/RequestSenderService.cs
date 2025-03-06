using EasySoapClient.Enums;
using EasySoapClient.Exceptions;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

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
        IServiceProvider serviceProvider,
        IOptionsMonitor<EasySoapClientOptions> optionsMonitor,
        [ServiceKey] string? serviceKey = null)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        var options = serviceKey is not null
            ? optionsMonitor.Get(serviceKey) // Keyed configuration.
            : optionsMonitor.CurrentValue;   // Non-keyed (default) configuration.

        _serviceUrl = new Uri(options.BaseUri);

        // Resolve the correct credentials provider.
        _credentials = serviceKey is not null
            ? serviceProvider.GetRequiredKeyedService<ICredentialsProvider>(serviceKey)
            : serviceProvider.GetRequiredService<ICredentialsProvider>();
    }

    public async Task<string> SendWebServiceSoapRequestAsync(CallMethod soapMethod, string soapEnvelope, IWebServiceElement instance, CancellationToken cancellationToken = default)
    {
        string serviceUrl = $"{_serviceUrl}/Page/{instance.ServiceName}";

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

    private static string GetSoapAction(CallMethod methodToCall, IWebServiceElement instance)
        => $"{instance.Namespace}/{methodToCall}";
}
