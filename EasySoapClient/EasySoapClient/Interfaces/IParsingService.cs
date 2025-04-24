using EasySoapClient.Models.Responses;

namespace EasySoapClient.Interfaces;

public interface IParsingService
{
    List<T> ParseSoapResponseList<T>(string result, IWebServiceElement instance) 
        where T : IWebServiceElement, new();

    T ParseSoapResponseSingle<T>(string result, IWebServiceElement instance) 
        where T : IWebServiceElement, new();

    string ParseIdFromKey<T>(string result);

    CodeUnitResponse ParseCodeUnitResponse(string response);
}
