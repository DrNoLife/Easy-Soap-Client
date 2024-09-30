using EasySoapClient.Models;

namespace EasySoapClient.Interfaces;

public interface IRepository<TWebservice> where TWebservice : IWebServiceElement
{
    public Task<List<TWebservice>> ReadMultipleAsync(IEnumerable<ReadMultipleFilter> filters, int size = 10, string? bookmarkKey = null);
    public Task<TWebservice> CreateAsync(TWebservice item);
}