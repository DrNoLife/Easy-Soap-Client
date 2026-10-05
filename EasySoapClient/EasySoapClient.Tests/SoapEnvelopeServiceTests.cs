using System.Xml.Linq;
using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Contracts.Read;
using EasySoapClient.Models;

namespace EasySoapClient.Tests;

// F-01, F-02, F-05, F-13, F-28, F-30, ReadMultiple element order.
public class SoapEnvelopeServiceTests
{
    private static readonly XNamespace Ns = Soap.Customer;
    private readonly Services.SoapEnvelopeService _envelopes = TestServices.Envelopes();

    private static XElement Body(string envelope)
        => XDocument.Parse(envelope).Root!.Element(XName.Get("Body", Soap.EnvelopeNs))!.Elements().Single();

    [Fact]
    public void Create_escapes_values()
    {
        string envelope = _envelopes.CreateCreateEnvelope(new Customer { No = "1", Name = "Jensen & Søn <A/S>" });

        XElement customer = Body(envelope).Element(Ns + "Customer")!;
        Assert.Equal("Jensen & Søn <A/S>", customer.Element(Ns + "Name")!.Value);
    }

    [Fact]
    public void Create_value_cannot_inject_elements()
    {
        string envelope = _envelopes.CreateCreateEnvelope(new Customer { No = "1", Name = "</wsns:Name><wsns:Blocked>false</wsns:Blocked><wsns:Name>" });

        XElement customer = Body(envelope).Element(Ns + "Customer")!;
        Assert.Null(customer.Element(Ns + "Blocked"));
        Assert.Single(customer.Elements(Ns + "Name"));
    }

    [Fact]
    public void ReadMultiple_escapes_nav_filter_syntax_and_bookmark()
    {
        string envelope = _envelopes.CreateReadMultipleEnvelope("Customer", [new ReadMultipleFilter("Balance", "<>0&<100"), new ReadMultipleFilter("Name", "A&B*")], 5, "12;<key>&");

        XElement read = Body(envelope);
        Assert.Equal(["<>0&<100", "A&B*"], read.Elements(Ns + "filter").Select(f => f.Element(Ns + "Criteria")!.Value));
        Assert.Equal("12;<key>&", read.Element(Ns + "bookmarkKey")!.Value);
    }

    [Fact]
    public void ReadMultiple_follows_wsdl_order_filter_bookmark_setSize()
    {
        string envelope = _envelopes.CreateReadMultipleEnvelope("Customer", [new ReadMultipleFilter("No", "1..9")], 25, "bm");

        Assert.Equal(["filter", "bookmarkKey", "setSize"], Body(envelope).Elements().Select(e => e.Name.LocalName));
        Assert.Equal("25", Body(envelope).Element(Ns + "setSize")!.Value);
    }

    [Fact]
    public void ReadMultiple_without_filters_sends_no_filter_element()
    {
        string envelope = _envelopes.CreateReadMultipleEnvelope("Customer", [], 10, null);

        Assert.Equal(["setSize"], Body(envelope).Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void ReadMultiple_rejects_negative_size_and_empty_field()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _envelopes.CreateReadMultipleEnvelope("Customer", [], -1, null));
        Assert.Throws<ArgumentException>(() => _envelopes.CreateReadMultipleEnvelope("Customer", [new ReadMultipleFilter("", "x")], 1, null));
    }

    [Fact]
    public void Read_escapes_and_formats_parameters()
    {
        using var _ = new CultureScope("da-DK");
        string envelope = _envelopes.CreateReadEnvelope("Customer", new ReadRequest(("No", "A&B"), ("Line_No", 10000), ("Amount", 1.5m)));

        XElement read = Body(envelope);
        Assert.Equal("A&B", read.Element(Ns + "No")!.Value);
        Assert.Equal("10000", read.Element(Ns + "Line_No")!.Value);
        Assert.Equal("1.5", read.Element(Ns + "Amount")!.Value);
    }

    [Fact]
    public void Read_with_default_request_throws_argument_exception()
    {
        Assert.Throws<ArgumentException>(() => _envelopes.CreateReadEnvelope("Customer", default));
    }

