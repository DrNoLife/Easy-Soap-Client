using System.Xml;
using EasySoapClient.Exceptions;

namespace EasySoapClient.Tests;

// F-04, F-09, F-15, F-20, F-25, F-29 and the illegal-character fallback.
public class ParsingServiceTests
{
    private readonly Services.ParsingService _parser = TestServices.Parser();
    private static readonly string Ns = Soap.Customer.NamespaceName;

    [Fact]
    public void ParseList_returns_all_records()
    {
        string xml = Soap.ReadMultipleResult(Soap.CustomerXml("k1", "1", "A"), Soap.CustomerXml("k2", "2", "B &amp; C"));

        List<Customer> customers = _parser.ParseList<Customer>(TestServices.Stream(xml), "Customer", Ns);

        Assert.Equal(["1", "2"], customers.Select(c => c.No));
        Assert.Equal("B & C", customers[1].Name);
        Assert.Equal("k2", customers[1].Key);
    }

    [Fact]
    public void ParseList_returns_empty_for_no_records()
    {
        Assert.Empty(_parser.ParseList<Customer>(TestServices.Stream(Soap.ReadMultipleResult()), "Customer", Ns));
    }

    [Fact]
    public void ParseSingle_reports_not_found_without_throwing()
    {
        string xml = Soap.Envelope($"<Read_Result xmlns=\"{Ns}\"/>");

        (bool found, Customer? customer) = _parser.ParseSingle<Customer>(TestServices.Stream(xml), "Customer", Ns);

        Assert.False(found);
        Assert.Null(customer);
    }

    [Fact]
    public void Illegal_character_references_are_sanitized_and_parsing_retried()
    {
        string xml = Soap.ReadMultipleResult(Soap.CustomerXml("k1", "1", "Bad&#x1F;Name"), Soap.CustomerXml("k2", "2", "Raw\u0001Char"));

        List<Customer> customers = _parser.ParseList<Customer>(TestServices.Stream(xml), "Customer", Ns);

        Assert.Equal("BadName", customers[0].Name);
        Assert.Equal("RawChar", customers[1].Name);
    }

    [Fact]
    public void Structurally_broken_xml_still_throws_with_original_as_inner()
    {
        string xml = Soap.ReadMultipleResult("<Customer><No>1</No>");

        var error = Assert.Throws<XmlException>(() => _parser.ParseList<Customer>(TestServices.Stream(xml), "Customer", Ns));
        var original = Assert.IsType<XmlException>(error.InnerException);
        string location = $"Line {original.LineNumber}, position {original.LinePosition}.";
        Assert.Equal(1, error.Message.Split(location).Length - 1);
    }

    [Fact]
    public void ParseResultValue_reads_nested_result()
    {
        string xml = Soap.Envelope($"<GetRecIdFromKey_Result xmlns=\"{Ns}\"><GetRecIdFromKey_Result>Customer: 10000</GetRecIdFromKey_Result></GetRecIdFromKey_Result>");

        Assert.Equal("Customer: 10000", _parser.ParseResultValue(TestServices.Stream(xml), "GetRecIdFromKey_Result"));
        Assert.Null(_parser.ParseResultValue(TestServices.Stream(Soap.Envelope("<Other/>")), "GetRecIdFromKey_Result"));
    }

    [Fact]
    public void ParseCodeUnitResponse_returns_return_value_and_by_ref_parameters()
    {
        string xml = Soap.Envelope("<Calc_Result xmlns=\"urn:microsoft-dynamics-schemas/codeunit/MyCU\"><return_value>42</return_value><outText>hello &amp; bye</outText></Calc_Result>");

        var response = _parser.ParseCodeUnitResponse(TestServices.Stream(xml));

        Assert.Equal("42", response.Value);
        Assert.Equal("hello & bye", response.Values["outText"]);
    }

