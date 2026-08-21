using EasySoapClient.Extensions;
using EasySoapClient.Interfaces;
using EasySoapClient.Models.Responses;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace EasySoapClient.Services;

public class ParsingService(ILogger<ParsingService> logger) : IParsingService
{
    private readonly ILogger<ParsingService> _logger = logger;

    public List<T> ParseSoapResponseList<T>(string result, IWebServiceElement instance)
        where T : IWebServiceElement, new()
    {
        XDocument xmlDoc = ParseDocument(result);
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
        XDocument xmlDoc = ParseDocument(result);
        XNamespace xmlNamespace = instance.GetXmlNamespace();

        var singleElement = xmlDoc.Descendants(xmlNamespace + instance.ServiceName).FirstOrDefault()
            ?? throw new InvalidOperationException("No valid element found in the SOAP response." + result);

        XmlSerializer serializer = new(typeof(T), new XmlRootAttribute(instance.ServiceName) { Namespace = xmlNamespace.ToString() });
        using XmlReader reader = singleElement.CreateReader();
        return (T)serializer.Deserialize(reader)!;
    }

    public string ParseIdFromKey<T>(string result)
    {
        if (String.IsNullOrEmpty(result))
        {
            throw new ArgumentException("Result is null or empty.", nameof(result));
        }

        XDocument xmlDoc = ParseDocument(result);

        XElement? element = xmlDoc
            .Descendants() 
            .FirstOrDefault(x => x.Name.LocalName.Equals("GetRecIdFromKey_Result", StringComparison.Ordinal));

        return element?.Value.Trim() ?? String.Empty;
    }

    public CodeUnitResponse ParseCodeUnitResponse(string response)
    {
        XDocument doc = ParseDocument(response);

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

    private XDocument ParseDocument(string xml)
    {
        try
        {
            return XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            string context = GetXmlContext(xml, ex.LineNumber, linesBefore: 3, linesAfter: 3);

            _logger.LogError(
                ex,
                "Failed to parse SOAP XML at line {LineNumber}, position {LinePosition}. XML context: \n{XmlContext}",
                ex.LineNumber,
                ex.LinePosition,
                context);

            throw;
        }
    }

    private static string GetXmlContext(string xml, int targetLine, int linesBefore, int linesAfter)
    {
        int startLine = Math.Max(1, targetLine - linesBefore);
        int endLine = targetLine + linesAfter;

        using StringReader reader = new(xml);
        StringBuilder builder = new();
        int currentLine = 1;

        while (reader.ReadLine() is { } line)
        {
            if (currentLine > endLine)
            {
                break;
            }

            if (currentLine >= startLine)
            {
                string marker = currentLine == targetLine ? ">>>" : "   ";
                builder.Append(marker);
                builder.Append(' ');
                builder.Append(currentLine);
                builder.Append(": ");
                builder.AppendLine(EscapeControlCharacters(line));
            }

            currentLine++;
        }

        return builder.ToString();
    }

    private static string EscapeControlCharacters(string value)
    {
        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            if (char.IsControl(character) && character is not '\r' and not '\n' and not '\t')
            {
                builder.Append($@"\u{(int)character:X4}");
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
