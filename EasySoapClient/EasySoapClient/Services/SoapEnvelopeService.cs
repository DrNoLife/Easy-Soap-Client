using System.Collections;
using System.Xml;
using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Contracts.Read;
using EasySoapClient.Interfaces;
using EasySoapClient.Logging;
using EasySoapClient.Models;
using EasySoapClient.Serialization;
using Microsoft.Extensions.Logging;

namespace EasySoapClient.Services;

/// <summary>
/// Builds SOAP envelopes with <see cref="XmlWriter"/>, so every value and name is escaped and validated.
/// </summary>
internal sealed class SoapEnvelopeService(ILogger<SoapEnvelopeService> logger) : ISoapEnvelopeService
{
    private const string EnvelopePrefix = "soapenv";
    private const string ServicePrefix = "wsns";

    private static readonly XmlWriterSettings WriterSettings = new()
    {
        OmitXmlDeclaration = true,
        Indent = false,
        CheckCharacters = true,
    };

    private readonly ILogger<SoapEnvelopeService> _logger = logger;

    public string CreateReadMultipleEnvelope(string serviceName, IReadOnlyCollection<ReadMultipleFilter> filters, int size, string? bookmarkKey)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        // Element order follows the WSDL sequence: filter*, bookmarkKey?, setSize.
        return BuildPageEnvelope(serviceName, SoapOperations.ReadMultiple, (writer, ns) =>
        {
            foreach (ReadMultipleFilter filter in filters)
            {
                if (String.IsNullOrEmpty(filter.Field))
                {
                    throw new ArgumentException("A filter must have a field name.", nameof(filters));
                }

                writer.WriteStartElement(ServicePrefix, "filter", ns);
                WriteTextElement(writer, "Field", ns, filter.Field);
                WriteTextElement(writer, "Criteria", ns, filter.Criteria ?? String.Empty);
                writer.WriteEndElement();
            }

            if (!String.IsNullOrEmpty(bookmarkKey))
            {
                WriteTextElement(writer, "bookmarkKey", ns, bookmarkKey);
            }

            WriteTextElement(writer, "setSize", ns, SoapValueFormatter.Format(size));
        });
    }

    public string CreateReadEnvelope(string serviceName, ReadRequest request)
    {
        if (request.Parameters is null || request.Parameters.Count == 0)
        {
            throw new ArgumentException("A Read request must contain at least one parameter.", nameof(request));
        }

        return BuildPageEnvelope(serviceName, SoapOperations.Read, (writer, ns) =>
        {
            foreach (KeyValuePair<string, object?> parameter in request.Parameters)
            {
                WriteTextElement(writer, parameter.Key, ns, parameter.Value is null ? String.Empty : SoapValueFormatter.Format(parameter.Value));
            }
        });
    }

    public string CreateReadByRecIdEnvelope(string serviceName, string recId)
    {
        ArgumentException.ThrowIfNullOrEmpty(recId);

        return BuildPageEnvelope(serviceName, SoapOperations.ReadByRecId, (writer, ns) => WriteTextElement(writer, "recId", ns, recId));
    }

    public string CreateCreateEnvelope<T>(T item) where T : IWebServiceElement, new()
    {
        ArgumentNullException.ThrowIfNull(item);
        string serviceName = ServiceMetadata<T>.ServiceName;

        return BuildPageEnvelope(serviceName, SoapOperations.Create, (writer, ns) =>
            WriteRecord(writer, serviceName, ns, item, includeKey: false));
    }

    public string CreateCreateMultipleEnvelope<T>(IReadOnlyCollection<T> items) where T : IWebServiceElement, new()
    {
        ArgumentNullException.ThrowIfNull(items);
        string serviceName = ServiceMetadata<T>.ServiceName;

        return BuildPageEnvelope(serviceName, SoapOperations.CreateMultiple, (writer, ns) =>
        {
            writer.WriteStartElement(ServicePrefix, $"{serviceName}_List", ns);
            foreach (T item in items)
            {
                ArgumentNullException.ThrowIfNull(item, nameof(items));
                WriteRecord(writer, serviceName, ns, item, includeKey: false);
            }

            writer.WriteEndElement();
        });
    }

    public string CreateUpdateEnvelope<T>(T item) where T : IKeyedWebServiceElement, new()
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureKey(item, nameof(item));
        string serviceName = ServiceMetadata<T>.ServiceName;

        return BuildPageEnvelope(serviceName, SoapOperations.Update, (writer, ns) =>
            WriteRecord(writer, serviceName, ns, item, includeKey: true));
    }

    public string CreateUpdateMultipleEnvelope<T>(IReadOnlyCollection<T> items) where T : IKeyedWebServiceElement, new()
    {
        ArgumentNullException.ThrowIfNull(items);
        string serviceName = ServiceMetadata<T>.ServiceName;

        foreach (T item in items)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(items));
            EnsureKey(item, nameof(items));
        }

        return BuildPageEnvelope(serviceName, SoapOperations.UpdateMultiple, (writer, ns) =>
        {
            writer.WriteStartElement(ServicePrefix, $"{serviceName}_List", ns);
            foreach (T item in items)
            {
                WriteRecord(writer, serviceName, ns, item, includeKey: true);
            }

            writer.WriteEndElement();
        });
    }

    public string CreateKeyOperationEnvelope(string serviceName, string operation, string key)
    {
        if (String.IsNullOrEmpty(key))
        {
            throw new ArgumentException($"A key is required for the '{operation}' operation.", nameof(key));
        }

        return BuildPageEnvelope(serviceName, operation, (writer, ns) => WriteTextElement(writer, "Key", ns, key));
    }

    public string CreateCodeUnitMethodInvocationEnvelope(CodeUnitRequest request)
    {
        if (String.IsNullOrWhiteSpace(request.CodeUnitName))
        {
            throw new ArgumentException("The codeunit name must be set.", nameof(request));
        }

        if (String.IsNullOrWhiteSpace(request.MethodName))
        {
            throw new ArgumentException("The method name must be set.", nameof(request));
        }

        string envelope = BuildEnvelope(request.GenerateNamespace(), request.MethodName, (writer, ns) =>
        {
            foreach (CodeUnitParameter parameter in request.Parameters ?? [])
            {
                string value = parameter.ParameterValue is null ? String.Empty : SoapValueFormatter.Format(parameter.ParameterValue);
                WriteTextElement(writer, parameter.ParameterName, ns, value);
            }
        });

        Log.EnvelopeCreated(_logger, "Codeunit", request.CodeUnitName, request.MethodName, envelope);
        return envelope;
    }

    private string BuildPageEnvelope(string serviceName, string operation, Action<XmlWriter, string> writeBody)
    {
        string envelope = BuildEnvelope(SoapNames.PageNamespace(serviceName), operation, writeBody);
        Log.EnvelopeCreated(_logger, "Page", serviceName, operation, envelope);
        return envelope;
    }

    private static string BuildEnvelope(string serviceNamespace, string operation, Action<XmlWriter, string> writeBody)
    {
        using var stringWriter = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        using (XmlWriter writer = XmlWriter.Create(stringWriter, WriterSettings))
        {
            writer.WriteStartElement(EnvelopePrefix, "Envelope", SoapNames.SoapEnvelopeNamespace);
            writer.WriteAttributeString("xmlns", ServicePrefix, null, serviceNamespace);
            writer.WriteStartElement(EnvelopePrefix, "Header", SoapNames.SoapEnvelopeNamespace);
            writer.WriteEndElement();
            writer.WriteStartElement(EnvelopePrefix, "Body", SoapNames.SoapEnvelopeNamespace);
            writer.WriteStartElement(ServicePrefix, VerifyName(operation), serviceNamespace);

            writeBody(writer, serviceNamespace);

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        return stringWriter.ToString();
    }

    private static void WriteRecord(XmlWriter writer, string elementName, string ns, object record, bool includeKey)
        => WriteRecord(writer, elementName, ns, record, includeKey, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static void WriteRecord(XmlWriter writer, string elementName, string ns, object record, bool includeKey, HashSet<object> path)
    {
        // A record that contains itself would recurse forever (a stack overflow cannot be caught).
        if (!record.GetType().IsValueType && !path.Add(record))
        {
            throw new InvalidOperationException($"A circular reference was detected while writing '{elementName}' ({record.GetType().Name}).");
        }

        writer.WriteStartElement(ServicePrefix, VerifyName(elementName), ns);
        TypeMetadata metadata = TypeMetadata.For(record.GetType());

        // The key identifies the record on update; write it first.
        if (includeKey)
        {
            foreach (PropertyMetadata property in metadata.Properties.Where(p => p.IsKey))
            {
                WriteProperty(writer, ns, record, property, path);
            }
        }

        foreach (PropertyMetadata property in metadata.Properties.Where(p => !p.IsKey))
        {
            WriteProperty(writer, ns, record, property, path);
        }

        writer.WriteEndElement();
        path.Remove(record);
    }

    private static void WriteProperty(XmlWriter writer, string ns, object record, PropertyMetadata property, HashSet<object> path)
    {
        if (property.ShouldSerialize is not null && !property.ShouldSerialize(record))
        {
            return;
        }

        object? value = property.Getter(record);

        // Null means "not set": the element is left out, so the service keeps its current / default value.
        // An empty key (e.g. a new subpage line) is left out for the same reason.
        if (value is null || (property.IsKey && value is string { Length: 0 }))
        {
            return;
        }

        if (SoapValueFormatter.IsSimpleType(value.GetType()))
        {
            WriteTextElement(writer, property.ElementName, ns, SoapValueFormatter.Format(value, property.DataType));
            return;
        }

        if (property.IsCollection && value is IEnumerable collection)
        {
            if (!property.IsFlatCollection)
            {
                writer.WriteStartElement(ServicePrefix, VerifyName(property.ArrayElementName ?? property.ElementName), ns);
            }

            foreach (object? child in collection)
            {
                if (child is null)
                {
                    continue;
                }

                string childName = property.IsFlatCollection
                    ? property.ElementName
                    : property.ArrayItemElementName ?? SoapValueFormatter.DefaultElementName(child.GetType());

                if (SoapValueFormatter.IsSimpleType(child.GetType()))
                {
                    WriteTextElement(writer, childName, ns, SoapValueFormatter.Format(child));
                }
                else
                {
                    WriteRecord(writer, childName, ns, child, includeKey: true, path);
                }
            }

            if (!property.IsFlatCollection)
            {
                writer.WriteEndElement();
            }

            return;
        }

        // Nested record (e.g. a subpage line). Its key is included so existing lines can be matched.
        WriteRecord(writer, property.ElementName, ns, value, includeKey: true, path);
    }

    private static void WriteTextElement(XmlWriter writer, string name, string ns, string value)
    {
        writer.WriteStartElement(ServicePrefix, VerifyName(name), ns);
        writer.WriteString(value);
        writer.WriteEndElement();
    }

    private static string VerifyName(string? name)
    {
        if (String.IsNullOrEmpty(name))
        {
            throw new ArgumentException("An XML element name cannot be empty.", nameof(name));
        }

        try
        {
            return XmlConvert.VerifyNCName(name);
        }
        catch (XmlException ex)
        {
            throw new ArgumentException($"'{name}' is not a valid XML element name.", nameof(name), ex);
        }
    }

    private static void EnsureKey(IKeyedWebServiceElement item, string parameterName)
    {
        if (String.IsNullOrEmpty(item.Key))
        {
            throw new ArgumentException("The 'Key' property must be set for update operations.", parameterName);
        }
    }
}
