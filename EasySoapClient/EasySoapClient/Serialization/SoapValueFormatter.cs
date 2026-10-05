using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace EasySoapClient.Serialization;

/// <summary>
/// Formats values for SOAP envelopes using XML Schema formats, independent of the current culture.
/// </summary>
internal static class SoapValueFormatter
{
    private static readonly ConcurrentDictionary<Enum, string> EnumNames = new();

    /// <summary>
    /// Whether values of the type are written as a single text element (as opposed to nested elements).
    /// </summary>
    public static bool IsSimpleType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type == typeof(byte[])
            || type == typeof(object);
    }

    /// <summary>
    /// The element name XmlSerializer uses for a collection item without an explicit name:
    /// the XSD name for primitives (e.g. <c>string</c>, <c>int</c>), otherwise the [XmlType] name or the type name.
    /// </summary>
    public static string DefaultElementName(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (XsdNames.TryGetValue(type, out string? xsdName))
        {
            return xsdName;
        }

        XmlTypeAttribute? xmlType = type.GetCustomAttribute<XmlTypeAttribute>();
        return String.IsNullOrEmpty(xmlType?.TypeName) ? type.Name : xmlType.TypeName;
    }

    private static readonly Dictionary<Type, string> XsdNames = new()
    {
        [typeof(string)] = "string",
        [typeof(bool)] = "boolean",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(short)] = "short",
        [typeof(sbyte)] = "byte",
        [typeof(byte)] = "unsignedByte",
        [typeof(uint)] = "unsignedInt",
        [typeof(ulong)] = "unsignedLong",
        [typeof(ushort)] = "unsignedShort",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(DateTime)] = "dateTime",
        [typeof(DateOnly)] = "dateOnly",
        [typeof(TimeOnly)] = "timeOnly",
        [typeof(char)] = "char",
        [typeof(Guid)] = "guid",
        [typeof(byte[])] = "base64Binary",
    };

    /// <param name="value">The value. Must not be null.</param>
    /// <param name="dataType">The XML Schema data type from <see cref="XmlElementAttribute.DataType"/>, if any.</param>
    public static string Format(object value, string? dataType = null) => value switch
    {
        string text => text,
        bool boolean => XmlConvert.ToString(boolean),
        DateTime dateTime => FormatDateTime(dateTime, dataType),
        DateTimeOffset dateTimeOffset => XmlConvert.ToString(dateTimeOffset),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        TimeSpan timeSpan => XmlConvert.ToString(timeSpan),
        decimal number => XmlConvert.ToString(number),
        double number => XmlConvert.ToString(number),
        float number => XmlConvert.ToString(number),
        Guid guid => guid.ToString("D"),
        byte[] bytes => Convert.ToBase64String(bytes),
        char character => XmlConvert.ToString((ushort)character), // XmlSerializer writes a char as its number
        Enum enumValue => FormatEnum(enumValue),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? String.Empty,
    };

    /// <summary>
    /// <c>yyyy-MM-ddTHH:mm:ss</c>: whole seconds and no time zone, exactly as 2.x sent it, regardless of
    /// <see cref="DateTime.Kind"/>. Use <see cref="DateTimeOffset"/> to send an explicit offset.
    /// </summary>
    private static string FormatDateTime(DateTime dateTime, string? dataType) => dataType switch
    {
        "date" => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "time" => dateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        _ => dateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
    };

    private static string FormatEnum(Enum value) => EnumNames.GetOrAdd(value, static v =>
    {
        Type type = v.GetType();

        // XmlSerializer writes [Flags] combinations space separated ("A B"), not "A, B".
        if (type.IsDefined(typeof(FlagsAttribute), inherit: false))
        {
            // No flags set and no member named for 0: XmlSerializer writes an empty value.
            if (!Enum.IsDefined(type, v) && v.Equals(Enum.ToObject(type, 0)))
            {
                return String.Empty;
            }

            return String.Join(' ', v.ToString().Split(", ").Select(name => EnumMemberName(type, name)));
        }

        // Like XmlSerializer: a value without a member cannot be written by name.
        if (!Enum.IsDefined(type, v))
        {
            throw new ArgumentException($"{v} is not a defined value of {type.Name}.", nameof(value));
        }

        return EnumMemberName(type, v.ToString());
    });

    private static string EnumMemberName(Type type, string name)
    {
        FieldInfo? field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
        XmlEnumAttribute? xmlEnum = field?.GetCustomAttribute<XmlEnumAttribute>();

        return xmlEnum?.Name ?? name;
    }
}
