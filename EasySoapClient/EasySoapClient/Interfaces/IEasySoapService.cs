using EasySoapClient.Models;

namespace EasySoapClient.Interfaces;

public interface IEasySoapService
{
    Task<List<T>> GetAsync<T>(IEnumerable<ReadMultipleFilter> filters, int size = 10, string? bookmarkKey = null) where T : IWebServiceElement, new();
    Task<T> CreateAsync<T>(T item) where T : IWebServiceElement, new();
    Task<T> UpdateAsync<T>(T item) where T : IUpdatableWebServiceElement, new();
}
