using EasySoapClient.Models;

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

    Task<T> CreateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IWebServiceElement, new();
    Task<T> UpdateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IUpdatableWebServiceElement, new();
}
