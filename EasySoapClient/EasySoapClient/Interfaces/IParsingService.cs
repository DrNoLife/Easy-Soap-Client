namespace EasySoapClient.Interfaces;

public interface IParsingService
{
    List<T> ParseSoapResponseList<T>(string result, IWebServiceElement instance) 
        where T : IWebServiceElement, new();

    T ParseSoapResponseSingle<T>(string result, IWebServiceElement instance) 
        where T : IWebServiceElement, new();
}
