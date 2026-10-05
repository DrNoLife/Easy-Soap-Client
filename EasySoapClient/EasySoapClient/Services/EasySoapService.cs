using System.Runtime.CompilerServices;
using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Contracts.Read;
using EasySoapClient.Exceptions;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using EasySoapClient.Models.Responses;
using EasySoapClient.Serialization;

namespace EasySoapClient.Services;

internal sealed class EasySoapService(
    ISoapEnvelopeService soapEnvelopeService,
    IParsingService parsingService,
    IRequestSenderService requestSenderService) : IEasySoapService
{
    private readonly ISoapEnvelopeService _soapEnvelopeService = soapEnvelopeService;
    private readonly IParsingService _parsingService = parsingService;
    private readonly IRequestSenderService _requestSenderService = requestSenderService;

    public Task<IReadOnlyList<T>> GetAsync<T>(IEnumerable<ReadMultipleFilter>? filters = null, int size = 10, string? bookmarkKey = null, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new()
        => ReadMultipleAsync<T>(Materialize(filters), size, bookmarkKey, cancellationToken);

    public Task<IReadOnlyList<T>> GetAsync<T>(ReadMultipleFilter filter, int size = 10, string? bookmarkKey = null, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new()
        => ReadMultipleAsync<T>([filter], size, bookmarkKey, cancellationToken);

    public async IAsyncEnumerable<T> GetAllAsync<T>(
        IEnumerable<ReadMultipleFilter>? filters = null,
        int pageSize = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : IKeyedWebServiceElement, new()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        IReadOnlyCollection<ReadMultipleFilter> filterList = Materialize(filters);
        string? bookmarkKey = null;

        while (true)
        {
            IReadOnlyList<T> page = await ReadMultipleAsync<T>(filterList, pageSize, bookmarkKey, cancellationToken).ConfigureAwait(false);

            foreach (T item in page)
            {
                yield return item;
            }

            if (page.Count < pageSize)
            {
                yield break;
            }

            bookmarkKey = page[^1].Key;

            if (String.IsNullOrEmpty(bookmarkKey))
            {
                throw new SoapResponseException($"Cannot continue paging: the last {typeof(T).Name} on the page has no Key. Map the 'Key' element on the model.");
            }
        }
    }

    public async Task<T?> GetItemAsync<T>(ReadRequest request, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new()
    {
        string serviceName = ServiceMetadata<T>.ServiceName;
        string envelope = _soapEnvelopeService.CreateReadEnvelope(serviceName, request);

        (bool found, T? item) = await SendPageAsync(serviceName, SoapOperations.Read, envelope,
            stream => _parsingService.ParseSingle<T>(stream, serviceName, ServiceMetadata<T>.XmlNamespace), cancellationToken).ConfigureAwait(false);

        return found ? item : default;
    }

    public async Task<T?> GetItemByRecIdAsync<T>(string recId, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new()
    {
        string serviceName = ServiceMetadata<T>.ServiceName;
        string envelope = _soapEnvelopeService.CreateReadByRecIdEnvelope(serviceName, recId);

        (bool found, T? item) = await SendPageAsync(serviceName, SoapOperations.ReadByRecId, envelope,
            stream => _parsingService.ParseSingle<T>(stream, serviceName, ServiceMetadata<T>.XmlNamespace), cancellationToken).ConfigureAwait(false);

        return found ? item : default;
    }

    public async Task<T> CreateAsync<T>(T item, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new()
    {
        string envelope = _soapEnvelopeService.CreateCreateEnvelope(item);

        return await SendAndParseRecordAsync<T>(SoapOperations.Create, envelope, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<T>> CreateMultipleAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new()
    {
        ArgumentNullException.ThrowIfNull(items);
        T[] itemArray = [.. items];
        if (itemArray.Length == 0)
        {
            return [];
        }

        string envelope = _soapEnvelopeService.CreateCreateMultipleEnvelope(itemArray);

        return await SendAndParseListAsync<T>(SoapOperations.CreateMultiple, envelope, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> UpdateAsync<T>(T item, CancellationToken cancellationToken = default)
        where T : IKeyedWebServiceElement, new()
    {
        string envelope = _soapEnvelopeService.CreateUpdateEnvelope(item);

        return await SendAndParseRecordAsync<T>(SoapOperations.Update, envelope, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<T>> UpdateMultipleAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken = default)
        where T : IKeyedWebServiceElement, new()
    {
        ArgumentNullException.ThrowIfNull(items);
        T[] itemArray = [.. items];
        if (itemArray.Length == 0)
        {
            return [];
        }

        string envelope = _soapEnvelopeService.CreateUpdateMultipleEnvelope(itemArray);

        return await SendAndParseListAsync<T>(SoapOperations.UpdateMultiple, envelope, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> DeleteAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new()
        => SendKeyOperationForBoolAsync<T>(SoapOperations.Delete, key, cancellationToken);

    public Task<bool> IsUpdatedAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new()
        => SendKeyOperationForBoolAsync<T>(SoapOperations.IsUpdated, key, cancellationToken);

    public async Task<string> GetIdFromKeyAsync<T>(string key, bool longResult = false, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new()
    {
        string result = await SendKeyOperationAsync<T>(SoapOperations.GetRecIdFromKey, key, cancellationToken).ConfigureAwait(false);

        if (longResult)
        {
            return result;
        }

        // NAV returns e.g. "Customer: 10000"; key values themselves may contain ": ".
        int separator = result.IndexOf(": ", StringComparison.Ordinal);
        return separator < 0 ? result : result[(separator + 2)..];
    }

    public async Task<CodeUnitResponse> CallCodeUnitAsync(CodeUnitRequest request, CancellationToken cancellationToken = default)
    {
        string envelope = _soapEnvelopeService.CreateCodeUnitMethodInvocationEnvelope(request);

        return await _requestSenderService.SendAsync(
            SoapNames.CodeUnitUrl(request.CodeUnitName),
            request.GenerateSoapActionDefinedNamespace(),
            envelope,
            _parsingService.ParseCodeUnitResponse,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<T>> ReadMultipleAsync<T>(IReadOnlyCollection<ReadMultipleFilter> filters, int size, string? bookmarkKey, CancellationToken cancellationToken)
        where T : IWebServiceElement, new()
    {
        string envelope = _soapEnvelopeService.CreateReadMultipleEnvelope(ServiceMetadata<T>.ServiceName, filters, size, bookmarkKey);

        return await SendAndParseListAsync<T>(SoapOperations.ReadMultiple, envelope, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<T>> SendAndParseListAsync<T>(string operation, string envelope, CancellationToken cancellationToken)
        where T : IWebServiceElement, new()
    {
        string serviceName = ServiceMetadata<T>.ServiceName;

        return await SendPageAsync(serviceName, operation, envelope,
            stream => _parsingService.ParseList<T>(stream, serviceName, ServiceMetadata<T>.XmlNamespace), cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> SendAndParseRecordAsync<T>(string operation, string envelope, CancellationToken cancellationToken)
        where T : IWebServiceElement, new()
    {
        string serviceName = ServiceMetadata<T>.ServiceName;

        (bool found, T? item) = await SendPageAsync(serviceName, operation, envelope,
            stream => _parsingService.ParseSingle<T>(stream, serviceName, ServiceMetadata<T>.XmlNamespace), cancellationToken).ConfigureAwait(false);

        return found && item is not null
            ? item
            : throw new SoapResponseException($"The {operation} response for '{serviceName}' did not contain a '{serviceName}' element.");
    }

    private async Task<string> SendKeyOperationAsync<T>(string operation, string key, CancellationToken cancellationToken)
        where T : IWebServiceElement, new()
    {
        string serviceName = ServiceMetadata<T>.ServiceName;
        string envelope = _soapEnvelopeService.CreateKeyOperationEnvelope(serviceName, operation, key);
        string resultElement = $"{operation}_Result";

        string? result = await SendPageAsync(serviceName, operation, envelope,
            stream => _parsingService.ParseResultValue(stream, resultElement), cancellationToken).ConfigureAwait(false);

        return result ?? throw new SoapResponseException($"The {operation} response for '{serviceName}' did not contain a '{resultElement}' element.");
    }

    private async Task<bool> SendKeyOperationForBoolAsync<T>(string operation, string key, CancellationToken cancellationToken)
        where T : IWebServiceElement, new()
    {
        string result = await SendKeyOperationAsync<T>(operation, key, cancellationToken).ConfigureAwait(false);

        try
        {
            return System.Xml.XmlConvert.ToBoolean(result);
        }
        catch (FormatException ex)
        {
            throw new SoapResponseException($"The {operation} response was not a boolean: '{result}'.", ex);
        }
    }

    private Task<TResult> SendPageAsync<TResult>(string serviceName, string operation, string envelope, Func<Stream, TResult> parse, CancellationToken cancellationToken)
        => _requestSenderService.SendAsync(
            SoapNames.PageUrl(serviceName),
            SoapNames.PageSoapAction(serviceName, operation),
            envelope,
            parse,
            cancellationToken);

    private static ReadMultipleFilter[] Materialize(IEnumerable<ReadMultipleFilter>? filters)
        => filters is null ? [] : [.. filters];
}
