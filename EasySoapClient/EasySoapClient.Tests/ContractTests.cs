using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Contracts.Read;
using EasySoapClient.Extensions;
using EasySoapClient.Models;

namespace EasySoapClient.Tests;

// F-06, F-20, F-24, F-31, F-02 (ToNavisionString).
public class ContractTests
{
    [Fact]
    public void Codeunit_soap_action_contains_namespace_not_delegate_name()
    {
        Assert.Equal(
            "urn:microsoft-dynamics-schemas/codeunit/MyCU:DoIt",
            CodeUnitRequest.CreateRequest("MyCU", "DoIt").GenerateSoapActionDefinedNamespace());
    }

    [Fact]
    public void Builder_reuse_does_not_change_built_requests()
    {
        var builder = CodeUnitRequestBuilder.WithCodeUnit("CU").WithMethod("M").AddParameter("a", "1");
        CodeUnitRequest first = builder.Build();
        builder.AddParameter("b", "2");

        Assert.Single(first.Parameters);
        Assert.Equal(2, builder.Build().Parameters.Count());
    }

    [Fact]
    public void Request_snapshots_caller_collection()
    {
        List<CodeUnitParameter> parameters = [new("a", "1")];
        var request = new CodeUnitRequest("CU", "M", parameters);
        parameters.Add(new("b", "2"));

        Assert.Single(request.Parameters);
    }

    [Fact]
    public void With_expression_also_snapshots_parameters()
    {
        List<CodeUnitParameter> parameters = [new("a", "1")];
        var request = CodeUnitRequest.CreateRequest("CU", "M") with { Parameters = parameters };
        parameters.Add(new("b", "2"));

        Assert.Single(request.Parameters);
        Assert.Empty(default(CodeUnitRequest).Parameters);
    }

    [Fact]
    public void ReadRequest_has_value_equality()
    {
        Assert.Equal(new ReadRequest(("No", "1"), ("Line", 2)), new ReadRequest(("No", "1"), ("Line", 2)));
        Assert.Equal(new ReadRequest("No", "1").GetHashCode(), new ReadRequest("No", "1").GetHashCode());
        Assert.NotEqual(new ReadRequest(("No", "1"), ("Line", 2)), new ReadRequest(("Line", 2), ("No", "1")));
    }

    [Fact]
    public void Namespace_is_culture_invariant()
    {
        using var _ = new CultureScope("tr-TR");

        Assert.Equal("urn:microsoft-dynamics-schemas/page/items", new Item().GetXmlNamespace().NamespaceName);
    }

    [Fact]
    public void ToNavisionString_is_culture_invariant()
    {
        using var _ = new CultureScope("da-DK");

        Assert.Equal("2026-10-05T14:30:00", new DateTime(2026, 10, 5, 14, 30, 0).ToNavisionString());
    }

    [Fact]
    public void ReadRequestBuilder_conversion_is_explicit_and_validates()
    {
        ReadRequest request = (ReadRequest)ReadRequestBuilder.New().With(("Line_No", 1), ("Item_No", "X"));

        Assert.Equal(2, request.Parameters.Count);
        Assert.Throws<InvalidOperationException>(() => (ReadRequest)ReadRequestBuilder.New());
    }

    [Fact]
    public void ReadMultipleFilter_is_an_immutable_value()
    {
        var filter = new ReadMultipleFilter("No", "1");

        Assert.Equal(new ReadMultipleFilter("No", "1"), filter);
        Assert.True(typeof(ReadMultipleFilter).IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), inherit: false));
    }

    [Fact]
    public void Implementation_types_are_not_public()
    {
        Type[] publicTypes = typeof(ReadMultipleFilter).Assembly.GetExportedTypes();

        Assert.DoesNotContain(publicTypes, t => t.Namespace == "EasySoapClient.Services");
        Assert.DoesNotContain(publicTypes, t => t.Name is "IParsingService" or "ISoapEnvelopeService" or "IRequestSenderService" or "IXmlSanitizerService");
    }

    private sealed class Item : Interfaces.IWebServiceElement
    {
        public string ServiceName => "Items";
    }

    [Fact]
    public void CodeUnitRequest_has_value_equality_and_readable_ToString()
    {
        List<CodeUnitParameter> parameters = [new("a", "1")];
        var first = new CodeUnitRequest("CU", "M", parameters);
        var second = new CodeUnitRequest("CU", "M", parameters);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, first with { MethodName = "Other" });
        Assert.Contains("ParameterName = a", first.ToString());
    }
}
