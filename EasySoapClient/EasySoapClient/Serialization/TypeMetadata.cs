using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Serialization;
using EasySoapClient.Interfaces;

namespace EasySoapClient.Serialization;

/// <summary>
/// The members of a model that are written to Create / Update envelopes, built once per type.
/// Mirrors what XmlSerializer writes: public read/write properties and public writable fields.
/// </summary>
internal sealed class TypeMetadata
{
    private const string SpecifiedSuffix = "Specified";

    private static readonly ConcurrentDictionary<Type, TypeMetadata> Cache = new();

    public IReadOnlyList<PropertyMetadata> Properties { get; }

    private TypeMetadata(IReadOnlyList<PropertyMetadata> properties)
    {
        Properties = properties;
    }

    public static TypeMetadata For(Type type) => Cache.GetOrAdd(type, Build);

    private static TypeMetadata Build(Type type)
    {
        XmlSerializerSupport.EnsureSupported(type);

        List<Member> members = [.. DistinctByName(Members(type))];

        // "FooSpecified" (bool) is a control flag for "Foo" (XmlSerializer convention), not a field of its own.
        var specifiedFlags = members
            .Where(m => m.Type == typeof(bool) && m.Name.EndsWith(SpecifiedSuffix, StringComparison.Ordinal))
            .Where(m => members.Any(other => other.Name == m.Name[..^SpecifiedSuffix.Length]))
            .ToDictionary(m => m.Name[..^SpecifiedSuffix.Length], StringComparer.Ordinal);

        List<(PropertyMetadata Metadata, int Depth, int Token, int Order)> result = [];

        foreach (Member member in members)
        {
            if (!member.IsWritten
                || member.Name == nameof(IWebServiceElement.ServiceName)
                || specifiedFlags.ContainsValue(member))
            {
                continue;
            }

            XmlElementAttribute? xmlElement = member.Info.GetCustomAttribute<XmlElementAttribute>();
            string elementName = String.IsNullOrEmpty(xmlElement?.ElementName) ? member.Name : xmlElement.ElementName;

            var metadata = new PropertyMetadata(
                elementName,
                xmlElement?.DataType,
                IsKey: member.Name == nameof(IKeyedWebServiceElement.Key) || elementName == nameof(IKeyedWebServiceElement.Key),
                CompileGetter(type, member.Info),
                BuildShouldSerialize(type, member, specifiedFlags),
                member.Type,
                xmlElement is not null,
                NullIfEmpty(member.Info.GetCustomAttribute<XmlArrayAttribute>()?.ElementName),
                NullIfEmpty(member.Info.GetCustomAttribute<XmlArrayItemAttribute>()?.ElementName));

            result.Add((metadata, member.Depth, member.Token, xmlElement?.Order ?? -1));
        }

        // XmlSerializer order: explicit Order when used, otherwise base class members first, then declaration order
        // (fields before properties, as their metadata tokens sort that way).
        bool useExplicitOrder = result.Any(p => p.Order >= 0);
        var ordered = useExplicitOrder
            ? result.OrderBy(p => p.Order)
            : result.OrderBy(p => p.Depth).ThenBy(p => p.Token);

        return new TypeMetadata([.. ordered.Select(p => p.Metadata)]);
    }

    /// <summary>
    /// A name hidden with <c>new</c> (or a base field hidden by a property) appears more than once: the most derived
    /// member is used, at the position of the first declaration, as XmlSerializer does.
    /// </summary>
    private static IEnumerable<Member> DistinctByName(IEnumerable<Member> members)
        => members
            .GroupBy(m => m.Name, StringComparer.Ordinal)
            .Select(group =>
            {
                Member mostDerived = group.MaxBy(m => m.DeclaringDepth)!;

                // Position from the first declaration XmlSerializer actually writes (a readonly base field, for example, is not).
                Member first = group.Where(m => m.IsWritten).MinBy(m => (m.Depth, m.Token)) ?? group.MinBy(m => (m.Depth, m.Token))!;
                return mostDerived with { Depth = first.Depth, Token = first.Token };
            });

