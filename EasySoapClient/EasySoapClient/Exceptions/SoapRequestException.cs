using System.Net;

namespace EasySoapClient.Exceptions;

/// <summary>
/// Thrown when the web service answers with a non-success HTTP status code, typically a SOAP fault.
/// </summary>
public class SoapRequestException : Exception
{
    /// <summary>The HTTP status code of the response.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The SOAP <c>faultcode</c>, if the response was a SOAP fault.</summary>
    public string? FaultCode { get; }

    /// <summary>The SOAP <c>faultstring</c>, if the response was a SOAP fault. This is the error message from NAV.</summary>
    public string? FaultString { get; }

    /// <summary>The raw response body.</summary>
    public string ErrorContent { get; }

    /// <summary>
    /// The SOAP envelope that was sent. Only populated when
    /// <see cref="Models.EasySoapClientOptions.IncludeEnvelopeInExceptions"/> is enabled, as it can contain business data.
    /// </summary>
    public string? SoapEnvelope { get; }

    /// <summary>Creates a new exception.</summary>
    public SoapRequestException(
        string message,
        HttpStatusCode statusCode,
        string? faultCode,
        string? faultString,
        string errorContent,
        string? soapEnvelope,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        FaultCode = faultCode;
        FaultString = faultString;
        ErrorContent = errorContent;
        SoapEnvelope = soapEnvelope;
    }
}
