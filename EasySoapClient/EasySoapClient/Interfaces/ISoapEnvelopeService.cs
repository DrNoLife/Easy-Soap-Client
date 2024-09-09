using EasySoapClient.Models;

namespace EasySoapClient.Interfaces;

public interface ISoapEnvelopeService
{
    string CreateReadMultipleEnvelope<T>(ReadMultipleFilter filter, T serviceElement) where T : IWebServiceElement;
    string CreateCreateEnvelope<T>(T item) where T : IWebServiceElement;
}
