using EasySoapClient.Interfaces;
using System.Xml.Linq;

namespace EasySoapClient.Extensions;

public static class IWebServiceElementExtensions
{
    public static XNamespace GetXmlNamespace(this IWebServiceElement element)
        => $"urn:microsoft-dynamics-schemas/page/{element.ServiceName.ToLower()}";
}