    [Fact]
    public void ParseCodeUnitResponse_without_return_value_is_empty()
    {
        var response = _parser.ParseCodeUnitResponse(TestServices.Stream(Soap.Envelope("<Run_Result xmlns=\"urn:microsoft-dynamics-schemas/codeunit/MyCU\"/>")));

        Assert.Equal(String.Empty, response.Value);
        Assert.Empty(response.Values);
    }

    [Fact]
    public void ParseCodeUnitResponse_throws_on_unexpected_shape()
    {
        Assert.Throws<SoapResponseException>(() => _parser.ParseCodeUnitResponse(TestServices.Stream("<root/>")));
        Assert.Throws<SoapResponseException>(() => _parser.ParseCodeUnitResponse(TestServices.Stream(Soap.Envelope(String.Empty))));
    }

    [Fact]
    public void Dtd_is_rejected()
    {
        string xml = "<!DOCTYPE x [<!ENTITY a \"aaaa\">]>" + Soap.ReadMultipleResult().Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", "");

        Assert.Throws<XmlException>(() => _parser.ParseList<Customer>(TestServices.Stream(xml), "Customer", Ns));
    }

    [Fact]
    public void DateOnly_round_trips_or_is_rejected_never_silently_defaulted()
    {
        string xml = Soap.Envelope("<Read_Result xmlns=\"urn:microsoft-dynamics-schemas/page/dates\"><Dates><Day>2026-10-05</Day><Time>14:30:00</Time></Dates></Read_Result>");

        if (Serialization.XmlSerializerSupport.SupportsDateOnlyAndTimeOnly)
        {
            (_, Dates? dates) = _parser.ParseSingle<Dates>(TestServices.Stream(xml), "Dates", "urn:microsoft-dynamics-schemas/page/dates");
            Assert.Equal(new DateOnly(2026, 10, 5), dates!.Day);
            Assert.Equal(new TimeOnly(14, 30), dates.Time);
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => _parser.ParseSingle<Dates>(TestServices.Stream(xml), "Dates", "urn:microsoft-dynamics-schemas/page/dates"));
        }
    }

    [Fact]
    public void Sanitizing_fallback_honours_declared_encoding()
    {
        string xml = Soap.ReadMultipleResult(Soap.CustomerXml("k", "1", "Søren Ærø\u0001"))
            .Replace("encoding=\"utf-8\"", "encoding=\"iso-8859-1\"");
        var stream = new MemoryStream(System.Text.Encoding.Latin1.GetBytes(xml));

        List<Customer> customers = _parser.ParseList<Customer>(stream, "Customer", Ns);

        Assert.Equal("Søren Ærø", customers.Single().Name);
    }

    [Fact]
    public void Fallback_reads_windows_code_pages()
    {
        string xml = Soap.ReadMultipleResult(Soap.CustomerXml("k", "1", "Søren\u0001"))
            .Replace("encoding=\"utf-8\"", "encoding=\"windows-1252\"");
        var stream = new MemoryStream(System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetBytes(xml));

        Assert.Equal("Søren", _parser.ParseList<Customer>(stream, "Customer", Ns).Single().Name);
    }

    [Fact]
    public void Fallback_refuses_unknown_encoding_instead_of_guessing()
    {
        string xml = Soap.ReadMultipleResult(Soap.CustomerXml("k", "1", "x\u0001"))
            .Replace("encoding=\"utf-8\"", "encoding=\"x-no-such-encoding\"");

        var error = Assert.Throws<XmlException>(() => _parser.ParseList<Customer>(TestServices.Stream(xml), "Customer", Ns));
        Assert.Contains("x-no-such-encoding", error.Message);
    }

    [Fact]
    public void Fallback_reads_utf8_bytes_declared_as_utf16_as_utf8()
    {
        string xml = Soap.ReadMultipleResult(Soap.CustomerXml("k", "1", "Søren\u0001"))
            .Replace("encoding=\"utf-8\"", "encoding=\"utf-16\"");

        Assert.Equal("Søren", _parser.ParseList<Customer>(TestServices.Stream(xml), "Customer", Ns).Single().Name);
    }

    [Fact]
    public void Logged_context_is_a_window_not_the_whole_single_line_response()
    {
        var logger = new ListLogger<Services.ParsingService>();
        var parser = new Services.ParsingService(logger, new Services.XmlSanitizerService());
        string filler = new('x', 20_000);
        string xml = Soap.ReadMultipleResult(Soap.CustomerXml("k", "1", filler + "\u0001" + filler));

        parser.ParseList<Customer>(TestServices.Stream(xml), "Customer", Ns);

        string warning = logger.Messages.First();
        Assert.True(warning.Length < 1_000, $"Logged {warning.Length} characters");
        Assert.Contains("\\u0001", warning);
    }

    [Fact]
    public void Fallback_with_unsupported_or_unreadable_encoding_throws_xml_exception()
    {
        string utf7 = Soap.ReadMultipleResult(Soap.CustomerXml("k", "1", "x\u0001")).Replace("encoding=\"utf-8\"", "encoding=\"utf-7\"");
        string longDeclaration = Soap.ReadMultipleResult(Soap.CustomerXml("k", "1", "x\u0001")).Replace("encoding=\"utf-8\"", "encoding=\"utf-8\"" + new string(' ', 2000));

        Assert.Throws<XmlException>(() => _parser.ParseList<Customer>(TestServices.Stream(utf7), "Customer", Ns));
        Assert.Throws<XmlException>(() => _parser.ParseList<Customer>(TestServices.Stream(longDeclaration), "Customer", Ns));
    }
}

