using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Contracts.Read;
using EasySoapClient.Delegates;
using EasySoapClient.Enums;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using EasySoapClient.Models.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace EasySoapClient.Services;

public class EasySoapService(
    ISoapEnvelopeService soapEnvelopeService,
    IParsingService parsingService,
    MaybeKeyedServiceResolver<IRequestSenderService> resolveRequestSender,
    [ServiceKey] string? serviceKey = null) : IEasySoapService
{
    private readonly ISoapEnvelopeService _soapEnvelopeService = soapEnvelopeService;
    private readonly IParsingService _parsingService = parsingService;
    private readonly IRequestSenderService _requestSenderService = resolveRequestSender(serviceKey);

    public async Task<List<T>> GetAsync<T>(IEnumerable<ReadMultipleFilter>? filters = null, int size = 10, string? bookmarkKey = null, CancellationToken cancellationToken = default) 
        where T : IWebServiceElement, new()
    {
        if (filters is null || !filters.Any())
        {
            ReadMultipleFilter emptyReadAllFilter = new("", "");
            filters = [emptyReadAllFilter];
        }

        var instance = new T();
        string soapMessage = _soapEnvelopeService.CreateReadMultipleEnvelope(filters, size, bookmarkKey, instance);
        string soapResponse = await _requestSenderService.SendWebServiceSoapRequestAsync(CallMethod.ReadMultiple, soapMessage, instance, cancellationToken);

        return _parsingService.ParseSoapResponseList<T>(soapResponse, instance);
    }

    public async Task<List<T>> GetAsync<T>(ReadMultipleFilter filter, int size = 10, string? bookmarkKey = null, CancellationToken cancellationToken = default) 
        where T : IWebServiceElement, new()
        => await GetAsync<T>(
            filters: [filter],
            size: size,
            bookmarkKey: bookmarkKey,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Note: This only works if the item has a singular id, and not multiple.. and the Id element is actually named Id.
    /// </summary>
    public async Task<T> GetItemAsync<T>(ReadRequest request, CancellationToken cancellationToken = default)
        where T : ISearchable, new()
    {
        var instance = new T();

        string soapMessage = _soapEnvelopeService.CreateReadEnvelope<T>(request);
        string soapResponse = await _requestSenderService.SendWebServiceSoapRequestAsync(CallMethod.Read, soapMessage, instance, cancellationToken);

        return _parsingService.ParseSoapResponseSingle<T>(soapResponse, instance);
    }

    public async Task<T> CreateAsync<T>(T item, CancellationToken cancellationToken = default) 
        where T : IWebServiceElement, new()
    {
        string soapMessage = _soapEnvelopeService.CreateCreateEnvelope(item);
        string soapResponse = await _requestSenderService.SendWebServiceSoapRequestAsync(CallMethod.Create, soapMessage, item, cancellationToken);

        return _parsingService.ParseSoapResponseSingle<T>(soapResponse, item);
    }

    public async Task<T> UpdateAsync<T>(T item, CancellationToken cancellationToken = default) 
        where T : IUpdatableWebServiceElement, new()
    {
        string soapMessage = _soapEnvelopeService.CreateUpdateEnvelope(item);
        string soapResponse = await _requestSenderService.SendWebServiceSoapRequestAsync(CallMethod.Update, soapMessage, item, cancellationToken);

        return _parsingService.ParseSoapResponseSingle<T>(soapResponse, item);
    }

    public async Task<string> GetIdFromKeyAsync<T>(string key, bool longResult = false, CancellationToken cancellationToken = default) 
        where T : ISearchable, new()
    {
        string soapMessage = _soapEnvelopeService.CreateGetIdEnvelope<T>(key);
        string soapResponse = await _requestSenderService.SendWebServiceSoapRequestAsync(CallMethod.GetRecIdFromKey, soapMessage, new T(), cancellationToken);

        var parsedResult = _parsingService.ParseIdFromKey<T>(soapResponse);

        return longResult
            ? parsedResult
            : parsedResult.Split(": ").Last();
    }

    public async Task<CodeUnitResponse> CallCodeUnitAsync(CodeUnitRequest request, CancellationToken cancellationToken = default)
    {
        string soapEnvelope = _soapEnvelopeService.CreateCodeUnitMethodInvocationEnvelope(request);
        string soapResponse = await _requestSenderService.SendCodeUnitSoapRequestAsync(request, soapEnvelope, cancellationToken);

        return _parsingService.ParseCodeUnitResponse(soapResponse);
    }
}
