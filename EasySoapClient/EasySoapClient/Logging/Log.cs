using Microsoft.Extensions.Logging;

namespace EasySoapClient.Logging;

internal static partial class Log
{
    // Trace, as envelopes contain business data.
    [LoggerMessage(EventId = 1, Level = LogLevel.Trace, Message = "SOAP envelope created for {Kind}/{Name}:{Operation}:\n{Envelope}")]
    public static partial void EnvelopeCreated(ILogger logger, string kind, string name, string operation, string envelope);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "SOAP response from {RelativeUrl}: ({StatusCode}) {ReasonPhrase}")]
    public static partial void ResponseReceived(ILogger logger, string relativeUrl, int statusCode, string? reasonPhrase);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Failed to parse SOAP XML at line {LineNumber}, position {LinePosition}. Attempting to remove illegal XML characters and retrying. XML context: \n{XmlContext}")]
    public static partial void ParseFailedRetrying(ILogger logger, Exception exception, int lineNumber, int linePosition, string xmlContext);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Successfully parsed the SOAP XML after removing illegal characters.")]
    public static partial void ParseSucceededAfterSanitizing(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to parse SOAP XML even after removing illegal characters. Line {LineNumber}, position {LinePosition}. XML context: \n{XmlContext}")]
    public static partial void ParseFailedAfterSanitizing(ILogger logger, Exception exception, int lineNumber, int linePosition, string xmlContext);
}
