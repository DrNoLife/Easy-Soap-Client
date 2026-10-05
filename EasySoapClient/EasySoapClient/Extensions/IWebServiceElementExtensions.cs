using EasySoapClient.Interfaces;
using EasySoapClient.Serialization;
using System.Xml.Linq;

namespace EasySoapClient.Extensions;

/// <summary>
/// Helpers for page models.
/// </summary>
public static class IWebServiceElementExtensions
{
    /// <summary>
    /// The XML namespace of the page, e.g. <c>urn:microsoft-dynamics-schemas/page/customer</c>.
    /// </summary>
    public static XNamespace GetXmlNamespace(this IWebServiceElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return SoapNames.PageNamespace(element.ServiceName);
    }
}
