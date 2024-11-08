using EasySoapClient.Models;

namespace EasySoapClient.Interfaces;

public interface ISoapEnvelopeService
{
    string CreateReadMultipleEnvelope<T>(IEnumerable<ReadMultipleFilter> filters, int size, string? bookmarkKey, T serviceElement) where T : IWebServiceElement;
    string CreateCreateEnvelope<T>(T item) where T : IWebServiceElement;
    string CreateUpdateEnvelope<T>(T item) where T : IUpdatableWebServiceElement;
}
