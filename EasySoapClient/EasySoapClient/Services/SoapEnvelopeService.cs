using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Extensions;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Reflection;
using System.Security;
using System.Text;
using System.Xml.Serialization;

namespace EasySoapClient.Services;

public class SoapEnvelopeService(ILogger<SoapEnvelopeService> logger) : ISoapEnvelopeService
{
    private readonly ILogger<SoapEnvelopeService> _logger = logger;

    public virtual string CreateReadMultipleEnvelope<T>(IEnumerable<ReadMultipleFilter> filters, int size, string? bookmarkKey, T serviceElement) 
        where T : IWebServiceElement
    {
        StringBuilder soapMessage = new();

        soapMessage.Append($@"
        <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsns=""{serviceElement.GetXmlNamespace()}"">
            <soapenv:Header/>
            <soapenv:Body>
                <wsns:ReadMultiple>");

        // Append each filter
        foreach (var filter in filters)
        {
            soapMessage.Append($@"
                    <wsns:filter>
                        <wsns:Field>{filter.Field}</wsns:Field>
                        <wsns:Criteria>{filter.Criteria}</wsns:Criteria>
                    </wsns:filter>");
        }

        // Add size and bookmarkKey
        soapMessage.Append($@"
                    <wsns:setSize>{size}</wsns:setSize>");

        if (!String.IsNullOrEmpty(bookmarkKey))
        {
            soapMessage.Append($@"<wsns:bookmarkKey>{bookmarkKey}</wsns:bookmarkKey>");
        }

        soapMessage.Append(@"
                </wsns:ReadMultiple>
            </soapenv:Body>
        </soapenv:Envelope>");

        _logger.LogDebug("Soap envelope created. \n{Envelope}", soapMessage);

        return soapMessage.ToString();
    }

    public virtual string CreateReadByIdEnvelope<T>(string id)
        where T : ISearchable, new()
    {
        ArgumentException.ThrowIfNullOrEmpty(id, "The 'Id' property must be set for this operation.");

        var instance = new T();

        StringBuilder soapMessage = new();

        soapMessage.Append($@"
        <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsns=""{instance.GetXmlNamespace()}"">
            <soapenv:Header/>
            <soapenv:Body>
                <wsns:Read>
                    <wsns:ID>{id}</wsns:ID>
                </wsns:Read>
            </soapenv:Body>
        </soapenv:Envelope>");

        _logger.LogDebug("Soap envelope created. \n{Envelope}", soapMessage);

        return soapMessage.ToString();
    }

    public virtual string CreateCreateEnvelope<T>(T item) 
        where T : IWebServiceElement
    {
        StringBuilder soapMessage = new();
        soapMessage.Append($@"
        <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsns=""{item.GetXmlNamespace()}"">
            <soapenv:Header/>
            <soapenv:Body>
                <wsns:Create>
                    <wsns:{item.ServiceName}>");

        // Get all properties of the item using reflection.
        var properties = typeof(T).GetProperties();

        foreach (var property in properties)
        {
            if (property.Name == nameof(IWebServiceElement.ServiceName))
            {
                continue;
            }

            var xmlElementAttribute = property.GetCustomAttribute<XmlElementAttribute>();
            string elementName = xmlElementAttribute?.ElementName ?? property.Name;

            // Get the formatted property value.
            var value = property.FormatNavisionValue(item);

            // Append the element to the soap message, formatting value as string if necessary
            soapMessage.Append($@"<wsns:{elementName}>{value ?? String.Empty}</wsns:{elementName}>");
        }

        soapMessage.Append($@"
                    </wsns:{item.ServiceName}>
                </wsns:Create>
            </soapenv:Body>
        </soapenv:Envelope>");

        _logger.LogDebug("SOAP Create envelope created. \n{Envelope}", soapMessage);

        return soapMessage.ToString();
    }

    public virtual string CreateUpdateEnvelope<T>(T item) 
        where T : IUpdatableWebServiceElement
    {
        if (String.IsNullOrEmpty(item.Key))
        {
            throw new ArgumentException("The 'Key' property must be set for update operations.");
        }

        StringBuilder soapMessage = new();
        soapMessage.Append($@"
        <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsns=""{item.GetXmlNamespace()}"">
            <soapenv:Header/>
            <soapenv:Body>
                <wsns:Update>
                    <wsns:{item.ServiceName}>");

        // Get all properties of the item using reflection.
        var properties = typeof(T).GetProperties();

        foreach (var property in properties)
        {
            if (property.Name == nameof(IWebServiceElement.ServiceName) || property.Name == nameof(IUpdatableWebServiceElement.Key))
            {
                continue;
            }

            var xmlElementAttribute = property.GetCustomAttribute<XmlElementAttribute>();
            string elementName = xmlElementAttribute?.ElementName ?? property.Name;

            // Get the formatted property value.
            var value = property.FormatNavisionValue(item);

            // Append the element to the soap message, formatting value as string if necessary
            soapMessage.Append($@"<wsns:{elementName}>{SecurityElement.Escape(value?.ToString() ?? String.Empty)}</wsns:{elementName}>");
        }

        soapMessage.Append($@"
                    </wsns:{item.ServiceName}>
                </wsns:Update>
            </soapenv:Body>
        </soapenv:Envelope>");

        _logger.LogDebug("SOAP Update envelope created. \n{Envelope}", soapMessage);

        return soapMessage.ToString();
    }

    public virtual string CreateGetIdEnvelope<T>(string key)
        where T : ISearchable, new()
    {
        if (String.IsNullOrEmpty(key))
        {
            throw new ArgumentException("The 'Key' property must be set for update operations.");
        }

        var instance = new T();

        StringBuilder soapMessage = new();
        soapMessage.Append($@"
        <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsns=""{instance.GetXmlNamespace()}"">
            <soapenv:Header/>
            <soapenv:Body>
                <wsns:GetRecIdFromKey>
                    <wsns:Key>{key}</wsns:Key>
                </wsns:GetRecIdFromKey>
            </soapenv:Body>
        </soapenv:Envelope>");

        _logger.LogDebug("SOAP Update envelope created. \n{Envelope}", soapMessage);

        return soapMessage.ToString();
    }

    public virtual string CreateCodeUnitMethodInvocationEnvelope(CodeUnitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        StringBuilder soapMessage = new();
        soapMessage.Append($@"
        <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsns=""urn:microsoft-dynamics-schemas/codeunit/{request.CodeUnitName}"">
            <soapenv:Header/>
            <soapenv:Body>
                <wsns:{request.MethodName}>");

        foreach (var parameter in request.Parameters)
        {
            soapMessage.Append($@"<wsns:{parameter.ParameterName}>{parameter.ParameterValue}</wsns:{parameter.ParameterName}>");
        }

        soapMessage.Append($@"
                </wsns:{request.MethodName}>
            </soapenv:Body>
        </soapenv:Envelope>");

        _logger.LogDebug("SOAP code unit envelope created. \n{Envelope}", soapMessage);

        return soapMessage.ToString();
    }
}
