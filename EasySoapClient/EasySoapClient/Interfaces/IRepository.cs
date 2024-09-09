using EasySoapClient.Models;

namespace EasySoapClient.Interfaces;

public interface IRepository<TWebservice> where TWebservice : IWebServiceElement
{
    public Task<List<TWebservice>> ReadMultipleAsync(ReadMultipleFilter filter);
    public Task<TWebservice> CreateAsync(TWebservice item);
}