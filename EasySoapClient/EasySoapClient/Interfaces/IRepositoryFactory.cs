namespace EasySoapClient.Interfaces;

public interface IRepositoryFactory
{
    IRepository<T> CreateRepository<T>(Uri webserviceUri, ICredentialsProvider credentials) 
        where T : IWebServiceElement, new();
}
