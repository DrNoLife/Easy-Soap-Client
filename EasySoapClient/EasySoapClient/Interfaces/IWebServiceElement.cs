namespace EasySoapClient.Interfaces;

/// <summary>
/// A model that maps to a page published as a SOAP web service.
/// Properties are mapped to page fields by name, or by <see cref="System.Xml.Serialization.XmlElementAttribute"/>.
/// </summary>
public interface IWebServiceElement
{
    /// <summary>
    /// The service name of the page, as published in NAV / Business Central (e.g. <c>Customer</c>).
    /// It is read once per model type and cached, so it must not depend on instance state.
    /// </summary>
    public string ServiceName { get; }
}
