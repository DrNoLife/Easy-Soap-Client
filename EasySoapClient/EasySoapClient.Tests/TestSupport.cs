using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EasySoapClient.Tests;

public class Customer : IKeyedWebServiceElement
{
    public string ServiceName => "Customer";

    [XmlElement(ElementName = "Key")]
    public string Key { get; set; } = String.Empty;

    [XmlElement(ElementName = "No")]
    public string No { get; set; } = String.Empty;

    [XmlElement(ElementName = "Name")]
    public string? Name { get; set; }

    public decimal? Balance { get; set; }

    public bool? Blocked { get; set; }

    public DateTime? LastModified { get; set; }

    // Not a field: get-only and ignored properties must never be sent.
    public string Namespace => "urn:old-readme-style";

    public string DisplayName => $"{No} {Name}";

    [XmlIgnore]
    public string Computed { get; set; } = "never-sent";
}

public class AllTypes : IWebServiceElement
{
    public string ServiceName => "AllTypes";
    public decimal Amount { get; set; }
    public double Ratio { get; set; }
    public bool Flag { get; set; }
    public DateTime When { get; set; }
    [XmlElement(DataType = "date")]
    public DateTime DateOnlyField { get; set; }
    public int Count { get; set; }
    public Color Colour { get; set; }
    public Guid Id { get; set; }

    public int Quantity { get; set; }
    [XmlIgnore]
    public bool QuantitySpecified { get; set; }
}

public class Dates : IWebServiceElement
{
    public string ServiceName => "Dates";
    public DateOnly Day { get; set; }
    public TimeOnly? Time { get; set; }
}

[Flags]
public enum Permissions
{
    None = 0,
    Read = 1,
    [XmlEnum("Write_Access")]
    Write = 2,
}

public class FlagsModel : IWebServiceElement
{
    public string ServiceName => "FlagsModel";
    public Permissions Permissions { get; set; }
}

public class UnnamedArrays : IWebServiceElement
{
    public string ServiceName => "UnnamedArrays";

    [XmlArray]
    public List<SalesLine>? Lines { get; set; }

    [XmlArray("Other")]
    [XmlArrayItem(typeof(SalesLine))]
    public List<SalesLine>? OtherLines { get; set; }
}

public class WithFields : IWebServiceElement
{
    public string ServiceName => "WithFields";
    public string? Name;
    [XmlElement("Amount_LCY")]
    public decimal? Amount;
    public readonly string NotSent = "x";
}

public class DateOnlyField : IWebServiceElement
{
    public string ServiceName => "DateOnlyField";
    public DateOnly Day;
}

public class ComputedDateOnly : IWebServiceElement
{
    public string ServiceName => "ComputedDateOnly";
    public DateTime When { get; set; }
    public DateOnly Day => DateOnly.FromDateTime(When);
}

[XmlType("Sales_Line")]
public class TypedLine
{
    public string? No { get; set; }
}

public class ArrayNames : IWebServiceElement
{
    public string ServiceName => "ArrayNames";

    [XmlArray("Tags")]
    [XmlArrayItem]
    public List<string>? Tags { get; set; }

    [XmlArray("Lines")]
    [XmlArrayItem(typeof(TypedLine))]
    public List<TypedLine>? Lines { get; set; }
}

public class FieldSpecified : IWebServiceElement
{
    public string ServiceName => "FieldSpecified";
    public decimal Amount;
    [XmlIgnore]
    public bool AmountSpecified;
}

[Flags]
public enum SignedFlags
{
    A = 1,
    High = int.MinValue,
}

public class BaseModel : IWebServiceElement
{
    public string ServiceName => "BaseModel";
    public virtual string? First { get; set; }
    public string? Second { get; set; }
}

public class DerivedModel : BaseModel
{
    public string? Third { get; set; }
    public override string? First { get; set; }
}

public class GetOnlyLines : IWebServiceElement
{
    public string ServiceName => "GetOnlyLines";

    [XmlArray("Lines")]
    [XmlArrayItem("Line")]
    public List<SalesLine> Lines { get; } = [];
}

public class HidingBase : IWebServiceElement
{
    public string ServiceName => "Hiding";
    public bool FooSpecified;
    public string? Foo { get; set; }
    public virtual string? Bar { get; set; }
}

public class HidingDerived : HidingBase
{
    public new bool FooSpecified { get; set; }
    public override string? Bar => "computed";
}

public class Node : IWebServiceElement
{
    public string ServiceName => "Node";
    public string? Name { get; set; }
    public Node? Child { get; set; }
}

public class ComputedViews : IWebServiceElement
{
    public string ServiceName => "ComputedViews";
    public List<SalesLine> Lines { get; } = [];
    public IReadOnlyCollection<SalesLine> Open => [.. Lines.Where(l => l.Quantity > 0)];
    public IEnumerable<string> Names => Lines.Select(l => l.No ?? "");
    public Dictionary<string, string> Extra { get; } = [];
}

