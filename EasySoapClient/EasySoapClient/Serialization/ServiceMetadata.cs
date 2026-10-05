using EasySoapClient.Interfaces;

namespace EasySoapClient.Serialization;

/// <summary>
/// Service name and XML namespace of a page model, read once per type.
/// </summary>
internal static class ServiceMetadata<T> where T : IWebServiceElement, new()
{
    private static string? _serviceName;
    private static string? _xmlNamespace;

    // Computed lazily (not in a static initializer), so an invalid ServiceName gives a clear
    // InvalidOperationException on every call instead of a TypeInitializationException.
    public static string ServiceName => _serviceName ??= ValidateServiceName(new T().ServiceName);

    public static string XmlNamespace => _xmlNamespace ??= SoapNames.PageNamespace(ServiceName);

    private static string ValidateServiceName(string? serviceName)
    {
        if (String.IsNullOrWhiteSpace(serviceName))
        {
            throw new InvalidOperationException($"{typeof(T).Name}.{nameof(IWebServiceElement.ServiceName)} must return a non-empty service name.");
        }

        return serviceName;
    }
}

internal static class SoapNames
{
    public const string SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

    public static string PageNamespace(string serviceName)
        => $"urn:microsoft-dynamics-schemas/page/{serviceName.ToLowerInvariant()}";

    public static string PageSoapAction(string serviceName, string operation)
        => $"{PageNamespace(serviceName)}:{operation}";

    public static string PageUrl(string serviceName)
        => $"Page/{Uri.EscapeDataString(serviceName)}";

    public static string CodeUnitUrl(string codeUnitName)
        => $"Codeunit/{Uri.EscapeDataString(codeUnitName)}";
}

internal static class SoapOperations
{
    public const string Read = "Read";
    public const string ReadByRecId = "ReadByRecId";
    public const string ReadMultiple = "ReadMultiple";
    public const string Create = "Create";
    public const string CreateMultiple = "CreateMultiple";
    public const string Update = "Update";
    public const string UpdateMultiple = "UpdateMultiple";
    public const string Delete = "Delete";
    public const string IsUpdated = "IsUpdated";
    public const string GetRecIdFromKey = "GetRecIdFromKey";
}
