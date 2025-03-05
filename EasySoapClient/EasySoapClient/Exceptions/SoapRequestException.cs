namespace EasySoapClient.Exceptions;

public class SoapRequestException(string message, string errorContent, string soapEnvelope) : Exception(message)
{
    public string ErrorContent { get; } = errorContent;
    public string SoapEnvelope { get; } = soapEnvelope;
}
