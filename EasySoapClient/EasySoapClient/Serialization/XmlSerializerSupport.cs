using System.Collections;
using System.Reflection;
using System.Xml.Serialization;

namespace EasySoapClient.Serialization;

/// <summary>
/// Guards against model types that XmlSerializer on the current runtime would read incorrectly.
/// </summary>
internal static class XmlSerializerSupport
{
    private static readonly Lazy<bool> DateOnlySupported = new(ProbeDateOnlySupport);

    /// <summary>
    /// Whether XmlSerializer on this runtime reads <see cref="DateOnly"/> and <see cref="TimeOnly"/>.
    /// On .NET 8 it silently leaves them at their default value.
    /// </summary>
    public static bool SupportsDateOnlyAndTimeOnly => DateOnlySupported.Value;

    /// <exception cref="NotSupportedException">The type (or a nested type) uses DateOnly/TimeOnly and the runtime cannot read them.</exception>
    public static void EnsureSupported(Type type)
    {
        if (SupportsDateOnlyAndTimeOnly)
        {
            return;
        }

        if (FindDateOnlyOrTimeOnlyMember(type, []) is { } member)
        {
            throw new NotSupportedException(
                $"{member.Member.DeclaringType!.Name}.{member.Member.Name} is {member.Type.Name}, which XmlSerializer on this runtime " +
                $"(.NET {Environment.Version}) cannot read: it would silently become its default value. Use DateTime with " +
                "[XmlElement(DataType = \"date\")] (or \"time\"), or a runtime whose XmlSerializer supports it (.NET 10).");
        }
    }

    private static (MemberInfo Member, Type Type)? FindDateOnlyOrTimeOnlyMember(Type type, HashSet<Type> visited)
    {
        if (!visited.Add(type))
        {
            return null;
        }

        foreach ((MemberInfo member, Type memberType) in SerializedMembers(type))
        {
            Type elementType = ElementType(memberType);
            if (elementType == typeof(DateOnly) || elementType == typeof(TimeOnly))
            {
                return (member, elementType);
            }

            if (!SoapValueFormatter.IsSimpleType(elementType) && FindDateOnlyOrTimeOnlyMember(elementType, visited) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// The members XmlSerializer reads: public writable fields, public read/write properties,
    /// and get-only collection properties (which it fills).
    /// </summary>
    private static IEnumerable<(MemberInfo Member, Type Type)> SerializedMembers(Type type)
    {
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!field.IsInitOnly && !field.IsDefined(typeof(XmlIgnoreAttribute), inherit: true))
            {
                yield return (field, field.FieldType);
            }
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Same rule as for sending (including getter-only overrides / new over a settable base), plus
            // [XmlAttribute] properties, which are not sent but are read.
            bool attribute = property.IsDefined(typeof(XmlAttributeAttribute), inherit: true)
                && !property.IsDefined(typeof(XmlIgnoreAttribute), inherit: true)
                && property.GetMethod is { IsPublic: true }
                && property.SetMethod is { IsPublic: true };

            if (property.GetIndexParameters().Length == 0 && (attribute || TypeMetadata.IsSerializedProperty(type, property)))
            {
                yield return (property, property.PropertyType);
            }
        }
    }

    private static Type ElementType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type != typeof(string) && type != typeof(byte[]) && typeof(IEnumerable).IsAssignableFrom(type))
        {
            Type? element = type.IsArray
                ? type.GetElementType()
                : type.GetInterfaces().Append(type)
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?
                    .GetGenericArguments()[0];

            if (element is not null)
            {
                return Nullable.GetUnderlyingType(element) ?? element;
            }
        }

        return type;
    }

    // Runs once. XmlRootAttribute is needed for a primitive root; the single generated assembly is acceptable.
    private static bool ProbeDateOnlySupport()
    {
        try
        {
            var serializer = new XmlSerializer(typeof(DateOnly), new XmlRootAttribute("value"));
            using var text = new StringReader("<value>2001-02-03</value>");
            using var reader = System.Xml.XmlReader.Create(text, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null });
            return serializer.Deserialize(reader) is DateOnly { Year: 2001, Month: 2, Day: 3 };
        }
        catch (Exception)
        {
            // Any failure (e.g. a trimmed app) means "not supported": models with these types are then rejected
            // instead of risking silent data loss.
            return false;
        }
    }
}
