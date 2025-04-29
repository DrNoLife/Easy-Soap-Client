using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Contracts.Read;
using EasySoapClient.Models;

namespace EasySoapClient.Interfaces;

public interface ISoapEnvelopeService
{
    string CreateReadMultipleEnvelope<T>(IEnumerable<ReadMultipleFilter> filters, int size, string? bookmarkKey, T serviceElement) 
        where T : IWebServiceElement;

    string CreateReadEnvelope<T>(ReadRequest request)
        where T : ISearchable, new();

    string CreateCreateEnvelope<T>(T item) 
        where T : IWebServiceElement;

    string CreateUpdateEnvelope<T>(T item) 
        where T : IUpdatableWebServiceElement;

    string CreateGetIdEnvelope<T>(string key)
        where T : ISearchable, new();

    string CreateCodeUnitMethodInvocationEnvelope(CodeUnitRequest request);
}
