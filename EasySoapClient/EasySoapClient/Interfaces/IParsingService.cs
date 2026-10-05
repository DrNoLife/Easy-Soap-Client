using EasySoapClient.Models.Responses;

namespace EasySoapClient.Interfaces;

internal interface IParsingService
{
    List<T> ParseList<T>(Stream response, string elementName, string xmlNamespace);

    (bool Found, T? Value) ParseSingle<T>(Stream response, string elementName, string xmlNamespace);

    string? ParseResultValue(Stream response, string resultElementName);

    CodeUnitResponse ParseCodeUnitResponse(Stream response);
}
