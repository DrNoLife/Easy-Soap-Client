namespace EasySoapClient.Interfaces;

/// <summary>
/// A page model that carries the NAV <c>Key</c> (the record's bookmark key). Required for
/// <c>Update</c> and for automatic paging with <c>GetAllAsync</c>.
/// </summary>
public interface IKeyedWebServiceElement : IWebServiceElement
{
    /// <summary>The NAV record key, as returned by the web service.</summary>
    string Key { get; set; }
}
