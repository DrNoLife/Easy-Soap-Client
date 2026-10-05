using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Contracts.Read;
using EasySoapClient.Models;

namespace EasySoapClient.Interfaces;

internal interface ISoapEnvelopeService
{
    string CreateReadMultipleEnvelope(string serviceName, IReadOnlyCollection<ReadMultipleFilter> filters, int size, string? bookmarkKey);

    string CreateReadEnvelope(string serviceName, ReadRequest request);

    string CreateReadByRecIdEnvelope(string serviceName, string recId);

    string CreateCreateEnvelope<T>(T item) where T : IWebServiceElement, new();

    string CreateCreateMultipleEnvelope<T>(IReadOnlyCollection<T> items) where T : IWebServiceElement, new();

    string CreateUpdateEnvelope<T>(T item) where T : IKeyedWebServiceElement, new();

    string CreateUpdateMultipleEnvelope<T>(IReadOnlyCollection<T> items) where T : IKeyedWebServiceElement, new();

    string CreateKeyOperationEnvelope(string serviceName, string operation, string key);

    string CreateCodeUnitMethodInvocationEnvelope(CodeUnitRequest request);
}