    private static IEnumerable<Member> Members(Type type)
    {
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            bool written = !field.IsInitOnly && !IsExcluded(field);
            int depth = InheritanceDepth(field.DeclaringType!);
            yield return new Member(field, field.Name, field.FieldType, written, depth, field.MetadataToken, depth);
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            // Overridden or hidden (new) properties are written where XmlSerializer writes them:
            // at the position of the first declaration of that name in the class hierarchy.
            PropertyInfo[] declarations = DeclarationsBaseFirst(type, property);
            bool written = IsSerializedProperty(property, declarations);

            // Position from the first declaration XmlSerializer writes (not e.g. a get-only base it skips).
            PropertyInfo first = declarations.FirstOrDefault(d => IsSerializedProperty(d, [d])) ?? property;

            yield return new Member(property, property.Name, property.PropertyType, written, InheritanceDepth(first.DeclaringType!), first.MetadataToken, InheritanceDepth(property.DeclaringType!));
        }
    }

    /// <summary>
    /// Whether XmlSerializer reads and writes the property: a public getter plus a public setter (possibly only on an
    /// overridden or hidden declaration), or a get-only concrete collection (which it fills when reading).
    /// </summary>
    internal static bool IsSerializedProperty(Type type, PropertyInfo property)
        => IsSerializedProperty(property, DeclarationsBaseFirst(type, property));

    private static bool IsSerializedProperty(PropertyInfo property, PropertyInfo[] declarations)
    {
        bool settable = property.SetMethod is { IsPublic: true } || declarations.Any(d => d.SetMethod is { IsPublic: true });
        bool getOnlyCollection = !settable && IsFillableCollectionType(property.PropertyType);
        return property.GetMethod is { IsPublic: true } && (settable || getOnlyCollection) && !IsExcluded(property);
    }

    /// <summary>
    /// Collections XmlSerializer can fill through a get-only property: concrete (not an interface or abstract type),
    /// not an array, not a dictionary.
    /// </summary>
    internal static bool IsFillableCollectionType(Type type)
        => type != typeof(string)
        && !type.IsArray
        && !type.IsInterface
        && !type.IsAbstract
        && typeof(IEnumerable).IsAssignableFrom(type)
        && !typeof(IDictionary).IsAssignableFrom(type);

    /// <summary>
    /// Every public declaration of the property's name in the class hierarchy (overridden and hidden ones included),
    /// base class first. Reflection on the derived type only returns the most derived one.
    /// </summary>
    private static PropertyInfo[] DeclarationsBaseFirst(Type type, PropertyInfo property)
    {
        List<PropertyInfo> declarations = [];
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            PropertyInfo? declared = current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .FirstOrDefault(p => p.Name == property.Name && p.GetIndexParameters().Length == 0);

            if (declared is not null)
            {
                declarations.Add(declared);
            }
        }

        declarations.Reverse();
        return [.. declarations];
    }

    private static bool IsExcluded(MemberInfo member)
        => member.IsDefined(typeof(XmlIgnoreAttribute), inherit: true)
        || member.IsDefined(typeof(XmlAttributeAttribute), inherit: true);

    // Attributes return "" (not null) when no name is given, e.g. [XmlArray] or [XmlArrayItem(typeof(Line))].
    private static string? NullIfEmpty(string? value) => String.IsNullOrEmpty(value) ? null : value;

    private static Func<object, bool>? BuildShouldSerialize(Type type, Member member, Dictionary<string, Member> specifiedFlags)
    {
        if (specifiedFlags.TryGetValue(member.Name, out Member? specified))
        {
            Func<object, object?> getter = CompileGetter(type, specified.Info);
            return instance => getter(instance) is true;
        }

        MethodInfo? method = type.GetMethod($"ShouldSerialize{member.Name}", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (method is not null && method.ReturnType == typeof(bool))
        {
            var instance = Expression.Parameter(typeof(object), "instance");
            var call = Expression.Call(Expression.Convert(instance, type), method);
            return Expression.Lambda<Func<object, bool>>(call, instance).Compile();
        }

        return null;
    }

    private static Func<object, object?> CompileGetter(Type type, MemberInfo member)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var access = Expression.MakeMemberAccess(Expression.Convert(instance, type), member);
        return Expression.Lambda<Func<object, object?>>(Expression.Convert(access, typeof(object)), instance).Compile();
    }

    private static int InheritanceDepth(Type type)
    {
        int depth = 0;
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            depth++;
        }

        return depth;
    }

    private sealed record Member(MemberInfo Info, string Name, Type Type, bool IsWritten, int Depth, int Token, int DeclaringDepth);
}

internal sealed record PropertyMetadata(
    string ElementName,
    string? DataType,
    bool IsKey,
    Func<object, object?> Getter,
    Func<object, bool>? ShouldSerialize,
    Type PropertyType,
    bool HasXmlElementAttribute,
    string? ArrayElementName,
    string? ArrayItemElementName)
{
    public bool IsCollection { get; } = PropertyType != typeof(string)
        && PropertyType != typeof(byte[])
        && typeof(IEnumerable).IsAssignableFrom(PropertyType);

    /// <summary>
    /// XmlSerializer semantics: a collection marked with [XmlElement] (and no [XmlArray]) is written
    /// as repeated elements without a wrapper element.
    /// </summary>
    public bool IsFlatCollection => IsCollection && HasXmlElementAttribute && ArrayElementName is null;
}
