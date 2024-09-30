using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace EasySoapClient.Services;

public class SoapEnvelopeService : ISoapEnvelopeService
{
    public virtual string CreateReadMultipleEnvelope<T>(IEnumerable<ReadMultipleFilter> filters, int size, string? bookmarkKey, T serviceElement) where T : IWebServiceElement
    {
        var soapMessage = new StringBuilder();

        soapMessage.Append($@"
        <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsns=""{serviceElement.Namespace}"">
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

        return soapMessage.ToString();
    }


    public virtual string CreateCreateEnvelope<T>(T item) where T : IWebServiceElement
    {
        StringBuilder soapMessage = new();
        soapMessage.Append($@"
        <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsns=""{item.Namespace}"">
            <soapenv:Header/>
            <soapenv:Body>
                <wsns:Create>
                    <wsns:{item.ServiceName}>");

        // Get all properties of the item using reflection.
        var properties = typeof(T).GetProperties();

        foreach (var property in properties)
        {
            if (property.Name == nameof(IWebServiceElement.ServiceName) || property.Name == nameof(IWebServiceElement.Namespace))
            {
                continue;
            }

            var xmlElementAttribute = property.GetCustomAttribute<XmlElementAttribute>();
            string elementName = xmlElementAttribute?.ElementName ?? property.Name;

            // Get the property value.
            var value = property.GetValue(item);

            // Check if the property is DateTime or Nullable<DateTime> and format it
            if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
            {
                if (value is DateTime dateTimeValue)
                {
                    // Format the DateTime value to the required ISO 8601 format
                    value = dateTimeValue.ToString("yyyy-MM-ddTHH:mm:ss");
                }
            }

            // Append the element to the soap message, formatting value as string if necessary
            soapMessage.Append($@"<wsns:{elementName}>{value ?? String.Empty}</wsns:{elementName}>");
        }

        soapMessage.Append($@"
                    </wsns:{item.ServiceName}>
                </wsns:Create>
            </soapenv:Body>
        </soapenv:Envelope>");

        return soapMessage.ToString();
    }

}
