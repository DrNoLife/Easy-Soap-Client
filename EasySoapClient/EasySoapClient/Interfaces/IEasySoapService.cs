using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Contracts.Read;
using EasySoapClient.Models;
using EasySoapClient.Models.Responses;

namespace EasySoapClient.Interfaces;

/// <summary>
/// Calls pages and codeunits published as SOAP web services.
/// </summary>
public interface IEasySoapService
{
    /// <summary>
    /// Calls <c>ReadMultiple</c> and returns one page of records.
    /// </summary>
    /// <param name="filters">Filters to apply (combined with AND). <see langword="null"/> or empty applies no filter; <paramref name="size"/> still limits the result.</param>
    /// <param name="size">Maximum number of records to return. 0 means no limit (all records). Defaults to 10.</param>
    /// <param name="bookmarkKey">The <c>Key</c> of the last record of the previous page, to continue after it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<T>> GetAsync<T>(
        IEnumerable<ReadMultipleFilter>? filters = null,
        int size = 10,
        string? bookmarkKey = null,
        CancellationToken cancellationToken = default) where T : IWebServiceElement, new();

    /// <summary>
    /// Calls <c>ReadMultiple</c> with a single filter and returns one page of records.
    /// </summary>
    Task<IReadOnlyList<T>> GetAsync<T>(
        ReadMultipleFilter filter,
        int size = 10,
        string? bookmarkKey = null,
        CancellationToken cancellationToken = default) where T : IWebServiceElement, new();

    /// <summary>
    /// Streams every record matching the filters, calling <c>ReadMultiple</c> page by page and following the
    /// <c>Key</c> of the last record as bookmark.
    /// </summary>
    IAsyncEnumerable<T> GetAllAsync<T>(
        IEnumerable<ReadMultipleFilter>? filters = null,
        int pageSize = 100,
        CancellationToken cancellationToken = default) where T : IKeyedWebServiceElement, new();

    /// <summary>
    /// Calls <c>Read</c> with the given key fields. Returns <see langword="null"/> if no record matches.
    /// </summary>
    Task<T?> GetItemAsync<T>(ReadRequest request, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new();

    /// <summary>
    /// Calls <c>ReadByRecId</c>. Returns <see langword="null"/> if no record matches.
    /// </summary>
    Task<T?> GetItemByRecIdAsync<T>(string recId, CancellationToken cancellationToken = default)
        where T : IWebServiceElement, new();

    /// <summary>
    /// Calls <c>Create</c> and returns the created record as returned by the service.
    /// Properties that are <see langword="null"/> are not sent, and <c>Key</c> is never sent.
    /// </summary>
    Task<T> CreateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IWebServiceElement, new();

    /// <summary>
    /// Calls <c>CreateMultiple</c> and returns the created records.
    /// </summary>
    Task<IReadOnlyList<T>> CreateMultipleAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken = default) where T : IWebServiceElement, new();

    /// <summary>
    /// Calls <c>Update</c> and returns the updated record. Properties that are <see langword="null"/> are not sent and
    /// therefore not changed; use nullable properties for fields you do not want to overwrite.
    /// </summary>
    Task<T> UpdateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IKeyedWebServiceElement, new();

    /// <summary>
    /// Calls <c>UpdateMultiple</c> and returns the updated records.
    /// </summary>
    Task<IReadOnlyList<T>> UpdateMultipleAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken = default) where T : IKeyedWebServiceElement, new();

    /// <summary>
    /// Calls <c>Delete</c> for the record with the given <c>Key</c>. Returns the service's result.
    /// </summary>
    Task<bool> DeleteAsync<T>(string key, CancellationToken cancellationToken = default) where T : IWebServiceElement, new();

    /// <summary>
    /// Calls <c>IsUpdated</c>: whether the record has changed since the given <c>Key</c> was read.
    /// </summary>
    Task<bool> IsUpdatedAsync<T>(string key, CancellationToken cancellationToken = default) where T : IWebServiceElement, new();

    /// <summary>
    /// Calls <c>GetRecIdFromKey</c>. NAV returns e.g. <c>Customer: 10000</c>; by default only the part after the table name
    /// prefix (the first <c>": "</c>) is returned. Set <paramref name="longResult"/> to get the full text.
    /// </summary>
    Task<string> GetIdFromKeyAsync<T>(string key, bool longResult = false, CancellationToken cancellationToken = default) where T : IWebServiceElement, new();

    /// <summary>
    /// Calls a codeunit method.
    /// </summary>
    Task<CodeUnitResponse> CallCodeUnitAsync(CodeUnitRequest request, CancellationToken cancellationToken = default);
}
