using System.Net;
using System.Text;
using System.Xml.Linq;
using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Contracts.Read;
using EasySoapClient.Exceptions;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using EasySoapClient.Models.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EasySoapClient.Tests;

// End-to-end through DI with a fake HTTP handler: F-03, F-06, F-07, F-08, F-09, F-10, F-12, F-17, F-18, F-32, F-36.
public class ClientIntegrationTests
{
    private static readonly string Ns = Soap.Customer.NamespaceName;

    private static FakeHandler RespondWith(string xml, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => FakeHandler.Xml(xml, status));

    [Fact]
    public async Task BaseUri_without_trailing_slash_keeps_company_segment()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        using var provider = TestServices.Build(handler);

        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();

        Assert.Equal("https://nav.example.com/BC/WS/CRONUS%20Danmark/Page/Customer", handler.Requests.Single().Uri!.AbsoluteUri);
    }

    [Fact]
    public async Task Page_request_has_quoted_soap_action_content_type_and_basic_auth()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        using var provider = TestServices.Build(handler, o => { o.Username = "Søren"; o.Password = "æøå"; });

        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>(new ReadMultipleFilter("No", "1"));

        RecordedRequest request = handler.Requests.Single();
        Assert.Equal("\"urn:microsoft-dynamics-schemas/page/customer:ReadMultiple\"", request.SoapAction);
        Assert.Equal("text/xml; charset=utf-8", request.ContentType);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("Søren:æøå")), request.Authorization);
    }

    [Fact]
    public async Task Codeunit_request_has_correct_soap_action_and_url()
    {
        var handler = RespondWith(Soap.Envelope("<Calc_Result xmlns=\"urn:microsoft-dynamics-schemas/codeunit/My CU\"><return_value>7</return_value></Calc_Result>"));
        using var provider = TestServices.Build(handler);

        CodeUnitResponse response = await provider.GetRequiredService<IEasySoapService>()
            .CallCodeUnitAsync(CodeUnitRequest.CreateRequest("My CU", "Calc", new CodeUnitParameter("x", 1)));

        RecordedRequest request = handler.Requests.Single();
        Assert.Equal("\"urn:microsoft-dynamics-schemas/codeunit/My CU:Calc\"", request.SoapAction);
        Assert.EndsWith("/Codeunit/My%20CU", request.Uri!.AbsoluteUri);
        Assert.Equal("7", response.Value);
    }

    [Fact]
    public async Task Default_http_client_of_the_application_is_not_configured_with_nav_credentials()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        using var provider = TestServices.Build(handler);

        HttpClient defaultClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient();

        Assert.Null(defaultClient.BaseAddress);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Keyed_clients_use_their_own_base_address_and_credentials()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        var services = new ServiceCollection().AddLogging();
        services.AddKeyedEasySoapClient("A", o => { o.BaseUri = "https://nav/WS/CompanyA"; o.Username = "a"; }, h => h.ConfigurePrimaryHttpMessageHandler(() => handler));
        services.AddKeyedEasySoapClient("B", o => { o.BaseUri = "https://nav/WS/CompanyB"; o.Username = "b"; }, h => h.ConfigurePrimaryHttpMessageHandler(() => handler));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        await provider.GetRequiredKeyedService<IEasySoapService>("A").GetAsync<Customer>();
        await provider.GetRequiredKeyedService<IEasySoapService>("B").GetAsync<Customer>();

        Assert.Equal("https://nav/WS/CompanyA/Page/Customer", handler.Requests[0].Uri!.AbsoluteUri);
        Assert.Equal("https://nav/WS/CompanyB/Page/Customer", handler.Requests[1].Uri!.AbsoluteUri);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("a:")), handler.Requests[0].Authorization);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("b:")), handler.Requests[1].Authorization);
        Assert.Null(provider.GetService<IEasySoapService>());
    }

    [Fact]
    public void Registering_several_clients_does_not_duplicate_services()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddKeyedEasySoapClient("A", o => { o.BaseUri = "https://nav/A"; o.Username = "a"; });
        services.AddKeyedEasySoapClient("A", o => { o.BaseUri = "https://nav/A"; o.Username = "a"; });
        services.AddKeyedEasySoapClient("B", o => { o.BaseUri = "https://nav/B"; o.Username = "b"; });
        services.AddEasySoapClient(o => { o.BaseUri = "https://nav/C"; o.Username = "c"; });
        services.AddEasySoapClient(o => { o.BaseUri = "https://nav/C"; o.Username = "c"; });

        Assert.Single(services, d => d.ServiceType == typeof(IEasySoapService) && !d.IsKeyedService);
        Assert.Single(services, d => d.ServiceType == typeof(ICredentialsProvider) && !d.IsKeyedService);
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IEasySoapService) && d.IsKeyedService));
        Assert.Single(services, d => d.ServiceType == typeof(IParsingService));
        Assert.Single(services, d => d.ServiceType == typeof(IValidateOptions<EasySoapClientOptions>));
        Assert.All(services.Where(d => d.ServiceType == typeof(IParsingService) || d.ServiceType == typeof(ISoapEnvelopeService)),
            d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));
    }

    [Theory]
    [InlineData("", "user", "BaseUri is required")]
    [InlineData("not a url", "user", "not an absolute http(s) URL")]
    [InlineData("ftp://nav/WS", "user", "not an absolute http(s) URL")]
    public void Invalid_options_fail_validation(string baseUri, string username, string expectedMessage)
    {
        using var provider = TestServices.Build(RespondWith(""), o => { o.BaseUri = baseUri; o.Username = username; });

        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptionsMonitor<EasySoapClientOptions>>().Get(Options.DefaultName));
        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void Options_can_be_bound_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Nav:BaseUri"] = "https://nav/WS/Company",
                ["Nav:Username"] = "u",
                ["Nav:AuthenticationMode"] = "Windows",
            })
            .Build();

        var services = new ServiceCollection().AddLogging();
        services.AddEasySoapClient(configuration.GetSection("Nav"));
        using var provider = services.BuildServiceProvider();

        EasySoapClientOptions options = provider.GetRequiredService<IOptionsMonitor<EasySoapClientOptions>>().Get(Options.DefaultName);
        Assert.Equal("https://nav/WS/Company", options.BaseUri);
        Assert.Equal(SoapAuthenticationMode.Windows, options.AuthenticationMode);
    }

    [Fact]
    public async Task Soap_fault_becomes_exception_with_status_and_fault_string()
    {
        var handler = RespondWith(Soap.Fault("The Customer does not exist."), HttpStatusCode.InternalServerError);
        using var provider = TestServices.Build(handler);

        var error = await Assert.ThrowsAsync<SoapRequestException>(() => provider.GetRequiredService<IEasySoapService>().CreateAsync(new Customer { No = "1" }));

        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.Equal("The Customer does not exist.", error.FaultString);
        Assert.Contains("NavCSideRecordNotFoundException", error.FaultCode);
        Assert.Equal("SOAP request failed with HTTP 500 (InternalServerError): The Customer does not exist.", error.Message);
        Assert.Null(error.SoapEnvelope);
    }

    [Fact]
    public async Task Envelope_is_included_in_exception_only_when_enabled()
    {
        var handler = RespondWith(String.Empty, HttpStatusCode.Unauthorized);
        using var provider = TestServices.Build(handler, o => o.IncludeEnvelopeInExceptions = true);

        var error = await Assert.ThrowsAsync<SoapRequestException>(() => provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>());

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Null(error.FaultString);
        Assert.Contains("ReadMultiple", error.SoapEnvelope);
        Assert.Equal("SOAP request failed with HTTP 401 (Unauthorized).", error.Message);
    }

    [Fact]
    public async Task GetItemAsync_returns_null_when_not_found()
    {
        var handler = RespondWith(Soap.Envelope($"<Read_Result xmlns=\"{Ns}\"/>"));
        using var provider = TestServices.Build(handler);

        Customer? customer = await provider.GetRequiredService<IEasySoapService>().GetItemAsync<Customer>(ReadRequestBuilder.Single("NOPE"));

        Assert.Null(customer);
    }

    [Fact]
    public async Task GetItemAsync_returns_record()
    {
        var handler = RespondWith(Soap.Envelope($"<Read_Result xmlns=\"{Ns}\">{Soap.CustomerXml("k", "10000", "A")}</Read_Result>"));
        using var provider = TestServices.Build(handler);

        Customer? customer = await provider.GetRequiredService<IEasySoapService>().GetItemAsync<Customer>(new ReadRequest("No", "10000"));

        Assert.Equal("A", customer!.Name);
        Assert.Contains("<wsns:No>10000</wsns:No>", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task Create_without_record_in_response_throws_short_message()
    {
        var handler = RespondWith(Soap.Envelope($"<Create_Result xmlns=\"{Ns}\"/>"));
        using var provider = TestServices.Build(handler);

        var error = await Assert.ThrowsAsync<SoapResponseException>(() => provider.GetRequiredService<IEasySoapService>().CreateAsync(new Customer { No = "1" }));
        Assert.DoesNotContain("Envelope", error.Message);
    }

    [Fact]
    public async Task GetAllAsync_pages_with_bookmark_key()
    {
        int call = 0;
        var handler = new FakeHandler(request =>
        {
            call++;
            return FakeHandler.Xml(call switch
            {
                1 => Soap.ReadMultipleResult(Soap.CustomerXml("k1", "1", "A"), Soap.CustomerXml("k2", "2", "B")),
                2 => Soap.ReadMultipleResult(Soap.CustomerXml("k3", "3", "C"), Soap.CustomerXml("k4", "4", "D")),
                _ => Soap.ReadMultipleResult(Soap.CustomerXml("k5", "5", "E")),
            });
        });
        using var provider = TestServices.Build(handler);

        List<string> numbers = [];
        await foreach (Customer customer in provider.GetRequiredService<IEasySoapService>().GetAllAsync<Customer>(pageSize: 2))
        {
            numbers.Add(customer.No);
        }

        Assert.Equal(["1", "2", "3", "4", "5"], numbers);
        XNamespace ns = Soap.Customer;
        string?[] bookmarks = [.. handler.Requests.Select(r => XDocument.Parse(r.Body).Descendants(ns + "bookmarkKey").SingleOrDefault()?.Value)];
        Assert.Equal<IEnumerable<string?>>([null, "k2", "k4"], bookmarks);
    }

    [Fact]
    public async Task Delete_IsUpdated_and_GetIdFromKey_parse_results()
    {
        var handler = new FakeHandler(request =>
        {
            string operation = request.SoapAction!.Trim('"').Split(':').Last();
            string value = operation switch
            {
                "Delete" => "true",
                "IsUpdated" => "false",
                _ => "Customer: 10000: x",
            };
            return FakeHandler.Xml(Soap.Envelope($"<{operation}_Result xmlns=\"{Ns}\"><{operation}_Result>{value}</{operation}_Result></{operation}_Result>"));
        });
        using var provider = TestServices.Build(handler);
        var service = provider.GetRequiredService<IEasySoapService>();

        Assert.True(await service.DeleteAsync<Customer>("k"));
        Assert.False(await service.IsUpdatedAsync<Customer>("k"));
        Assert.Equal("10000: x", await service.GetIdFromKeyAsync<Customer>("k"));
        Assert.Equal("Customer: 10000: x", await service.GetIdFromKeyAsync<Customer>("k", longResult: true));
    }

    [Fact]
    public async Task GetIdFromKey_without_result_element_throws()
    {
        var handler = RespondWith(Soap.Envelope("<Unexpected/>"));
        using var provider = TestServices.Build(handler);

        await Assert.ThrowsAsync<SoapResponseException>(() => provider.GetRequiredService<IEasySoapService>().GetIdFromKeyAsync<Customer>("k"));
    }

    [Fact]
    public async Task CreateMultiple_and_ReadByRecId_round_trip()
    {
        var handler = new FakeHandler(request => FakeHandler.Xml(request.SoapAction!.EndsWith(":CreateMultiple\"", StringComparison.Ordinal)
            ? Soap.Envelope($"<CreateMultiple_Result xmlns=\"{Ns}\"><Customer_List>{Soap.CustomerXml("k1", "1", "A")}{Soap.CustomerXml("k2", "2", "B")}</Customer_List></CreateMultiple_Result>")
            : Soap.Envelope($"<ReadByRecId_Result xmlns=\"{Ns}\">{Soap.CustomerXml("k1", "1", "A")}</ReadByRecId_Result>")));
        using var provider = TestServices.Build(handler);
        var service = provider.GetRequiredService<IEasySoapService>();

        IReadOnlyList<Customer> created = await service.CreateMultipleAsync([new Customer { No = "1" }, new Customer { No = "2" }]);
        Customer? byRecId = await service.GetItemByRecIdAsync<Customer>("Customer: 1");

        Assert.Equal(["k1", "k2"], created.Select(c => c.Key));
        Assert.Equal("1", byRecId!.No);
        Assert.Empty(await service.CreateMultipleAsync(Array.Empty<Customer>()));
    }

    [Fact]
    public async Task OAuth_mode_uses_registered_token_provider()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        using var provider = TestServices.Build(
            handler,
            o => { o.AuthenticationMode = SoapAuthenticationMode.OAuth; o.Username = ""; },
            extra: s => s.AddSingleton<IAccessTokenProvider>(new StaticTokenProvider("token-123")));

        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();

        Assert.Equal("Bearer token-123", handler.Requests.Single().Authorization);
    }

    [Fact]
    public async Task OAuth_mode_without_token_provider_fails_clearly()
    {
        using var provider = TestServices.Build(RespondWith(Soap.ReadMultipleResult()), o => o.AuthenticationMode = SoapAuthenticationMode.OAuth);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>());
        Assert.Contains(nameof(IAccessTokenProvider), error.Message);
    }

    [Fact]
    public async Task Custom_credentials_provider_is_called_per_request()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        var credentials = new CountingCredentials();
        using var provider = TestServices.Build(handler, extra: s => s.AddTransient<ICredentialsProvider>(_ => credentials));
        var service = provider.GetRequiredService<IEasySoapService>();

        await service.GetAsync<Customer>();
        await service.GetAsync<Customer>();

        Assert.Equal(["Basic 1", "Basic 2"], handler.Requests.Select(r => r.Authorization));
    }

    [Fact]
    public async Task None_mode_sends_no_authorization_header()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        using var provider = TestServices.Build(handler, o => o.AuthenticationMode = SoapAuthenticationMode.None);

        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();

        Assert.Null(handler.Requests.Single().Authorization);
    }

    [Fact]
    public async Task Configuration_reload_applies_to_credentials_without_restart()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Nav:BaseUri"] = "https://nav/WS/Company",
                ["Nav:Username"] = "first",
                ["Nav:Password"] = "pw",
            })
            .Build();
        var services = new ServiceCollection().AddLogging();
        services.AddEasySoapClient(configuration.GetSection("Nav"), h => h.ConfigurePrimaryHttpMessageHandler(() => handler));
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IEasySoapService>();

        await service.GetAsync<Customer>();
        configuration["Nav:Username"] = "second";
        configuration.Reload();
        await service.GetAsync<Customer>();

        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("first:pw")), handler.Requests[0].Authorization);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("second:pw")), handler.Requests[1].Authorization);
    }

    [Fact]
    public async Task Custom_credentials_registered_before_the_client_win()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<ICredentialsProvider>(new FixedCredentials("plain"));
        services.AddKeyedSingleton<ICredentialsProvider>("A", new FixedCredentials("keyed"));
        services.AddEasySoapClient(o => { o.BaseUri = "https://nav/WS/C"; o.Username = "u"; }, h => h.ConfigurePrimaryHttpMessageHandler(() => handler));
        services.AddKeyedEasySoapClient("A", o => { o.BaseUri = "https://nav/WS/A"; o.Username = "u"; }, h => h.ConfigurePrimaryHttpMessageHandler(() => handler));
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();
        await provider.GetRequiredKeyedService<IEasySoapService>("A").GetAsync<Customer>();

        Assert.Equal(["Basic plain", "Basic keyed"], handler.Requests.Select(r => r.Authorization));
    }

    [Fact]
    public async Task Cancellation_is_passed_to_the_http_call()
    {
        var handler = new BlockingHandler();
        var services = new ServiceCollection().AddLogging();
        services.AddEasySoapClient(o => { o.BaseUri = "https://nav/WS/C"; o.Username = "u"; }, h => h.ConfigurePrimaryHttpMessageHandler(() => handler));
        using var provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();

        Task call = provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>(cancellationToken: cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.True(handler.SawCancellation);
    }

    [Fact]
    public async Task Windows_mode_refuses_a_primary_handler_shared_between_clients()
    {
        var shared = new RecordingClientHandler();
        var services = new ServiceCollection().AddLogging();
        services.AddKeyedEasySoapClient("A", o => { o.BaseUri = "https://nav/A"; o.AuthenticationMode = SoapAuthenticationMode.Windows; o.Username = "alice"; }, h => h.ConfigurePrimaryHttpMessageHandler(() => shared));
        services.AddKeyedEasySoapClient("B", o => { o.BaseUri = "https://nav/B"; o.AuthenticationMode = SoapAuthenticationMode.Windows; o.Username = "bob"; }, h => h.ConfigurePrimaryHttpMessageHandler(() => shared));
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredKeyedService<IEasySoapService>("A").GetAsync<Customer>();
        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredKeyedService<IEasySoapService>("B"));

        Assert.Contains("own primary handler", error.Message);
        Assert.Equal("alice", ((NetworkCredential)shared.Credentials!).UserName);
    }

    [Fact]
    public async Task Default_credentials_without_username_fail_at_request_time_but_custom_provider_needs_none()
    {
        using var withoutUser = TestServices.Build(RespondWith(Soap.ReadMultipleResult()), o => o.Username = "");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => withoutUser.GetRequiredService<IEasySoapService>().GetAsync<Customer>());
        Assert.Contains("Username is required", error.Message);

        var handler = RespondWith(Soap.ReadMultipleResult());
        using var custom = TestServices.Build(handler, o => o.Username = "", extra: s => s.AddSingleton<ICredentialsProvider>(new FixedCredentials("from-vault")));
        await custom.GetRequiredService<IEasySoapService>().GetAsync<Customer>();
        Assert.Equal("Basic from-vault", handler.Requests.Single().Authorization);
    }

    [Fact]
    public async Task Invalid_options_fail_host_start()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddEasySoapClient(o => o.BaseUri = "");
        builder.Services.AddKeyedEasySoapClient("B", o => o.BaseUri = "nope");
        using var host = builder.Build();

        // Both clients are validated at start; the host aggregates the failures.
        var error = await Assert.ThrowsAsync<AggregateException>(() => host.StartAsync());

        Assert.All(error.InnerExceptions, e => Assert.IsType<OptionsValidationException>(e));
        Assert.Contains("EasySoapClient: BaseUri is required", error.Message);
        Assert.Contains("EasySoapClient 'B': BaseUri 'nope'", error.Message);
    }

    [Fact]
    public async Task Scoped_token_provider_gets_the_callers_scope()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        int created = 0;
        using var provider = TestServices.Build(
            handler,
            o => o.AuthenticationMode = SoapAuthenticationMode.OAuth,
            extra: s => s.AddScoped<IAccessTokenProvider>(_ => new StaticTokenProvider($"t{++created}")));

        for (int i = 0; i < 3; i++)
        {
            using IServiceScope scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();
        }

        Assert.Equal(["Bearer t1", "Bearer t2", "Bearer t3"], handler.Requests.Select(r => r.Authorization));
    }

    [Fact]
    public async Task Keyed_client_does_not_fall_back_to_non_keyed_token_provider()
    {
        using var provider = TestServices.Build(
            RespondWith(Soap.ReadMultipleResult()),
            o => o.AuthenticationMode = SoapAuthenticationMode.OAuth,
            key: "TenantB",
            extra: s => s.AddSingleton<IAccessTokenProvider>(new StaticTokenProvider("tenant-a-token")));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRequiredKeyedService<IEasySoapService>("TenantB").GetAsync<Customer>());
        Assert.Contains("'TenantB'", error.Message);
    }

    [Fact]
    public async Task Registering_the_same_client_twice_authenticates_once_per_request()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        var tokens = new CountingTokenProvider();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IAccessTokenProvider>(tokens);
        for (int i = 0; i < 2; i++)
        {
            services.AddEasySoapClient(o => { o.BaseUri = "https://nav/WS/C"; o.AuthenticationMode = SoapAuthenticationMode.OAuth; }, h => h.ConfigurePrimaryHttpMessageHandler(() => handler));
        }

        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();

        Assert.Equal(1, tokens.Calls);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Windows_mode_sets_credentials_on_the_primary_handler_without_replacing_it()
    {
        var handler = new RecordingClientHandler();
        var services = new ServiceCollection().AddLogging();
        services.AddEasySoapClient(
            o => { o.BaseUri = "https://nav/WS/C"; o.AuthenticationMode = SoapAuthenticationMode.Windows; o.Username = "svc"; o.Password = "pw"; o.Domain = "CORP"; },
            h => h.ConfigurePrimaryHttpMessageHandler(() => handler));
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();

        var credential = Assert.IsType<NetworkCredential>(handler.SeenCredentials);
        Assert.Equal(("svc", "pw", "CORP"), (credential.UserName, credential.Password, credential.Domain));
        Assert.True(handler.PreAuthenticate);
        Assert.Null(handler.SeenAuthorization);
    }

    [Fact]
    public void Windows_mode_with_unknown_primary_handler_fails_clearly()
    {
        using var provider = TestServices.Build(RespondWith(Soap.ReadMultipleResult()), o => o.AuthenticationMode = SoapAuthenticationMode.Windows);

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IEasySoapService>());
        Assert.Contains("Windows authentication", error.Message);
    }

    [Fact]
    public async Task App_wide_primary_handler_defaults_still_apply()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        var services = new ServiceCollection().AddLogging();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => handler));
        services.AddEasySoapClient(o => { o.BaseUri = "https://nav/WS/C"; o.Username = "u"; });
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Response_is_disposed_after_parsing()
    {
        var content = new TrackingContent(Soap.ReadMultipleResult());
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        using var provider = TestServices.Build(handler);

        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();

        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task BaseUri_query_is_kept_on_every_request()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        using var provider = TestServices.Build(handler, o => o.BaseUri = "https://srv/BC/WS/CRONUS?tenant=t1");

        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();

        Assert.Equal("https://srv/BC/WS/CRONUS/Page/Customer?tenant=t1", handler.Requests.Single().Uri!.AbsoluteUri);
    }

    [Fact]
    public void BaseUri_with_fragment_is_rejected()
    {
        using var provider = TestServices.Build(RespondWith(""), o => o.BaseUri = "https://srv/BC/WS/CRONUS#x");

        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptionsMonitor<EasySoapClientOptions>>().Get(Options.DefaultName));
        Assert.Contains("fragment", error.Message);
    }

    [Fact]
    public async Task Windows_mode_accepts_a_handler_that_already_has_the_same_credentials()
    {
        var handler = new RecordingClientHandler { Credentials = new NetworkCredential("svc", "pw", "CORP") };
        var services = new ServiceCollection().AddLogging();
        services.AddEasySoapClient(
            o => { o.BaseUri = "https://nav/WS/C"; o.AuthenticationMode = SoapAuthenticationMode.Windows; o.Username = "svc"; o.Password = "pw"; o.Domain = "CORP"; },
            h => h.ConfigurePrimaryHttpMessageHandler(() => handler));
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IEasySoapService>().GetAsync<Customer>();

        Assert.Equal("svc", ((NetworkCredential)handler.SeenCredentials!).UserName);
    }

    [Fact]
    public async Task None_mode_keeps_a_default_authorization_header_set_by_the_user()
    {
        var handler = RespondWith(Soap.ReadMultipleResult());
        var services = new ServiceCollection().AddLogging();
        services.AddEasySoapClient(
            o => { o.BaseUri = "https://nav/WS/C"; o.AuthenticationMode = SoapAuthenticationMode.None; },
            h =>
            {
                h.ConfigurePrimaryHttpMessageHandler(() => handler);
                h.ConfigureHttpClient(c => c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "mine"));
            });
        using var custom = services.BuildServiceProvider();

        await custom.GetRequiredService<IEasySoapService>().GetAsync<Customer>();

        Assert.Equal("Bearer mine", handler.Requests.Single().Authorization);
    }

    private sealed class StaticTokenProvider(string token) : IAccessTokenProvider
    {
        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken) => ValueTask.FromResult(token);
    }

    private sealed class CountingCredentials : ICredentialsProvider
    {
        private int _calls;
        public string GenerateBase64Credentials() => (++_calls).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class FixedCredentials(string value) : ICredentialsProvider
    {
        public string GenerateBase64Credentials() => value;
    }

    private sealed class CountingTokenProvider : IAccessTokenProvider
    {
        public int Calls { get; private set; }

        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult("t");
        }
    }

    private sealed class RecordingClientHandler : HttpClientHandler
    {
        public ICredentials? SeenCredentials { get; private set; }
        public string? SeenAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SeenCredentials = Credentials;
            SeenAuthorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(FakeHandler.Xml(Soap.ReadMultipleResult()));
        }
    }

    private sealed class TrackingContent(string xml) : StringContent(xml, Encoding.UTF8, "text/xml")
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool SawCancellation { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                SawCancellation = true;
                throw;
            }

            throw new InvalidOperationException("unreachable");
        }
    }
}