public class NewHidingBase : IWebServiceElement
{
    public string ServiceName => "NewHiding";
    public string? P1 { get; set; }
    public string? P2 { get; set; }
    public string? Q { get; set; }
}

public class NewHidingDerived : NewHidingBase
{
    public string? P3 { get; set; }
    public new string? P1 { get; set; }
    public new string Q => "derived";
}

public class GetOnlyBase : IWebServiceElement
{
    public string ServiceName => "GetOnlyBase";
    public string? A { get; set; }
    public string N => "base";
    public string? Z { get; set; }
}

public class SettableOverGetOnly : GetOnlyBase
{
    public string? Y { get; set; }
    public new string? N { get; set; }
}

public class DateBase : IWebServiceElement
{
    public string ServiceName => "DateBase";
    public virtual DateOnly Day { get; set; }
}

public class GetterOnlyDateOverride : DateBase
{
    public override DateOnly Day => new(2026, 1, 1);
}

public class DateList : IWebServiceElement
{
    public string ServiceName => "DateList";
    [XmlArray("Days")]
    [XmlArrayItem]
    public List<DateOnly>? Days { get; set; }
}

public class AttributeDate : IWebServiceElement
{
    public string ServiceName => "AttributeDate";
    [XmlAttribute]
    public DateOnly Day { get; set; }
}

public class BadServiceName : IWebServiceElement
{
    public string ServiceName => " ";
}

public enum Color
{
    Red,
    [XmlEnum("Dark_Blue")]
    DarkBlue,
}

public class SalesOrder : IKeyedWebServiceElement
{
    public string ServiceName => "SalesOrder";
    public string Key { get; set; } = String.Empty;
    public string? No { get; set; }

    [XmlArray("SalesLines")]
    [XmlArrayItem("Sales_Order_Line")]
    public List<SalesLine>? Lines { get; set; }
}

public class SalesLine
{
    public string Key { get; set; } = String.Empty;
    public string? No { get; set; }
    public decimal? Quantity { get; set; }
}

public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public CultureScope(string name)
    {
        CultureInfo.CurrentCulture = new CultureInfo(name);
        CultureInfo.CurrentUICulture = new CultureInfo(name);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}

public sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? SoapAction, string? Authorization, string? ContentType, string Body);

/// <summary>
/// Records requests and answers with a canned response.
/// </summary>
public sealed class FakeHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Like a real handler: honour cancellation.
        cancellationToken.ThrowIfCancellationRequested();
        string body = request.Content is null ? String.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.TryGetValues("SOAPAction", out var actions) ? actions.Single() : null,
            request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.ToString(),
            body);
        Requests.Add(recorded);
        return respond(recorded);
    }

    public static HttpResponseMessage Xml(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };
}

public static class Soap
{
    public const string EnvelopeNs = "http://schemas.xmlsoap.org/soap/envelope/";
    public static readonly XNamespace Customer = "urn:microsoft-dynamics-schemas/page/customer";

    public static string Envelope(string bodyContent)
        => $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Soap:Envelope xmlns:Soap=\"{EnvelopeNs}\"><Soap:Body>{bodyContent}</Soap:Body></Soap:Envelope>";

    public static string CustomerXml(string key, string no, string name)
        => $"<Customer><Key>{key}</Key><No>{no}</No><Name>{name}</Name></Customer>";

    public static string ReadMultipleResult(params string[] customers)
        => Envelope($"<ReadMultiple_Result xmlns=\"{Customer}\"><ReadMultiple_Result>{String.Concat(customers)}</ReadMultiple_Result></ReadMultiple_Result>");

    public static string Fault(string faultString)
        => Envelope($"<s:Fault xmlns:s=\"{EnvelopeNs}\"><faultcode xmlns:a=\"urn:microsoft-dynamics-schemas/error\">a:Microsoft.Dynamics.Nav.Types.Exceptions.NavCSideRecordNotFoundException</faultcode><faultstring xml:lang=\"en-US\">{faultString}</faultstring></s:Fault>");
}

internal static class TestServices
{
    public static ServiceProvider Build(FakeHandler handler, Action<EasySoapClientOptions>? configure = null, string? key = null, Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        void Configure(EasySoapClientOptions options)
        {
            options.BaseUri = "https://nav.example.com/BC/WS/CRONUS Danmark";
            options.Username = "user";
            options.Password = "pass";
            configure?.Invoke(options);
        }

        if (key is null)
        {
            services.AddEasySoapClient(Configure, http => http.ConfigurePrimaryHttpMessageHandler(() => handler));
        }
        else
        {
            services.AddKeyedEasySoapClient(key, Configure, http => http.ConfigurePrimaryHttpMessageHandler(() => handler));
        }

        extra?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public static Services.SoapEnvelopeService Envelopes() => new(NullLogger<Services.SoapEnvelopeService>.Instance);

    public static Services.ParsingService Parser() => new(NullLogger<Services.ParsingService>.Instance, new Services.XmlSanitizerService());

    public static MemoryStream Stream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
