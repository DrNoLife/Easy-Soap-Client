using EasySoapClient.Interfaces;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace EasySoapClient.Services;

public class ParsingService : IParsingService
{
    public List<T> ParseSoapResponseList<T>(string result, IWebServiceElement instance) where T : IWebServiceElement, new()
    {
        XDocument xmlDoc = XDocument.Parse(result);
        XNamespace ns = instance.Namespace;

        var elements = xmlDoc.Descendants(ns + instance.ServiceName);

        if (!elements.Any())
        {
            return [];
        }

        var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(instance.ServiceName) { Namespace = instance.Namespace });
        List<T> list = [];

        foreach (var element in elements)
        {
            using var reader = element.CreateReader();
            T obj = (T)serializer.Deserialize(reader)!;
            list.Add(obj);
        }

        return list;
    }

    public T ParseSoapResponseSingle<T>(string result, IWebServiceElement instance) where T : IWebServiceElement, new()
    {
        XDocument xmlDoc = XDocument.Parse(result);
        XNamespace ns = instance.Namespace;

        var singleElement = xmlDoc.Descendants(ns + instance.ServiceName).FirstOrDefault()
            ?? throw new InvalidOperationException("No valid element found in the SOAP response." + result);

        var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(instance.ServiceName) { Namespace = instance.Namespace });
        using var reader = singleElement.CreateReader();
        return (T)serializer.Deserialize(reader)!;
    }
}
