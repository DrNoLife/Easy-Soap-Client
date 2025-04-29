using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Contracts.Read;
using EasySoapClient.Models;
using EasySoapClient.Models.Responses;

namespace EasySoapClient.Interfaces;

public interface IEasySoapService
{
    Task<List<T>> GetAsync<T>(
        IEnumerable<ReadMultipleFilter>? filters = null, 
        int size = 10, 
        string? bookmarkKey = null, 
        CancellationToken cancellationToken = default) where T : IWebServiceElement, new();

    Task<List<T>> GetAsync<T>(
        ReadMultipleFilter filter, 
        int size = 10, 
        string? bookmarkKey = null, 
        CancellationToken cancellationToken = default) where T : IWebServiceElement, new();

    Task<T> GetItemAsync<T>(ReadRequest request, CancellationToken cancellationToken = default)
        where T : ISearchable, new();

    Task<T> CreateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IWebServiceElement, new();
    Task<T> UpdateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IUpdatableWebServiceElement, new();

    Task<string> GetIdFromKeyAsync<T>(string key, bool longResult = false, CancellationToken cancellationToken = default) where T : ISearchable, new();

    Task<CodeUnitResponse> CallCodeUnitAsync(CodeUnitRequest request, CancellationToken cancellationToken = default);
}
