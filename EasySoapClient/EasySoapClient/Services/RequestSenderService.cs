using EasySoapClient.Enums;
using EasySoapClient.Exceptions;
using EasySoapClient.Interfaces;
using System.Text;
using Microsoft.Extensions.Logging;
using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Extensions;

namespace EasySoapClient.Services;

public class RequestSenderService(
    ILogger<RequestSenderService> logger,
    HttpClient httpClient) : IRequestSenderService
{
    private readonly ILogger<RequestSenderService> _logger = logger;
    private readonly HttpClient _httpClient = httpClient;

    public Task<string> SendWebServiceSoapRequestAsync(CallMethod soapMethod, string soapEnvelope, IWebServiceElement instance, CancellationToken cancellationToken = default)
    {
        string relativeUrl = $"Page/{instance.ServiceName}";
        string soapAction = $"{instance.GetXmlNamespace()}/{soapMethod}";
        return SendSoapRequestAsync(relativeUrl, soapEnvelope, soapAction, cancellationToken);
    }

    public Task<string> SendCodeUnitSoapRequestAsync(CodeUnitRequest request, string soapEnvelope, CancellationToken cancellationToken = default)
    {
        string relativeUrl = $"Codeunit/{request.CodeUnitName}";
        string soapAction = request.GenerateSoapActionDefinedNamespace();
        return SendSoapRequestAsync(relativeUrl, soapEnvelope, soapAction, cancellationToken);
    }

    private async Task<string> SendSoapRequestAsync(string relativeUrl, string soapEnvelope, string soapAction, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", soapAction);

        HttpResponseMessage response = await _httpClient.PostAsync(relativeUrl, content, cancellationToken);
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
