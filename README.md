# Easy Soap Client

A .NET client for **Microsoft Dynamics NAV / Business Central SOAP web services** (pages and codeunits), without a Visual Studio generated proxy.

You describe a page with a small model class, inject `IEasySoapService`, and call `GetAsync`, `CreateAsync`, `UpdateAsync`, `CallCodeUnitAsync` and friends. The library builds the SOAP envelopes, sends them through `IHttpClientFactory`, and deserializes the responses.

- Targets **.NET 8** and **.NET 10**.
- Basic, Windows (NTLM/Negotiate) and OAuth authentication.
- Keyed clients for several NAV instances or companies in one application.
- Culture-independent value formatting, full XML escaping, streaming response parsing.

> Upgrading from 2.x? Read [Migrating from 2.x to 3.0](#migrating-from-2x-to-30) first.

## Contents

- [Installation](#installation)
- [Quick start](#quick-start)
- [Configuration](#configuration)
  - [Options](#options)
  - [BaseUri](#baseuri)
  - [Authentication](#authentication)
  - [Several instances (keyed clients)](#several-instances-keyed-clients)
  - [Configuring the HttpClient](#configuring-the-httpclient)
- [Models](#models)
  - [Mapping rules](#mapping-rules)
  - [Value formatting](#value-formatting)
  - [Partial updates](#partial-updates)
  - [Subpages (lines)](#subpages-lines)
- [Operations](#operations)
  - [Reading records](#reading-records)
  - [Filters](#filters)
  - [Paging](#paging)
  - [Reading a single record](#reading-a-single-record)
  - [Create, update, delete](#create-update-delete)
  - [Keys and record ids](#keys-and-record-ids)
  - [Codeunits](#codeunits)
- [Error handling](#error-handling)
- [Illegal XML characters in responses](#illegal-xml-characters-in-responses)
- [Logging](#logging)
- [Migrating from 2.x to 3.0](#migrating-from-2x-to-30)
- [License](#license)

## Installation

```
dotnet add package EasySoapClient
```

## Quick start

**1. Register the client** (`Program.cs`):

```csharp
builder.Services.AddEasySoapClient(builder.Configuration.GetSection("Navision"));
```

```json
{
  "Navision": {
    "BaseUri": "https://nav.example.com:7047/BC/WS/CRONUS Danmark",
    "Username": "WEBSERVICE",
    "Password": "secret"
  }
}
```

**2. Describe a page** published as a web service:

```csharp
public class Customer : IKeyedWebServiceElement
{
    public string ServiceName => "Customer";

    public string Key { get; set; } = "";

    [XmlElement("No")]
    public string? Number { get; set; }

    public string? Name { get; set; }

    public decimal? Balance_LCY { get; set; }
}
```

Use nullable types for fields: `null` means "not set" and is never sent, so the same model works for reading, creating and partial updates.

**3. Use it:**

```csharp
public class CustomerSync(IEasySoapService soap)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Customer> customers = await soap.GetAsync<Customer>(
            new ReadMultipleFilter("Balance_LCY", ">1000"),
            size: 50,
            cancellationToken: cancellationToken);

        Customer? customer = await soap.GetItemAsync<Customer>(ReadRequestBuilder.Single("10000"), cancellationToken);
    }
}
```

## Configuration

### Options

Register with a configuration section or a delegate:

```csharp
builder.Services.AddEasySoapClient(builder.Configuration.GetSection("Navision"));

builder.Services.AddEasySoapClient(options =>
{
    options.BaseUri = "https://nav.example.com:7047/BC/WS/CRONUS Danmark";
    options.Username = "WEBSERVICE";
    options.Password = builder.Configuration["Navision:Password"]!;
});
```

| Option | Default | Description |
| --- | --- | --- |
| `BaseUri` | (required) | The web service base address including the company. See [BaseUri](#baseuri). |
| `AuthenticationMode` | `Basic` | `Basic`, `Windows`, `OAuth` or `None`. See [Authentication](#authentication). |
| `Username` | `""` | Required for `Basic` (unless you register your own `ICredentialsProvider`). Optional for `Windows`. |
| `Password` | `""` | Password for `Basic` / `Windows`. |
| `Domain` | `null` | Domain for `Windows`. |
| `IncludeEnvelopeInExceptions` | `false` | Put the failing request envelope on `SoapRequestException.SoapEnvelope`. Off by default because envelopes contain business data. |

Options are validated: an empty or non-http(s) `BaseUri`, or one with a `#fragment`, fails at startup (when running in a host, wrapped in an `AggregateException` if several clients fail) or on first use, with a message naming the client. A missing `Username` for `Basic` is reported on the first request, so a custom `ICredentialsProvider` works without one.

Basic and OAuth credentials are read on every request, so credentials changed in a reloadable configuration source apply without restarting. `BaseUri` and `IncludeEnvelopeInExceptions` apply to services resolved after the change, and Windows credentials when `IHttpClientFactory` recycles the handler (every 2 minutes by default).

### BaseUri

Take the URL of any web service and cut it after the company:

```
https://<server>:<port>/<instance>/WS/<company>/Page/Customer
https://<server>:<port>/<instance>/WS/<company>          <- BaseUri
```

A trailing slash is added automatically, and spaces in the company name are allowed. (In 2.x a missing trailing slash made every call go to the server's *default* company.) A query string, such as `?tenant=t1` on multitenant servers, is kept and sent with every request.

### Authentication

| Mode | Use for | How |
| --- | --- | --- |
| `Basic` | NAV / BC on-premises with NavUserPassword | `Authorization: Basic` built from `Username` and `Password` (UTF-8, so `æøå` work). |
| `Windows` | On-premises with Windows authentication | NTLM / Negotiate with `Username`, `Password`, `Domain`, or the process' own credentials when `Username` is empty. |
| `OAuth` | Business Central online | `Authorization: Bearer` from your `IAccessTokenProvider`. |
| `None` | You add authentication yourself | Nothing is added. |

The header is created for every request, so rotating credentials and expiring tokens work. Credential and token providers are resolved in your DI scope together with the `IEasySoapService`, so scoped and transient providers behave as registered. Do not keep using an `IEasySoapService` after its scope is disposed (e.g. in fire-and-forget work); resolve it in a scope that lives as long as the work.

**OAuth** - register a token provider. A keyed client only uses a provider registered with its key (it never falls back to a non-keyed one, so one tenant's token cannot reach another tenant). It is called for every request, so cache tokens in a singleton.

```csharp
public sealed class BusinessCentralTokenProvider(IConfidentialClientApplication app) : IAccessTokenProvider
{
    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        AuthenticationResult result = await app
            .AcquireTokenForClient(["https://api.businesscentral.dynamics.com/.default"])
            .ExecuteAsync(cancellationToken);

        return result.AccessToken;
    }
}

builder.Services.AddSingleton<IAccessTokenProvider, BusinessCentralTokenProvider>();
builder.Services.AddEasySoapClient(options =>
{
    options.BaseUri = "https://api.businesscentral.dynamics.com/v2.0/<tenant>/Production/WS/CRONUS";
    options.AuthenticationMode = SoapAuthenticationMode.OAuth;
});
```

**Custom Basic credentials** - register your own `ICredentialsProvider` (for example backed by a secret store). For a non-keyed client a plain registration replaces the default, whichever order you register in; for a keyed client register it keyed with the same key. It is called for every request, and `Username` / `Password` in the options are then not needed.

**Windows** - the credentials are set on the client's primary handler (`SocketsHttpHandler` or `HttpClientHandler`, including one you or `ConfigureHttpClientDefaults` configured). Every client needs its own handler instance: a handler that already carries other credentials is refused, so two clients can never authenticate as each other. If you use another primary handler type, set its credentials yourself and use `AuthenticationMode.None`.

### Several instances (keyed clients)

Use one key per NAV instance or company:

```csharp
builder.Services.AddKeyedEasySoapClient("CompanyA", builder.Configuration.GetSection("Navision:CompanyA"));
builder.Services.AddKeyedEasySoapClient("CompanyB", builder.Configuration.GetSection("Navision:CompanyB"));
```

```csharp
public class Worker(
    [FromKeyedServices("CompanyA")] IEasySoapService companyA,
    [FromKeyedServices("CompanyB")] IEasySoapService companyB)
{
}
```

Keyed and non-keyed clients can be mixed. Each client gets its own named `HttpClient`, options and credentials; the application's default `HttpClient` is never touched.

### Configuring the HttpClient

Every registration method takes an optional `Action<IHttpClientBuilder>`, for timeouts, extra handlers, proxies or certificates:

```csharp
builder.Services.AddEasySoapClient(
    builder.Configuration.GetSection("Navision"),
    http => http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(100)));
```

Leave `BaseAddress` alone; the library sets it from `BaseUri`. App-wide defaults from `ConfigureHttpClientDefaults` apply too.

**Be careful with retries.** Every SOAP call is an HTTP POST, including `Create`, `Update`, `Delete` and codeunits that post documents, and NAV answers *every* business error (validation, "already exists", locking) with HTTP 500. A generic resilience pipeline such as `AddStandardResilienceHandler()` retries POSTs on 500 and on its own per-attempt timeout, which can create the same record or posting several times. If you add retries:

- only retry failures where the request cannot have reached NAV (connection refused, DNS, HTTP 502/503/504 from a proxy);
- never retry on HTTP 500 or on a timeout for writes and codeunits;
- prefer retrying in your own code around read operations (`GetAsync`, `GetItemAsync`).

## Models

A model is a class with a parameterless constructor that implements:

| Interface | Needed for |
| --- | --- |
| `IWebServiceElement` | Everything. `ServiceName` is the page's service name. |
| `IKeyedWebServiceElement` | `UpdateAsync`, `UpdateMultipleAsync`, `GetAllAsync`. Adds `Key`. |

`ISearchable` and `IUpdatableWebServiceElement` from 2.x still exist and are now just aliases of `IKeyedWebServiceElement`.

`ServiceName` is read once per type and cached, so it must be a constant.

### Mapping rules

Responses are deserialized with `XmlSerializer`, so its attributes apply. When **sending** (Create / Update), the library writes:

- public properties with a public getter **and** setter, public non-readonly fields, and get-only concrete collections (e.g. `List<Line> Lines { get; } = []`). Other get-only properties (e.g. `DisplayName => ...`, or a computed `IReadOnlyCollection<Line> Open => ...`) are never sent;
- under the name from `[XmlElement("...")]`, or the property name;
- in `[XmlElement(Order = n)]` order if used, otherwise declaration order (base class first);
- except properties marked `[XmlIgnore]` or `[XmlAttribute]`, and `ServiceName`;
- except properties whose value is `null` (an empty string **is** sent, and clears the field on update);
- except when a `FooSpecified` bool property or a `ShouldSerializeFoo()` method says not to (the `XmlSerializer` conventions);
- `Key` is never sent on create, and always sent first on update.

### Value formatting

Values are formatted with XML Schema formats, independent of the machine's culture:

| .NET type | Sent as |
| --- | --- |
| `decimal`, `double`, `int`, ... | `1234.5` |
| `bool` | `true` / `false` |
| `DateTime` | `2026-10-05T14:30:00` (whole seconds, no time zone, whatever the `Kind`; as in 2.x) |
| `DateTime` with `[XmlElement(DataType = "date")]` | `2026-10-05` |
| `DateOnly` | `2026-10-05` (.NET 10 only, see below) |
| `TimeOnly` | `14:30:00` (whole seconds; .NET 10 only, see below) |
| `DateTimeOffset` | `2026-10-05T14:30:00+02:00` (fractional seconds kept when present; use this to send an explicit offset) |
| `enum` | the `[XmlEnum("...")]` name, otherwise the member name |
| `Guid` | `0f8fad5b-d9cb-469f-a165-70867728950e` |
| `byte[]` | base64 |

The same rules apply to `ReadRequest` values and codeunit parameters. All text is XML escaped, so values and filters may contain `&`, `<`, `>` and quotes.

On **.NET 8**, `XmlSerializer` cannot read `DateOnly` / `TimeOnly`: they would silently come back as their default value, and a later update would write that back. The library checks the running runtime once (the first time a model type is used) and, where `XmlSerializer` cannot read them (.NET 8; .NET 10 can; other runtimes are detected the same way), throws `NotSupportedException` for models with such members. Use `DateTime` with `[XmlElement(DataType = "date")]` (or `"time"`) there. As codeunit parameters they work on both.

### Partial updates

`Update` changes the fields you send. To change only some fields, either use a small model with just those fields, or make fields nullable and leave them `null`:

```csharp
public class CustomerUpdate : IKeyedWebServiceElement
{
    public string ServiceName => "Customer";
    public string Key { get; set; } = "";
    public string? Name { get; set; }
    public bool? Blocked { get; set; }
}

await soap.UpdateAsync(new CustomerUpdate { Key = customer.Key, Blocked = true }); // Name is not touched
```

Non-nullable value types (`decimal`, `bool`, `DateTime`) are always sent, also when they hold their default value, and so are strings initialised to `""`. Use nullable types (`string?`, `decimal?`) or `FooSpecified` for fields you do not always want to send.

### Subpages (lines)

Collection properties are written as subpage elements, following `XmlSerializer` conventions:

```csharp
public class SalesOrder : IKeyedWebServiceElement
{
    public string ServiceName => "SalesOrder";
    public string Key { get; set; } = "";
    public string? Sell_to_Customer_No { get; set; }

    [XmlArray("SalesLines")]
    [XmlArrayItem("Sales_Order_Line")]
    public List<SalesOrderLine>? SalesLines { get; set; }
}

public class SalesOrderLine
{
    public string Key { get; set; } = "";   // empty for new lines: not sent
    public string? No { get; set; }
    public decimal? Quantity { get; set; }
}
```

## Operations

All methods take an optional `CancellationToken`.

| Method | NAV operation | Returns |
| --- | --- | --- |
| `GetAsync<T>(filters, size, bookmarkKey)` | `ReadMultiple` | `IReadOnlyList<T>` |
| `GetAllAsync<T>(filters, pageSize)` | `ReadMultiple`, page by page | `IAsyncEnumerable<T>` |
| `GetItemAsync<T>(ReadRequest)` | `Read` | `T?` (`null` when not found) |
| `GetItemByRecIdAsync<T>(recId)` | `ReadByRecId` | `T?` |
| `CreateAsync<T>(item)` | `Create` | the created record |
| `CreateMultipleAsync<T>(items)` | `CreateMultiple` | the created records |
| `UpdateAsync<T>(item)` | `Update` | the updated record |
| `UpdateMultipleAsync<T>(items)` | `UpdateMultiple` | the updated records |
| `DeleteAsync<T>(key)` | `Delete` | `bool` |
| `IsUpdatedAsync<T>(key)` | `IsUpdated` | `bool` |
| `GetIdFromKeyAsync<T>(key, longResult)` | `GetRecIdFromKey` | `string` |
| `CallCodeUnitAsync(request)` | codeunit method | `CodeUnitResponse` |

Create and update return the record as NAV saved it, including the new `Key` and fields NAV filled in.

### Reading records

```csharp
IReadOnlyList<Customer> firstTen = await soap.GetAsync<Customer>();
IReadOnlyList<Customer> all = await soap.GetAsync<Customer>(size: 0);   // 0 = no limit
```

`size` defaults to 10. For large tables use [paging](#paging) instead of `size: 0`.

### Filters

`ReadMultipleFilter` takes a field and a filter expression in normal NAV filter syntax. Several filters are combined with AND.

```csharp
IReadOnlyList<Customer> customers = await soap.GetAsync<Customer>(
[
    new ReadMultipleFilter("Balance_LCY", "<>0"),
    new ReadMultipleFilter("Country_Region_Code", "DK|SE"),
    new ReadMultipleFilter("Name", "@*a/s*"),
]);
```

### Paging

`GetAllAsync` streams every matching record. It calls `ReadMultiple` with `pageSize` records at a time and continues from the `Key` of the last record:

```csharp
await foreach (Customer customer in soap.GetAllAsync<Customer>(pageSize: 200, cancellationToken: cancellationToken))
{
    // ...
}
```

The model must implement `IKeyedWebServiceElement`. To page by hand, pass the last record's `Key` as `bookmarkKey` to `GetAsync`.

### Reading a single record

`GetItemAsync` calls `Read` with the page's key fields and returns `null` when the record does not exist. Give compound keys in the page's key order; the order is kept.

```csharp
Customer? customer = await soap.GetItemAsync<Customer>(ReadRequestBuilder.Single("10000"));        // key field "No"
Item? item = await soap.GetItemAsync<Item>(ReadRequestBuilder.Single("1000", name: "No"));

var lineRequest = new ReadRequest(("Document_Type", "Order"), ("Document_No", "101005"), ("Line_No", 10000));   // in key order
SalesLine? line = await soap.GetItemAsync<SalesLine>(lineRequest);

ReadRequest built = ReadRequestBuilder.New()
    .With("Document_Type", "Order")
    .With("No", "101005")
    .Build();
```

### Create, update, delete

```csharp
Customer created = await soap.CreateAsync(new Customer { Name = "Jensen & Søn A/S" });

created.Name = "Jensen & Sønner A/S";
Customer updated = await soap.UpdateAsync(created);

IReadOnlyList<Customer> many = await soap.CreateMultipleAsync(newCustomers);

bool deleted = await soap.DeleteAsync<Customer>(updated.Key);
```

Update uses `Key` to find the record, and NAV rejects it if the record has changed since `Key` was read. Use the `Key` from the latest read or from the record returned by the last update.

### Keys and record ids

```csharp
string id = await soap.GetIdFromKeyAsync<Customer>(customer.Key);                       // "10000"
string full = await soap.GetIdFromKeyAsync<Customer>(customer.Key, longResult: true);   // "Customer: 10000"
Customer? again = await soap.GetItemByRecIdAsync<Customer>(full);

bool changed = await soap.IsUpdatedAsync<Customer>(customer.Key);
```

### Codeunits

```csharp
CodeUnitRequest request = CodeUnitRequestBuilder
    .WithCodeUnit("SalesFunctions")
    .WithMethod("CalculatePrice")
    .AddParameter("itemNo", "1000")
    .AddParameter("quantity", 2.5m)
    .AddParameter("orderDate", new DateOnly(2026, 10, 5))
    .Build();

// or: CodeUnitRequest.CreateRequest("SalesFunctions", "CalculatePrice", new CodeUnitParameter("itemNo", "1000"));

CodeUnitResponse response = await soap.CallCodeUnitAsync(request);

string price = response.Value;                    // return_value
string message = response.Values["errorText"];    // a by-reference (var) parameter
```

Parameter values follow the [value formatting](#value-formatting) rules; `null` is sent as an empty element. The builder can be reused: requests already built are not affected.

## Error handling

| Exception | When |
| --- | --- |
| `SoapRequestException` | NAV answered with an HTTP error, usually a SOAP fault (validation error, record not found on update, permission error, 401). |
| `SoapResponseException` | The response was successful but not shaped as expected, e.g. a create response without the record. |
| `ArgumentException` | Invalid input caught before sending: missing `Key` on update, an empty `ReadRequest`, an invalid element name, a control character in a value. |
| `InvalidOperationException` | Missing setup found on the first request, e.g. no `Username` for `Basic`, or no `IAccessTokenProvider` for `OAuth`. |
| `NotSupportedException` | A model uses `DateOnly` / `TimeOnly` on .NET 8. |
| `XmlException` | The response is not valid XML, even after [removing illegal characters](#illegal-xml-characters-in-responses). |
| `OptionsValidationException` | Invalid configuration. |
| `HttpRequestException` | The server could not be reached (DNS, connection refused, TLS). |
| `TaskCanceledException` | The `HttpClient` timeout elapsed, or your `CancellationToken` was cancelled. |

`SoapRequestException` carries the details:

```csharp
try
{
    await soap.UpdateAsync(customer);
}
catch (SoapRequestException ex) when (ex.StatusCode == HttpStatusCode.InternalServerError)
{
    logger.LogWarning("NAV rejected the update: {Reason}", ex.FaultString);
}
```

| Property | Content |
| --- | --- |
| `StatusCode` | HTTP status. |
| `FaultString` | NAV's error message, e.g. `The Customer does not exist.` |
| `FaultCode` | NAV's error type, e.g. `a:Microsoft.Dynamics.Nav.Types.Exceptions.NavCSideRecordNotFoundException`. |
| `ErrorContent` | The raw response body. |
| `SoapEnvelope` | The request, only when `IncludeEnvelopeInExceptions` is on. |

`Message` is short: the status and the fault string.

## Illegal XML characters in responses

Some NAV setups return characters that XML does not allow (for example a stray `&#x1F;` in a text field), which would otherwise make the whole response unreadable.

When parsing fails, the library removes illegal characters, both raw control characters and numeric references such as `&#x1F;`, and parses once more. A warning is logged with the line, position and a few lines of context. Valid content, entities and references are left untouched; references inside `CDATA` and comments are kept, since a parser does not interpret them there.

If the response is still invalid, an `XmlException` is thrown with the original parse error as `InnerException`.

## Logging

Category names are the internal service names under `EasySoapClient.Services`.

| Level | What |
| --- | --- |
| `Trace` | Every request envelope (contains business data). |
| `Debug` | Every response status. |
| `Warning` | A response needed illegal-character removal. Includes the line, position and up to 7 lines around the error; the error line is cut to about 240 characters around the error position, the other lines to their first 240 characters (can contain business data). |
| `Error` | A response could not be parsed even after removal, with the same kind of context. |

## Migrating from 2.x to 3.0

3.0 fixes a number of bugs found in a full review. Some fixes change behaviour or the API:

**Behaviour**

- **BaseUri without trailing slash** now keeps the company. In 2.x such calls went to the server's default company. Check that you were really talking to the company you thought. 2.x also dropped a query string such as `?tenant=t1`; 3.0 keeps it.
- **Values are culture invariant.** On a Danish machine 2.x sent `1,5`, `True` and `14.30.00`. 3.0 sends `1.5`, `true` and `14:30:00`.
- **Create / Update no longer send** get-only properties, properties with a non-public setter, `[XmlIgnore]` or `[XmlAttribute]` properties, `null` values, or `Key` on create. In 2.x a `null` was sent as an empty element, which cleared the field on update.
- **Create / Update follow `XmlSerializer` conventions**: `FooSpecified` / `ShouldSerializeFoo()` are honoured (2.x sent `FooSpecified` as an element and always sent `Foo`), and public fields are sent too.
- **A codeunit response without a SOAP body or result element** throws `SoapResponseException` (2.x returned an empty value).
- **Values are XML escaped** everywhere: filters (`<>0`, `A&B`), read keys, codeunit parameters and create values now work. **Remove any escaping you did yourself** (e.g. `"&lt;&gt;0"`): it would now be escaped twice and filter on the literal text.
- **The Authorization header is set per request.** In `Basic` / `OAuth` mode it overrides an `Authorization` you set on `HttpClient.DefaultRequestHeaders` through `configureHttpClientBuilder`. If you did that in 2.x (e.g. for a bearer token), switch to `AuthenticationMode.OAuth` with an `IAccessTokenProvider`, or to `AuthenticationMode.None` to keep your own header.
- **`GetItemAsync` returns `null`** when the record does not exist, instead of throwing `InvalidOperationException`.
- **SOAPAction headers** now follow the WSDL (`urn:...:Operation`, quoted). The codeunit SOAPAction in 2.x was broken.
- **Basic authentication uses UTF-8**, so non-ASCII user names and passwords work.
- **Options are validated** at startup.
- **`SoapRequestException.SoapEnvelope`** is `null` unless `IncludeEnvelopeInExceptions` is enabled, and `Message` no longer contains the whole response.
- **The default HttpClient is no longer configured.** 2.x configured the application's default `HttpClient` with the NAV base address and credentials, so `IHttpClientFactory.CreateClient()` anywhere in the application sent NAV credentials.
- **The named HttpClients changed** from `""` / `<key>` to `EasySoapClient` / `EasySoapClient:<key>`. If you configured the client in 2.x with `services.AddHttpClient("<key>")...`, move that into the `configureHttpClientBuilder` argument.
- **`GetIdFromKeyAsync`** cuts at the first `": "` (after the table name) instead of the last, and throws `SoapResponseException` instead of returning `""` when the result is missing.
- **`GetAsync` without filters** sends no filter element (2.x sent an empty one).
- **Envelope logging** moved from `Debug` to `Trace`.
- **ReadMultiple** sends `bookmarkKey` before `setSize`, as the WSDL defines.

**API**

- `AddEasySoapClient` / `AddKeyedEasySoapClient` moved to the `Microsoft.Extensions.DependencyInjection` namespace, in a class renamed from `IServiceCollectionExtensions` to `EasySoapClientServiceCollectionExtensions`; remove `using EasySoapClient.Extensions;` if it is no longer needed.
- `IEasySoapService` has new members (`GetAllAsync`, `GetItemByRecIdAsync`, `CreateMultipleAsync`, `UpdateMultipleAsync`, `DeleteAsync`, `IsUpdatedAsync`); hand-written fakes or decorators must implement them.
- `GetAsync` returns `IReadOnlyList<T>` instead of `List<T>`.
- `GetItemAsync` returns `T?`, and `GetItemAsync` / `GetIdFromKeyAsync` only require `IWebServiceElement`.
- `ReadMultipleFilter` is an immutable `readonly record struct`; create a new one instead of setting `Field` / `Criteria`. Its constructor parameters are now `Field` / `Criteria` (named arguments `field:` / `criteria:` must be renamed).
- `CodeUnitRequest` and `CodeUnitParameter` are `readonly record struct`s (no setters); `CodeUnitRequestBuilder.Builder` is sealed.
- `ISearchable` and `IUpdatableWebServiceElement` inherit `Key` from `IKeyedWebServiceElement`; an explicit implementation must now be written as `string IKeyedWebServiceElement.Key`.
- `ReadRequestBuilder` converts to `ReadRequest` explicitly: call `.Build()` (or cast).
- `ReadRequest.Parameters` is an ordered `IReadOnlyList<KeyValuePair<string, object?>>` (was `ImmutableDictionary`), so compound keys are sent in the order given.
- `CodeUnitParameter.ParameterValue` is `object?`; `CodeUnitResponse` is sealed and has a new `Values` dictionary (its positional deconstruction now has two values).
- `SoapRequestException`'s constructor was replaced (it now takes status code and fault details), `SoapEnvelope` is `string?`, and it has new `StatusCode`, `FaultCode` and `FaultString` properties.
- Implementation types are internal: the services under `EasySoapClient.Services`, `ISoapEnvelopeService`, `IParsingService`, `IRequestSenderService` and `IXmlSanitizerService`.
- Removed: `CallMethod`, `MaybeKeyedServiceResolver<T>`, the `Credentials` struct and `PropertyInfoExtensions.FormatNavisionValue`.
- `UpdateAsync` / `GetAllAsync` require `IKeyedWebServiceElement` (`IUpdatableWebServiceElement` and `ISearchable` models still work, as they inherit it); fakes and decorators of `IEasySoapService` must use the new constraints.
- `EasySoapClientOptions` is sealed and has new `AuthenticationMode`, `Domain` and `IncludeEnvelopeInExceptions` properties.
- `ServiceName` is read once per model type: it must not depend on instance state.
- The package targets .NET 8 and .NET 10 (2.x targeted .NET 9 only).

## License

As of version 3.0.0 this project is licensed under the **MIT License with the Commons Clause** condition. See [LICENSE](https://github.com/DrNoLife/Easy-Soap-Client/blob/main/LICENSE).

In short: you may use, modify and include this library in any project, including closed-source and commercial products. You may not sell the library itself, i.e. offer for a fee a product or service whose value derives entirely or substantially from this library.

Versions up to and including 2.7.0 were released under GPL-3.0.