    [Fact]
    public void Invalid_element_names_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => _envelopes.CreateReadEnvelope("Customer", new ReadRequest("No><x", "1")));
        Assert.Throws<ArgumentException>(() => _envelopes.CreateCodeUnitMethodInvocationEnvelope(CodeUnitRequest.CreateRequest("CU", "Do It")));
    }

    [Fact]
    public void Values_are_formatted_culture_invariant()
    {
        using var _ = new CultureScope("da-DK");
        var item = new AllTypes
        {
            Amount = 1234.5m,
            Ratio = 0.25,
            Flag = true,
            When = new DateTime(2026, 10, 5, 14, 30, 0),
            DateOnlyField = new DateTime(2026, 10, 5, 14, 30, 0),
            Count = 1234567,
            Colour = Color.DarkBlue,
            Id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"),
        };

        XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Elements().Single();
        string Value(string name) => record.Elements().Single(e => e.Name.LocalName == name).Value;

        Assert.Equal("1234.5", Value("Amount"));
        Assert.Equal("0.25", Value("Ratio"));
        Assert.Equal("true", Value("Flag"));
        Assert.Equal("2026-10-05T14:30:00", Value("When"));
        Assert.Equal("2026-10-05", Value("DateOnlyField"));
        Assert.Equal("1234567", Value("Count"));
        Assert.Equal("Dark_Blue", Value("Colour"));
        Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", Value("Id"));
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Datetime_is_sent_as_whole_seconds_without_zone_like_2x(DateTimeKind kind)
    {
        var item = new AllTypes { When = new DateTime(2026, 10, 5, 14, 30, 0, kind).AddTicks(1234567) };
        XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Elements().Single();

        Assert.Equal("2026-10-05T14:30:00", record.Elements().Single(e => e.Name.LocalName == "When").Value);
    }

    [Fact]
    public void Specified_pattern_controls_whether_value_is_sent()
    {
        XElement withoutFlag = Body(_envelopes.CreateCreateEnvelope(new AllTypes { Quantity = 5 })).Elements().Single();
        XElement withFlag = Body(_envelopes.CreateCreateEnvelope(new AllTypes { Quantity = 5, QuantitySpecified = true })).Elements().Single();

        Assert.DoesNotContain(withoutFlag.Elements(), e => e.Name.LocalName == "Quantity");
        Assert.Equal("5", withFlag.Elements().Single(e => e.Name.LocalName == "Quantity").Value);
        Assert.DoesNotContain(withFlag.Elements(), e => e.Name.LocalName == "QuantitySpecified");
    }

    [Fact]
    public void Update_sends_only_settable_non_null_properties_with_key_first()
    {
        string envelope = _envelopes.CreateUpdateEnvelope(new Customer { Key = "12;abc", No = "10000", Name = null, Blocked = false });

        XElement customer = Body(envelope).Element(Ns + "Customer")!;
        Assert.Equal(["Key", "No", "Blocked"], customer.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("false", customer.Element(Ns + "Blocked")!.Value);
    }

    [Fact]
    public void Update_requires_key()
    {
        Assert.Throws<ArgumentException>(() => _envelopes.CreateUpdateEnvelope(new Customer { No = "1" }));
    }

    [Fact]
    public void Create_never_sends_key_get_only_or_ignored_properties()
    {
        string envelope = _envelopes.CreateCreateEnvelope(new Customer { Key = "should-not-be-sent", No = "1" });

        XElement customer = Body(envelope).Element(Ns + "Customer")!;
        Assert.Equal(["No"], customer.Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void Multiple_operations_wrap_records_in_list_element()
    {
        string create = _envelopes.CreateCreateMultipleEnvelope<Customer>([new Customer { No = "1" }, new Customer { No = "2" }]);
        string update = _envelopes.CreateUpdateMultipleEnvelope<Customer>([new Customer { Key = "k1", No = "1" }]);

        XElement createList = Body(create).Element(Ns + "Customer_List")!;
        Assert.Equal(2, createList.Elements(Ns + "Customer").Count());
        Assert.Equal("CreateMultiple", Body(create).Name.LocalName);
        Assert.Equal("k1", Body(update).Element(Ns + "Customer_List")!.Element(Ns + "Customer")!.Element(Ns + "Key")!.Value);
        Assert.Throws<ArgumentException>(() => _envelopes.CreateUpdateMultipleEnvelope<Customer>([new Customer { No = "no key" }]));
    }

    [Fact]
    public void Nested_lines_are_written_as_subpage_elements()
    {
        XNamespace ns = "urn:microsoft-dynamics-schemas/page/salesorder";
        var order = new SalesOrder
        {
            No = "SO1",
            Lines = [new SalesLine { No = "ITEM1", Quantity = 2 }, new SalesLine { Key = "line-key", Quantity = 3 }],
        };

        XElement record = Body(_envelopes.CreateCreateEnvelope(order)).Element(ns + "SalesOrder")!;
        XElement[] lines = [.. record.Element(ns + "SalesLines")!.Elements(ns + "Sales_Order_Line")];

        Assert.Equal(2, lines.Length);
        Assert.Equal(["No", "Quantity"], lines[0].Elements().Select(e => e.Name.LocalName));
        Assert.Equal("line-key", lines[1].Element(ns + "Key")!.Value);
    }

    [Fact]
    public void Key_operations_escape_key_and_use_operation_name()
    {
        string envelope = _envelopes.CreateKeyOperationEnvelope("Customer", "Delete", "12;<&>");

        Assert.Equal("Delete", Body(envelope).Name.LocalName);
        Assert.Equal("12;<&>", Body(envelope).Element(Ns + "Key")!.Value);
        var error = Assert.Throws<ArgumentException>(() => _envelopes.CreateKeyOperationEnvelope("Customer", "GetRecIdFromKey", ""));
        Assert.Contains("GetRecIdFromKey", error.Message);
    }

    [Fact]
    public void Codeunit_parameters_are_escaped_and_formatted()
    {
        using var _ = new CultureScope("da-DK");
        var request = CodeUnitRequestBuilder.WithCodeUnit("MyCU").WithMethod("Calc").AddParameter("text", "a<b&c").AddParameter("amount", 2.5m).AddParameter("empty", null).Build();

        XNamespace ns = "urn:microsoft-dynamics-schemas/codeunit/MyCU";
        XElement call = Body(_envelopes.CreateCodeUnitMethodInvocationEnvelope(request));

        Assert.Equal(ns + "Calc", call.Name);
        Assert.Equal("a<b&c", call.Element(ns + "text")!.Value);
        Assert.Equal("2.5", call.Element(ns + "amount")!.Value);
        Assert.Equal(String.Empty, call.Element(ns + "empty")!.Value);
    }

    [Fact]
    public void Codeunit_default_request_throws_argument_exception()
    {
        Assert.Throws<ArgumentException>(() => _envelopes.CreateCodeUnitMethodInvocationEnvelope(default));
    }

    [Fact]
    public void Envelope_has_no_padding_whitespace()
    {
        string envelope = _envelopes.CreateReadMultipleEnvelope("Customer", [], 1, null);

        Assert.DoesNotContain("\n", envelope);
        Assert.StartsWith("<soapenv:Envelope", envelope);
    }

    [Fact]
    public void Illegal_characters_in_values_are_rejected_before_sending()
    {
        Assert.Throws<ArgumentException>(() => _envelopes.CreateCreateEnvelope(new Customer { No = "1", Name = "bad\u0001" }));
    }

    [Fact]
    public void Array_attributes_without_names_fall_back_like_XmlSerializer()
    {
        XNamespace ns = "urn:microsoft-dynamics-schemas/page/unnamedarrays";
        var item = new UnnamedArrays { Lines = [new SalesLine { No = "1" }], OtherLines = [new SalesLine { No = "2" }] };

        XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Element(ns + "UnnamedArrays")!;

        Assert.Equal("1", record.Element(ns + "Lines")!.Element(ns + "SalesLine")!.Element(ns + "No")!.Value);
        Assert.Equal("2", record.Element(ns + "Other")!.Element(ns + "SalesLine")!.Element(ns + "No")!.Value);
    }

    [Fact]
    public void Compound_read_keys_keep_their_order()
    {
        var request = ReadRequestBuilder.New()
            .With("Document_Type", "Order")
            .With("Document_No", "101005")
            .With("Line_No", 10000)
            .With("Document_Type", "Invoice")
            .Build();

        XElement read = Body(_envelopes.CreateReadEnvelope("Customer", request));

        Assert.Equal(["Document_Type", "Document_No", "Line_No"], read.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("Invoice", read.Element(Ns + "Document_Type")!.Value);
    }

    [Fact]
    public void Flags_enums_are_space_separated_with_xml_enum_names()
    {
        XElement record = Body(_envelopes.CreateCreateEnvelope(new FlagsModel { Permissions = Permissions.Read | Permissions.Write })).Elements().Single();

        Assert.Equal("Read Write_Access", record.Elements().Single().Value);
    }

    [Fact]
    public void DateOnly_is_sent_where_the_runtime_can_read_it_back_and_rejected_otherwise()
    {
        var item = new Dates { Day = new DateOnly(2026, 1, 2), Time = new TimeOnly(14, 30) };

        if (Serialization.XmlSerializerSupport.SupportsDateOnlyAndTimeOnly)
        {
            XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Elements().Single();
            Assert.Equal(["2026-01-02", "14:30:00"], record.Elements().Select(e => e.Value));
        }
        else
        {
            var error = Assert.Throws<NotSupportedException>(() => _envelopes.CreateCreateEnvelope(item));
            Assert.Contains("Dates.Day", error.Message);
        }
    }

    [Fact]
    public void Public_fields_are_sent_like_XmlSerializer_does()
    {
        XNamespace ns = "urn:microsoft-dynamics-schemas/page/withfields";
        XElement record = Body(_envelopes.CreateCreateEnvelope(new WithFields { Name = "A&B", Amount = 2.5m })).Element(ns + "WithFields")!;

        Assert.Equal(["Name", "Amount_LCY"], record.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("A&B", record.Element(ns + "Name")!.Value);
    }

    [Fact]
    public void Collection_item_names_follow_XmlSerializer_defaults()
    {
        XNamespace ns = "urn:microsoft-dynamics-schemas/page/arraynames";
        var item = new ArrayNames { Tags = ["a"], Lines = [new TypedLine { No = "1" }] };

        XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Element(ns + "ArrayNames")!;

        Assert.Equal("a", record.Element(ns + "Tags")!.Element(ns + "string")!.Value);
        Assert.Equal("1", record.Element(ns + "Lines")!.Element(ns + "Sales_Line")!.Element(ns + "No")!.Value);

        // Same names as XmlSerializer itself would write.
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ArrayNames));
        using var writer = new StringWriter();
        serializer.Serialize(writer, item);
        XElement reference = XElement.Parse(writer.ToString());
        Assert.NotNull(reference.Element("Tags")!.Element("string"));
        Assert.NotNull(reference.Element("Lines")!.Element("Sales_Line"));
    }

    [Fact]
    public void Flags_enum_zero_without_named_member_is_empty()
    {
        XElement record = Body(_envelopes.CreateCreateEnvelope(new FlagsModel { Permissions = Permissions.None })).Elements().Single();
        Assert.Equal("None", record.Elements().Single().Value);

        Assert.Equal(String.Empty, Serialization.SoapValueFormatter.Format((AttributeTargets)0));
    }

    [Fact]
    public void Type_metadata_is_built_once_per_type()
    {
        Assert.Same(Serialization.TypeMetadata.For(typeof(Customer)), Serialization.TypeMetadata.For(typeof(Customer)));
    }

    [Fact]
    public void Invalid_service_name_fails_clearly_every_time()
    {
        for (int i = 0; i < 2; i++)
        {
            var error = Assert.Throws<InvalidOperationException>(() => _envelopes.CreateCreateEnvelope(new BadServiceName()));
            Assert.Contains("BadServiceName.ServiceName", error.Message);
        }
    }

    [Fact]
    public void DateOnly_support_matches_the_runtime()
    {
        // .NET 8's XmlSerializer reads DateOnly as default; .NET 10's reads it correctly.
        if (Environment.Version.Major == 8)
        {
            Assert.False(Serialization.XmlSerializerSupport.SupportsDateOnlyAndTimeOnly);
            Assert.Throws<NotSupportedException>(() => _envelopes.CreateCreateEnvelope(new DateOnlyField()));
        }
        else if (Environment.Version.Major >= 10)
        {
            Assert.True(Serialization.XmlSerializerSupport.SupportsDateOnlyAndTimeOnly);
        }

        // A computed get-only DateOnly is ignored by XmlSerializer, so it must not block the model.
        _envelopes.CreateCreateEnvelope(new ComputedDateOnly { When = new DateTime(2026, 1, 1) });
    }

    [Fact]
    public void Specified_pattern_works_for_public_fields_and_flag_is_never_sent()
    {
        XElement off = Body(_envelopes.CreateCreateEnvelope(new FieldSpecified { Amount = 5 })).Elements().Single();
        XElement on = Body(_envelopes.CreateCreateEnvelope(new FieldSpecified { Amount = 5, AmountSpecified = true })).Elements().Single();

        Assert.Empty(off.Elements());
        Assert.Equal(["Amount"], on.Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void Flags_enum_with_negative_member_is_formatted()
    {
        Assert.Equal("A High", Serialization.SoapValueFormatter.Format(SignedFlags.A | SignedFlags.High));
    }

    [Fact]
    public void Overridden_property_keeps_base_class_position_like_XmlSerializer()
    {
        XElement record = Body(_envelopes.CreateCreateEnvelope(new DerivedModel { First = "1", Second = "2", Third = "3" })).Elements().Single();

        Assert.Equal(["First", "Second", "Third"], record.Elements().Select(e => e.Name.LocalName));

        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(DerivedModel));
        using var writer = new StringWriter();
        serializer.Serialize(writer, new DerivedModel { First = "1", Second = "2", Third = "3" });
        Assert.Equal(["First", "Second", "Third"], XElement.Parse(writer.ToString()).Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void Get_only_collections_are_sent_like_XmlSerializer_does()
    {
        XNamespace ns = "urn:microsoft-dynamics-schemas/page/getonlylines";
        var item = new GetOnlyLines();
        item.Lines.Add(new SalesLine { No = "1" });

        XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Element(ns + "GetOnlyLines")!;

        Assert.Equal("1", record.Element(ns + "Lines")!.Element(ns + "Line")!.Element(ns + "No")!.Value);
    }

    [Fact]
    public void Hidden_members_and_getter_only_overrides_are_written_once()
    {
        XNamespace ns = "urn:microsoft-dynamics-schemas/page/hiding";
        var item = new HidingDerived { Foo = "f", FooSpecified = true };

        XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Element(ns + "Hiding")!;

        Assert.Equal(["Foo", "Bar"], record.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("computed", record.Element(ns + "Bar")!.Value);
    }

    [Fact]
    public void Circular_references_fail_instead_of_overflowing_the_stack()
    {
        var node = new Node { Name = "a" };
        node.Child = node;

        Assert.Throws<InvalidOperationException>(() => _envelopes.CreateCreateEnvelope(node));
        _envelopes.CreateCreateEnvelope(new Node { Name = "a", Child = new Node { Name = "b" } });
    }

    [Fact]
    public void Undefined_enum_values_and_chars_follow_XmlSerializer()
    {
        Assert.Throws<ArgumentException>(() => Serialization.SoapValueFormatter.Format((Color)42));
        Assert.Equal("65", Serialization.SoapValueFormatter.Format('A'));
    }

    [Fact]
    public void Get_only_interface_collections_and_dictionaries_are_not_sent()
    {
        XNamespace ns = "urn:microsoft-dynamics-schemas/page/computedviews";
        var item = new ComputedViews();
        item.Lines.Add(new SalesLine { No = "1", Quantity = 1 });

        XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Element(ns + "ComputedViews")!;

        Assert.Equal(["Lines"], record.Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void New_hiding_properties_match_XmlSerializer_order_and_values()
    {
        XNamespace ns = "urn:microsoft-dynamics-schemas/page/newhiding";
        var item = new NewHidingDerived { P1 = "1", P2 = "2", P3 = "3" };

        XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Element(ns + "NewHiding")!;

        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(NewHidingDerived));
        using var writer = new StringWriter();
        serializer.Serialize(writer, item);
        XElement reference = XElement.Parse(writer.ToString());

        Assert.Equal(reference.Elements().Select(e => (e.Name.LocalName, e.Value)), record.Elements().Select(e => (e.Name.LocalName, e.Value)));
    }

    [Fact]
    public void Settable_new_over_get_only_base_is_positioned_like_XmlSerializer()
    {
        var item = new SettableOverGetOnly { A = "a", N = "n", Z = "z", Y = "y" };
        XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Elements().Single();

        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SettableOverGetOnly));
        using var writer = new StringWriter();
        serializer.Serialize(writer, item);

        Assert.Equal(XElement.Parse(writer.ToString()).Elements().Select(e => e.Name.LocalName), record.Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void DateOnly_guard_covers_getter_only_overrides_and_attributes()
    {
        if (Serialization.XmlSerializerSupport.SupportsDateOnlyAndTimeOnly)
        {
            return;
        }

        Assert.Throws<NotSupportedException>(() => _envelopes.CreateCreateEnvelope(new GetterOnlyDateOverride()));
        Assert.Throws<NotSupportedException>(() => Services.ParsingService.GetSerializer(typeof(AttributeDate), "AttributeDate", "urn:x"));
    }

    [Fact]
    public void DateOnly_list_items_are_named_like_XmlSerializer()
    {
        if (!Serialization.XmlSerializerSupport.SupportsDateOnlyAndTimeOnly)
        {
            return;
        }

        var item = new DateList { Days = [new DateOnly(2026, 1, 2)] };
        XElement record = Body(_envelopes.CreateCreateEnvelope(item)).Elements().Single();

        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(DateList));
        using var writer = new StringWriter();
        serializer.Serialize(writer, item);
        XElement reference = XElement.Parse(writer.ToString());

        Assert.Equal(
            reference.Element("Days")!.Elements().Select(e => (e.Name.LocalName, e.Value)),
            record.Elements().Single().Elements().Select(e => (e.Name.LocalName, e.Value)));
    }
}
