using EasySoapClient.Extensions;
using EasySoapClient.Interfaces;
using EasySoapClient.Models.Responses;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace EasySoapClient.Services;

public class ParsingService : IParsingService
{
    public List<T> ParseSoapResponseList<T>(string result, IWebServiceElement instance) 
        where T : IWebServiceElement, new()
    {
        XDocument xmlDoc = XDocument.Parse(result);
        XNamespace xmlNamespace = instance.GetXmlNamespace();

        var elements = xmlDoc.Descendants(xmlNamespace + instance.ServiceName);

        if (!elements.Any())
        {
            return [];
        }

        XmlSerializer serializer = new(typeof(T), new XmlRootAttribute(instance.ServiceName) { Namespace = xmlNamespace.ToString() });
        List<T> list = [];

        foreach (var element in elements)
        {
            using XmlReader reader = element.CreateReader();
            T obj = (T)serializer.Deserialize(reader)!;
            list.Add(obj);
        }

        return list;
    }

    public T ParseSoapResponseSingle<T>(string result, IWebServiceElement instance) 
        where T : IWebServiceElement, new()
    {
        XDocument xmlDoc = XDocument.Parse(result);
        XNamespace xmlNamespace = instance.GetXmlNamespace();

        var singleElement = xmlDoc.Descendants(xmlNamespace + instance.ServiceName).FirstOrDefault()
            ?? throw new InvalidOperationException("No valid element found in the SOAP response." + result);

        XmlSerializer serializer = new(typeof(T), new XmlRootAttribute(instance.ServiceName) { Namespace = xmlNamespace.ToString() });
        using XmlReader reader = singleElement.CreateReader();
        return (T)serializer.Deserialize(reader)!;
    }

    public CodeUnitResponse ParseCodeUnitResponse(string response)
    {
        XDocument doc = XDocument.Parse(response);

        XNamespace soapNs = "http://schemas.xmlsoap.org/soap/envelope/";
        XElement? body = doc?.Root?.Element(soapNs + "Body");

        if (body is null)
        {
            return new CodeUnitResponse(String.Empty);
        }

        // The result element is the first child element of the SOAP Body.
        XElement? resultElement = body.Elements().FirstOrDefault();
        if (resultElement is null)
        {
            return new CodeUnitResponse(String.Empty);
        }

        // The result element has its own namespace (e.g. "urn:microsoft-dynamics-schemas/codeunit/FIPTestCodeunit").
        XNamespace resultNs = resultElement.Name.Namespace;
        XElement? returnValueElement = resultElement.Element(resultNs + "return_value");

        string returnValue = returnValueElement is not null 
            ? returnValueElement.Value 
            : String.Empty;

        return new CodeUnitResponse(returnValue);
    }
}
