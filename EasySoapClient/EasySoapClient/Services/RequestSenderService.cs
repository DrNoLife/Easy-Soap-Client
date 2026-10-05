using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EasySoapClient.Exceptions;
using EasySoapClient.Interfaces;
using EasySoapClient.Logging;
using EasySoapClient.Serialization;
using Microsoft.Extensions.Logging;

namespace EasySoapClient.Services;

internal sealed class RequestSenderService(
    ILogger<RequestSenderService> logger,
    HttpClient httpClient,
    string baseQuery,
    SoapAuthenticator authenticator,
    bool includeEnvelopeInExceptions) : IRequestSenderService
{
    private readonly ILogger<RequestSenderService> _logger = logger;
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _baseQuery = baseQuery;
    private readonly SoapAuthenticator _authenticator = authenticator;
    private readonly bool _includeEnvelopeInExceptions = includeEnvelopeInExceptions;

    public async Task<TResult> SendAsync<TResult>(
        string relativeUrl,
        string soapAction,
        string soapEnvelope,
        Func<Stream, TResult> parse,
        CancellationToken cancellationToken)
    {
        // The base address' query (e.g. ?tenant=t1 for multitenant servers) is not inherited by relative URLs.
        using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl + _baseQuery)
        {
            Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml"),
        };

        // SOAP 1.1: the SOAPAction header value is a quoted string.
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");
        request.Headers.Authorization = await _authenticator.CreateHeaderAsync(cancellationToken).ConfigureAwait(false);

        // The body is buffered (ResponseContentRead) on purpose: the parser needs to re-read it
        // if it has to fall back to sanitizing illegal XML characters.
        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        Log.ResponseReceived(_logger, relativeUrl, (int)response.StatusCode, response.ReasonPhrase);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw CreateException(response.StatusCode, response.ReasonPhrase, errorContent, soapEnvelope);
        }

        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            stream = buffer;
        }

        await using (stream.ConfigureAwait(false))
        {
            return parse(stream);
        }
    }

    private SoapRequestException CreateException(HttpStatusCode statusCode, string? reasonPhrase, string errorContent, string soapEnvelope)
    {
        (string? faultCode, string? faultString) = TryParseFault(errorContent);

        string message = faultString is not null
            ? $"SOAP request failed with HTTP {(int)statusCode} ({statusCode}): {faultString}"
            : $"SOAP request failed with HTTP {(int)statusCode} ({reasonPhrase ?? statusCode.ToString()}).";

        return new SoapRequestException(
            message,
            statusCode,
            faultCode,
            faultString,
            errorContent,
            _includeEnvelopeInExceptions ? soapEnvelope : null);
    }

    private static (string? FaultCode, string? FaultString) TryParseFault(string content)
    {
        if (String.IsNullOrWhiteSpace(content))
        {
            return (null, null);
        }

        try
        {
            using var textReader = new StringReader(content);
            using XmlReader reader = XmlReader.Create(textReader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            XDocument document = XDocument.Load(reader);

            XElement? fault = document.Descendants(XName.Get("Fault", SoapNames.SoapEnvelopeNamespace)).FirstOrDefault();
            if (fault is null)
            {
                return (null, null);
            }

            // SOAP 1.1 fault children are unqualified.
            return (fault.Element("faultcode")?.Value.Trim(), fault.Element("faultstring")?.Value.Trim());
        }
        catch (XmlException)
        {
            return (null, null);
        }
    }
}