public sealed class ListLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Messages.Add(formatter(state, exception));
}

// Counts loaded assemblies, so it must not run in parallel with tests that load others.
[CollectionDefinition(nameof(SerializerCacheTests), DisableParallelization = true)]
[Collection(nameof(SerializerCacheTests))]
public class SerializerCacheTests
{
    private readonly Services.ParsingService _parser = TestServices.Parser();
    private static readonly string Ns = Soap.Customer.NamespaceName;

    [Fact]
    public void Serializers_are_cached_so_no_assembly_is_generated_per_call()
    {
        string xml = Soap.Envelope($"<Read_Result xmlns=\"{Ns}\">{Soap.CustomerXml("k", "1", "A")}</Read_Result>");
        _parser.ParseSingle<Customer>(TestServices.Stream(xml), "Customer", Ns);
        int before = AppDomain.CurrentDomain.GetAssemblies().Length;

        for (int i = 0; i < 50; i++)
        {
            _parser.ParseSingle<Customer>(TestServices.Stream(xml), "Customer", Ns);
        }

        Assert.Equal(before, AppDomain.CurrentDomain.GetAssemblies().Length);
        Assert.Same(
            Services.ParsingService.GetSerializer(typeof(Customer), "Customer", Ns),
            Services.ParsingService.GetSerializer(typeof(Customer), "Customer", Ns));
    }

    [Fact]
    public void Concurrent_first_use_builds_one_serializer()
    {
        string root = "Concurrent_" + Guid.NewGuid().ToString("N");
        var serializers = new System.Collections.Concurrent.ConcurrentBag<System.Xml.Serialization.XmlSerializer>();

        Parallel.For(0, 16, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
            serializers.Add(Services.ParsingService.GetSerializer(typeof(Customer), root, Ns)));

        Assert.Single(serializers.Distinct());
    }

    [Fact]
    public void Concurrent_first_use_generates_at_most_one_serializer_assembly()
    {
        string root = "Bounded_" + Guid.NewGuid().ToString("N");
        int before = AppDomain.CurrentDomain.GetAssemblies().Length;

        Parallel.For(0, 32, new ParallelOptions { MaxDegreeOfParallelism = 32 }, _ =>
            Services.ParsingService.GetSerializer(typeof(Customer), root, Ns));

        // One serializer may generate at most one assembly; a cache without Lazy can build several concurrently.
        Assert.InRange(AppDomain.CurrentDomain.GetAssemblies().Length - before, 0, 1);
    }
}
