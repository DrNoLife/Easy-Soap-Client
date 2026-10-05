# Code review – arbejdsliste (2026-10-05, v2.7.0 @ f5c5239)

Fuld rapport (til læsning): https://claude.ai/code/artifact/8207ef7e-36ee-47f2-97c1-d0cc177d7ee8

Denne fil er arbejdslisten til at fikse findings. Hvert punkt: ID · alvor · sted · problem · fix-skitse · afhængigheder.
Linjenumre refererer til commit `f5c5239`. Sæt `[x]` når et punkt er fikset og testet.

Alvor: 5 kritisk, 6 høj, 12 medium, 11 lav, 1 afklaring (35 i alt). F-35 er afgjort og løst.

## Reproduceret

Følgende blev bevist med en net10.0-konsol (ProjectReference til biblioteket), se "Harness" nederst:

| ID | Output |
|---|---|
| F-01 | `Name = "Jensen & Søn <A/S>"` → XmlException; filter `"<>0"` → XmlException |
| F-02 | da-DK: `decimal=1234,5`, `bool=True`, `date=2026-10-05T14.30.00` |
| F-03 | `new Uri(new Uri("https://d/BC/WS/CRONUS%20Danmark"), "Page/Customer")` → `https://d/BC/WS/Page/Customer` |
| F-04 | Assemblies 19 → 80 efter 50 × `ParseSoapResponseSingle` |
| F-05 | Update sender `Namespace`, `Amount=0`, `When=0001-01-01T00.00.00`, `Active=False`, `<Optional></Optional>`, `[XmlIgnore] Computed` |
| F-06 | ``System.Func`1[System.String]:DoIt`` |
| F-09 | Tomt `Read_Result` → InvalidOperationException med hele svaret i message |
| F-13 | `CreateReadEnvelope<M>(default)` → NullReferenceException |
| F-24 | tr-TR: `urn:microsoft-dynamics-schemas/page/ıtems` |

## Kritisk

- [x] **F-01 · Ingen XML-escaping i envelopes**
  - Sted: `Services/SoapEnvelopeService.cs:35-36` (filter Field/Criteria), `:46` (bookmarkKey), `:77` (Read-parametre, både navn og værdi), `:118` (Create-værdier), `:195` (GetId key), `:218` (codeunit-parametre), `:99,122,146,169,214,222` (element-navne fra ServiceName/MethodName). Kun `:165` (Update) escaper.
  - Problem: NAV-filtersyntaks (`<`, `<>`, `&`) og almindelige data (`A&B`) giver ugyldig XML. XML-injection hvis værdier kommer fra slutbrugere.
  - Fix: Omskriv alle `Create*Envelope` til `XmlWriter` (StringWriter eller pooled buffer) med `WriteStartElement("wsns", name, ns)` / `WriteString(value)`. Valider navne med `XmlConvert.VerifyNCName`. Fælles helper: `WriteEnvelope(ns, Action<XmlWriter> body)`.
  - Afhænger af: F-22 (tests først). Løses sammen med F-02, F-05, F-14, F-28.

- [x] **F-02 · Kulturafhængig formatering af værdier**
  - Sted: `Extensions/DateTimeExtensions.cs:10`, `Extensions/PropertyInfoExtensions.cs:11-27`, `SoapEnvelopeService.cs:77,118,165`, codeunit-parametre.
  - Problem: `ToString("yyyy-MM-ddTHH:mm:ss")` bruger kulturens tidsseparator (`.` i da-DK). decimal/double → `1,5`. bool → `True` (XSD: `true`). DateTime.Kind ignoreres. DateOnly, enums med `[XmlEnum]`, `[XmlElement(DataType="date")]` håndteres ikke.
  - Fix: Central `internal static string? FormatValue(object? value, XmlElementAttribute? attr)`: switch på type → `XmlConvert.ToString(bool/decimal/double/int/long/Guid)`, DateTime → `ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)` (eller `XmlConvert.ToString(dt, XmlDateTimeSerializationMode.Unspecified)`), DateOnly → `yyyy-MM-dd`, enum → `[XmlEnum]`-navn ellers `ToString()`, `IFormattable` → `ToString(null, InvariantCulture)`. `ToNavisionString` beholdes (public) men med InvariantCulture.
  - Afhænger af: F-01 (samme omskrivning).

- [x] **F-03 · BaseUri uden afsluttende `/` taber firma-segmentet**
  - Sted: `Extensions/IServiceCollectionExtensions.cs:55`; `RequestSenderService.cs:20,27` (relative URL'er, u-escapede).
  - Problem: RFC 3986-merge erstatter sidste segment → NAV's standardfirma. README anbefaler formen uden `/`.
  - Fix: `var baseUri = options.BaseUri.EndsWith('/') ? options.BaseUri : options.BaseUri + "/";` og `Uri.EscapeDataString(instance.ServiceName)` / `CodeUnitName` i relative URL'er. Opdater README (F-34).
  - Note: Kan ændre adfærd for eksisterende brugere der (uden at vide det) har ramt standardfirmaet → nævn i release notes.

- [x] **F-04 · Hukommelseslæk: XmlSerializer pr. kald**
  - Sted: `Services/ParsingService.cs:30,52`.
  - Problem: `new XmlSerializer(Type, XmlRootAttribute)` caches ikke af runtime → ny dynamisk assembly pr. kald, aldrig unloaded.
  - Fix: `private static readonly ConcurrentDictionary<(Type, string, string), XmlSerializer> Serializers = new();` + `GetOrAdd(key, k => new XmlSerializer(k.Item1, new XmlRootAttribute(k.Item2) { Namespace = k.Item3 }))`. Fælles `GetSerializer(Type, root, ns)`-metode bruges af begge parse-metoder.
  - Test: tæl `AppDomain.CurrentDomain.GetAssemblies().Length` før/efter 50 parses → uændret efter første.

- [x] **F-05 · Create/Update sender alle public properties**
  - Sted: `SoapEnvelopeService.cs:102-119` (Create), `:149-166` (Update).
  - Problem: get-only properties, `[XmlIgnore]`, `null` (→ tomt element), value-type defaults (0/False/0001-01-01) sendes; Update overskriver felter; Create sender tom `<Key/>`.
  - Fix: I `TypeMetadata<T>` (F-14): medtag kun `CanRead && CanWrite && GetMethod.IsPublic && SetMethod.IsPublic`, uden `[XmlIgnore]`, ikke `ServiceName`. Respektér `XxxSpecified` (bool property) og `ShouldSerializeXxx()`. Udelad element når værdi er `null`. Ved Create: udelad `Key` (eller når tom). Ved Update: `Key` skal med og først.
  - Breaking: adfærdsændring (null udelades i stedet for at tømme feltet) → release notes. Overvej at dokumentere: "brug nullable value types for felter du ikke vil ændre".
  - Afhænger af: F-01/F-14.

## Høj

- [x] **F-06 · Codeunit-SOAPAction er `System.Func...`**
  - Sted: `Contracts/CodeUnit/CodeUnitRequest.cs:15` — `$"{GenerateNamespace}:{MethodName}"`.
  - Fix: `$"{GenerateNamespace()}:{MethodName}"`. Overvej `"\"...\""` i SOAPAction-header (SOAP 1.1) og at sætte den på `request.Headers` (ikke content headers) i `RequestSenderService.cs:35`. Brug også `GenerateNamespace()` i `SoapEnvelopeService.cs:211` i stedet for duplikeret streng.

- [x] **F-07 · Basic auth med Encoding.ASCII**
  - Sted: `Services/CredentialsService.cs:30`, `Models/Credentials.cs:12`.
  - Fix: `Encoding.UTF8`. (Hvis en NAV-server kræver Latin1: gør encoding konfigurerbar i options.)

- [x] **F-08 · Ingen options-validering**
  - Sted: `CredentialsService.cs:22-23` (null-checks rammer aldrig; CA2208), `IServiceCollectionExtensions.cs:55,75,92`.
  - Fix: `services.AddOptions<EasySoapClientOptions>(name).Configure(configureOptions).Validate(o => Uri.TryCreate(o.BaseUri, UriKind.Absolute, out _) && !string.IsNullOrEmpty(o.Username), "...").ValidateOnStart();` Fjern de døde null-checks.

- [x] **F-09 · Read not-found → InvalidOperationException med hele svaret**
  - Sted: `Services/ParsingService.cs:49-50`.
  - Fix: `GetItemAsync` returnerer `Task<T?>` (breaking) eller kaster `SoapItemNotFoundException` (ny, ikke-breaking). Aldrig rå svar i exception-message. `ParseSoapResponseSingle` bruges også af Create/Update — der er manglende element en reel fejl.

- [x] **F-10 · SoapRequestException mangler StatusCode/fault-parsing**
  - Sted: `Services/RequestSenderService.cs:40-46`, `Exceptions/SoapRequestException.cs`.
  - Fix: Tilføj `HttpStatusCode StatusCode`, `string? FaultCode`, `string? FaultString`. Pars `soap:Fault/faultstring` (try/catch, fallback til rå tekst). `Message` = `$"SOAP request failed ({(int)status}): {faultString}"`. Overvej at `SoapEnvelope` ikke medtages som default (persondata) eller trunkeres. Tilføj ctor med inner exception.

- [x] **F-11 · Targeter kun net9.0**
  - Sted: `EasySoapClient.csproj:4,24`.
  - Fix: Primært `net10.0` (LTS til nov. 2028). `net8.0` kun som overgangs-target hvis egne apps stadig kører .NET 8 — .NET 8 LTS stopper OGSÅ 2026-11-10. Ved multi-target: `Microsoft.Extensions.Http` 8.0.x for net8 og 10.0.x for net10 (conditional ItemGroups); tjek at `params IEnumerable<>` (C# 13, `CodeUnitRequest.cs:8`) kompilerer for net8 med `<LangVersion>latest</LangVersion>`.
  - Deadline: .NET 8 og .NET 9 support slutter begge 2026-11-10.

## Medium

- [x] **F-12 · HttpResponseMessage disposes ikke** — `RequestSenderService.cs:37` → `using HttpResponseMessage response = ...`.
- [x] **F-13 · Døde guards / default(struct)** — `SoapEnvelopeService.cs:62` (`ThrowIfNullOrEmpty(nameof(...))` er no-op; tjek `request.Parameters is null || request.Parameters.Count == 0`), `:207` (ThrowIfNull på struct, CA2264; tjek i stedet `CodeUnitName`/`MethodName` ikke tomme). Samme for `default(CodeUnitRequest)` (Parameters null → NRE i foreach).
- [x] **F-14 · Reflection pr. envelope uden cache** — `SoapEnvelopeService.cs:102-115,149-162`, `PropertyInfoExtensions.cs`. Fix: `internal static class TypeMetadata<T> { public static readonly PropertyMeta[] Properties; }` med `ElementName`, `Func<T, object?> Getter` (compiled expression eller `CreateDelegate`), `Specified`-getter. Bygges én gang pr. type. Del med F-05.
- [x] **F-15 · Hele svaret bufret som string + XDocument** — `RequestSenderService.cs:49`, `ParsingService.cs:20-38`. Fix: `SendAsync(request, HttpCompletionOption.ResponseHeadersRead)` + `ReadAsStreamAsync` + `XmlReader.Create(stream, settings{Async=true, IgnoreWhitespace=true})`; `ReadToFollowing(ServiceName, ns)` + `serializer.Deserialize(reader.ReadSubtree())`. Fallback: ved XmlException kan stream ikke genlæses → enten buffer til `MemoryStream` først (stadig bedre end string+DOM) eller behold string-vej kun til retry. Kræver at `IRequestSenderService` returnerer stream eller at parsing flyttes ind — interface-ændring (overvej at gøre interfaces internal først, F-21).
- [x] **F-16 · Ingen ConfigureAwait(false)** — alle `await` i `EasySoapService.cs`, `RequestSenderService.cs`.
- [x] **F-17 · DI-registrering** — `IServiceCollectionExtensions.cs`. (a) `AddHttpClient<IRequestSenderService, RequestSenderService>(key, ...)` → `AddHttpClient(key, ...)` (navngivet; typed-registreringen er død og overskrives). (b) `AddTransient` → `TryAddTransient`/`TryAddKeyedTransient`, resolvers kun registreret én gang. (c) `MaybeKeyedServiceResolver<T>` → internal. (d) Overload `AddEasySoapClient(IConfiguration section)`. (e) Overvej namespace `Microsoft.Extensions.DependencyInjection` (breaking for `using`).
- [x] **F-18 · Kun Basic auth** — `IServiceCollectionExtensions.cs:56`. Flyt til `DelegatingHandler` der kalder `ICredentialsProvider` pr. request (understøtter rotation). Dokumentér NTLM: `ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { Credentials = new NetworkCredential(...) })`. OAuth til BC Online: `ITokenProvider`-abstraktion.
- [x] **F-19 · Modeldesign** — `IWebServiceElement.ServiceName` instans → `static abstract` (net7+) eller `[SoapService("Name")]`; fjern `new()`-krav hvor muligt; slå `ISearchable.Key` og `IUpdatableWebServiceElement.Key` sammen i `IKeyedWebServiceElement`. Breaking → 3.0.
- [x] **F-20 · Codeunit-API** — `CodeUnitRequest.cs`, `CodeUnitRequestBuilder.cs:33` (`new(..., _parameters)` deler mutable liste → `[.. _parameters]`), `ParsingService.cs:73-101` (returnér alle child-elementer, ikke kun `return_value`; kast ved manglende Body). Parametre som `object?` formateret via F-02.
- [x] **F-21 · Hele implementeringen er public** — `Services/*`, `Models/Credentials.cs`, `Enums/CallMethod.cs`, `Delegates/*`, `IRequestSenderService`, `ISoapEnvelopeService`, `IParsingService`, `IXmlSanitizerService`. → `internal sealed`. Breaking → 3.0. Tilføj `InternalsVisibleTo` til testprojekt.
- [x] **F-22 · Ingen tests/CI** — Opret `EasySoapClient.Tests` (xUnit). Golden files for de seks envelopes (inkl. `&`, `<>`, da-DK, null, get-only, XmlIgnore), parsing-fixtures (liste, single, tomt Read_Result, fault, ulovlige tegn), fake `HttpMessageHandler` for URL/headers (SOAPAction, Authorization, BaseUri med/uden `/`). GitHub Actions: build + test + pack.
- [x] **F-23 · csproj/NuGet-metadata** — `<Version>$(VersionPrefix)2.7.0</Version>` → `<Version>2.7.0</Version>`. Tilføj `PackageId`, `RepositoryUrl`, `PackageProjectUrl`, `PublishRepositoryUrl`, `Microsoft.SourceLink.GitHub`, `IncludeSymbols`+`SymbolPackageFormat=snupkg`, `GenerateDocumentationFile`, `ContinuousIntegrationBuild` (i CI), `PackageTags=soap;navision;business-central;dynamics`. Overvej at fjerne `GeneratePackageOnBuild`.

## Lav

- [x] **F-24 · ToLower() kulturfølsom** — `Extensions/IWebServiceElementExtensions.cs:9` → `ToLowerInvariant()`.
- [x] **F-25 · Dobbelt enumerering** — `ParsingService.cs:25` (`elements.Any()` + foreach), `EasySoapService.cs:25` (`filters.Any()` på brugerens IEnumerable).
- [x] **F-26 · Transient for stateless services** — `IServiceCollectionExtensions.cs:83-85,103-105` → Singleton for SoapEnvelope/Parsing/XmlSanitizer.
- [x] **F-27 · Logging** — fulde envelopes på Debug (`SoapEnvelopeService.cs:54,85,127,174,200,226`), XML-kontekst fra svar i `ParsingService.cs:113,133` (persondata). → `[LoggerMessage]` source-gen (CA1848), overvej Trace + trunkering.
- [x] **F-28 · Whitespace i envelopes** — forsvinder med XmlWriter (F-01).
- [x] **F-29 · GetIdFromKey-parsing** — `EasySoapService.cs:88` (`Split(": ").Last()` → `LastIndexOf(": ")`), `ParsingService.cs:57` (ubrugt `<T>`), `:70` (returnerer `""` ved manglende element → kast).
- [x] **F-30 · Tomt standardfilter** — `EasySoapService.cs:25-29` sender `<filter><Field/><Criteria/></filter>`; send ingen filtre. Verificér mod NAV at nul filtre returnerer alt.
- [x] **F-31 · Mutable structs / død type / implicit operator** — `Models/ReadMultipleFilter.cs` → `readonly record struct`; `Models/Credentials.cs` bruges ikke internt (fjern eller brug i DI); `ReadRequestBuilder.cs:53` implicit operator kan kaste → explicit (breaking).
- [x] **F-32 · Manglende operationer** — Delete, CreateMultiple, UpdateMultiple, ReadByRecId, IsUpdated; `IAsyncEnumerable<T> GetAllAsync<T>(filters, pageSize)` der følger `bookmarkKey` (kræver Key på T); `size=10` default; `List<T>` → `IReadOnlyList<T>` (breaking).
- [x] **F-33 · Kodehygiejne** — ubrugte usings (`SoapEnvelopeService.cs:7` System.Drawing, `:6`, `CredentialsService.cs:3`, `EasySoapService.cs:8`...); copy-paste tekster `SoapEnvelopeService.cs:184` ("update operations") og `:200` ("SOAP Update envelope"); CA2208 `CredentialsService.cs:22-23`; `CredentialsService.Username/Password` public (fjern); forældet summary `EasySoapService.cs:47`; blandet BOM (nye filer uden). Slå `TreatWarningsAsErrors` + `AnalysisLevel=10.0-recommended` til (giver i dag 40+ warnings).
- [x] **F-34 · README forældet** — `Namespace` i model-eksempler (bryder Create/Update indtil F-05), BaseUri uden `/` (F-03), "repositories", "libraary", not-found-adfærd, `size`-semantik, nullable-mønster for Update (F-05), auth-muligheder (F-18).

## Afklaring

- [x] **F-35 · Licens** — Besluttet 2026-10-05: MIT + Commons Clause v1.0 (brug i kommercielle/lukkede produkter tilladt, salg af selve pakken ikke). `LICENSE` erstattet; csproj bruger `PackageLicenseFile` (Commons Clause har intet SPDX-id, så ikke `PackageLicenseExpression`) og pakker `LICENSE`. Versioner ≤ 2.7.0 forbliver GPL-3.0 for dem der har hentet dem.

## Status – 3.0.0 (2026-10-05)

Alle 35 findings er lukket i 3.0.0, plus 3 nye fund undervejs og 4 review-runder (R1–R4) af selve ændringerne. Verifikation: `dotnet test EasySoapClient/EasySoapClient.Tests` (net8.0 + net10.0), build med `TreatWarningsAsErrors` + `AnalysisLevel=latest-recommended` + `EnforceCodeStyleInBuild` (IDE0005), `dotnet pack` (LICENSE, README, snupkg, SourceLink), `dotnet list package --vulnerable` (ingen).

| ID | Fix | Bevis (test) |
|---|---|---|
| F-01 | Alle envelopes bygges med `XmlWriter`; navne valideres med `XmlConvert.VerifyNCName` | `SoapEnvelopeServiceTests`: `Create_escapes_values`, `Create_value_cannot_inject_elements`, `ReadMultiple_escapes_nav_filter_syntax_and_bookmark`, `Read_escapes_and_formats_parameters`, `Key_operations_escape_key…`, `Codeunit_parameters_are_escaped…`, `Invalid_element_names_are_rejected` |
| F-02 | `Serialization/SoapValueFormatter` (XmlConvert/InvariantCulture, DataType date/time, DateTime som 2.x `yyyy-MM-ddTHH:mm:ss` (se R2-02), DateOnly/TimeOnly/DateTimeOffset, XmlEnum); `ToNavisionString` invariant | `Values_are_formatted_culture_invariant` (da-DK), `Datetime_is_sent_as_whole_seconds_without_zone_like_2x`, `ToNavisionString_is_culture_invariant` |
| F-03 | `CreateBaseAddress` tilføjer `/`; service-/codeunit-navne URL-escapes | `BaseUri_without_trailing_slash_keeps_company_segment`, `Codeunit_request_has_correct_soap_action_and_url` |
| F-04 | Statisk serializer-cache i `ParsingService.GetSerializer` | `Serializers_are_cached_so_no_assembly_is_generated_per_call` |
| F-05 | `Serialization/TypeMetadata` (som XmlSerializer: public get+set, public fields og get-only collections — se R2-09, R3-02, R4), `[XmlIgnore]`/`[XmlAttribute]` springes over, `null` udelades, `FooSpecified`/`ShouldSerializeFoo`, Key aldrig på Create og først på Update | `Update_sends_only_settable_non_null_properties_with_key_first`, `Create_never_sends_key_get_only_or_ignored_properties`, `Specified_pattern_controls_whether_value_is_sent` |
| F-06 | `GenerateNamespace()`; SOAPAction i anførselstegn på request-headers; page-SOAPAction nu `urn:…:Operation` som i WSDL | `Codeunit_soap_action_contains_namespace_not_delegate_name`, `Page_request_has_quoted_soap_action…`, `Codeunit_request_has_correct_soap_action_and_url` |
| F-07 | UTF-8 | `Page_request_has_quoted_soap_action_content_type_and_basic_auth` (Søren/æøå) |
| F-08 | `EasySoapClientOptionsValidator` + `ValidateOnStart` | `Invalid_options_fail_validation` (3 cases), `Invalid_options_fail_host_start` |
| F-09 | `GetItemAsync`/`GetItemByRecIdAsync` returnerer `T?`; Create/Update uden record → `SoapResponseException` uden rå data | `GetItemAsync_returns_null_when_not_found`, `ParseSingle_reports_not_found_without_throwing`, `Create_without_record_in_response_throws_short_message` |
| F-10 | `SoapRequestException` med `StatusCode`, `FaultCode`, `FaultString`, kort `Message`; envelope kun ved `IncludeEnvelopeInExceptions` | `Soap_fault_becomes_exception_with_status_and_fault_string`, `Envelope_is_included_in_exception_only_when_enabled` |
| F-11 | `net8.0;net10.0`, M.E.Http 8.0.1 / 10.0.0 | Tests kører på begge TFM'er |
| F-12 | `using` på request og response | Kodegennemgang (`RequestSenderService`) |
| F-13 | Rigtige guards for `default(ReadRequest)`/`default(CodeUnitRequest)` | `Read_with_default_request_throws_argument_exception`, `Codeunit_default_request_throws_argument_exception` |
| F-14 | `TypeMetadata`-cache med kompilerede getters | Dækket af envelope-tests; cache pr. type |
| F-15 | Parsing direkte fra (bufret) response-stream med `XmlReader`; string kun i fallback; DTD forbudt | `ParsingServiceTests` (alle), `Dtd_is_rejected`, `Illegal_character_references_are_sanitized_and_parsing_retried` |
| F-16 | `ConfigureAwait(false)` på alle awaits | Kodegennemgang |
| F-17 | Navngivne klienter (`EasySoapClient`, `EasySoapClient:<key>`), `TryAdd*`, resolver-delegate fjernet, `IConfiguration`-overloads, namespace `Microsoft.Extensions.DependencyInjection` | `Registering_several_clients_does_not_duplicate_services`, `Keyed_clients_use_their_own_base_address_and_credentials`, `Options_can_be_bound_from_configuration` |
| F-18 | `SoapAuthenticator` (pr. request, i kalderens scope — se R1-01), `AuthenticationMode` Basic/Windows/OAuth/None, `IAccessTokenProvider` | `OAuth_mode_uses_registered_token_provider`, `OAuth_mode_without_token_provider_fails_clearly`, `Custom_credentials_provider_is_called_per_request`, `Custom_credentials_registered_before_the_client_win`, `Configuration_reload_applies_to_credentials_without_restart`, `None_mode_sends_no_authorization_header` |
| F-19 | `IKeyedWebServiceElement` som fælles base (ISearchable/IUpdatable er aliaser); `ServiceName` caches pr. type (`ServiceMetadata<T>`). `new()` bevares bevidst: XmlSerializer kræver parameterløs konstruktør | Kodegennemgang + alle tests |
| F-20 | `object?`-parametre formateret som F-02; `CodeUnitResponse.Values`; snapshot af parametre; fejl ved manglende Body | `ParseCodeUnitResponse_returns_return_value_and_by_ref_parameters`, `…_throws_on_unexpected_shape`, `Builder_reuse_does_not_change_built_requests`, `Request_snapshots_caller_collection` |
| F-21 | Services/interfaces internal sealed; `Credentials`, `CallMethod`, `MaybeKeyedServiceResolver`, `PropertyInfoExtensions` fjernet | `Implementation_types_are_not_public` |
| F-22 | `EasySoapClient.Tests` (xUnit, net8+net10) + `.github/workflows/ci.yml` | 133 tests grønne (se runde 1–4) |
| F-23 | csproj: Version 3.0.0, PackageId, license file, repo-URL'er, snupkg, XML docs, CI-build, tags; `GeneratePackageOnBuild` fjernet | `dotnet pack` + nuspec gennemset |
| F-24 | `ToLowerInvariant` | `Namespace_is_culture_invariant` (tr-TR) |
| F-25 | Forsvundet med streaming-parser og `Materialize(filters)` | — |
| F-26 | Envelope/Parsing/Sanitizer singleton | `Registering_several_clients_does_not_duplicate_services` |
| F-27 | `[LoggerMessage]` (Logging/Log.cs); envelopes på Trace | Build uden CA1848/CA1873 |
| F-28 | Ingen whitespace (XmlWriter) | `Envelope_has_no_padding_whitespace` |
| F-29 | Første `": "` (tabelnavn-præfiks); ubrugt `<T>` fjernet; manglende resultat → `SoapResponseException` | `Delete_IsUpdated_and_GetIdFromKey_parse_results`, `GetIdFromKey_without_result_element_throws` |
| F-30 | Ingen filter-elementer uden filtre | `ReadMultiple_without_filters_sends_no_filter_element` |
| F-31 | `readonly record struct ReadMultipleFilter`; `Credentials` fjernet; explicit conversion | `ReadMultipleFilter_is_an_immutable_value`, `ReadRequestBuilder_conversion_is_explicit_and_validates` |
| F-32 | `GetAllAsync` (IAsyncEnumerable), `GetItemByRecIdAsync`, `CreateMultipleAsync`, `UpdateMultipleAsync`, `DeleteAsync`, `IsUpdatedAsync`; `IReadOnlyList<T>`; `size` dokumenteret (0 = alle) | `GetAllAsync_pages_with_bookmark_key`, `CreateMultiple_and_ReadByRecId_round_trip`, `Delete_IsUpdated…`, `Multiple_operations_wrap_records_in_list_element` |
| F-33 | Ubrugte usings fjernet og håndhævet (IDE0005 som fejl), copy-paste-tekster rettet, BOM ensrettet (.editorconfig) | Build |
| F-34 | README omskrevet som fuld dokumentation inkl. migrationsguide | README-eksempler kompileret mod biblioteket |
| F-35 | MIT + Commons Clause | — |

### Fundet undervejs (runde 1)

| ID | Fund | Fix |
|---|---|---|
| F-36 | **Høj.** 2.x registrerede den ikke-keyede klient som navngivet HttpClient `""` = applikationens *default* klient. Al anden kode der brugte `IHttpClientFactory.CreateClient()` fik NAV's BaseAddress og Basic-credentials. | Navngivne klienter med eget præfiks. Test: `Default_http_client_of_the_application_is_not_configured_with_nav_credentials` |
| F-37 | **Lav.** ReadMultiple sendte `setSize` før `bookmarkKey`; WSDL-rækkefølgen er filter, bookmarkKey, setSize. | Rettet. Test: `ReadMultiple_follows_wsdl_order_filter_bookmark_setSize` |
| F-38 | **Lav.** XmlException ved fejlet retry gentog linje/position i beskeden. | Rettet. Test: `Structurally_broken_xml_still_throws_with_original_as_inner` |

### Review-runde 1 (to uafhængige reviewere af 3.0-ændringerne)

| ID | Alvor | Fund | Fix | Test |
|---|---|---|---|---|
| R1-01 | Høj | Auth-`DelegatingHandler` fik providers fra IHttpClientFactory's handler-scope (lever 2 min+): scoped/transient `IAccessTokenProvider`/`ICredentialsProvider` blev fanget og delt på tværs af requests/brugere; transient disposables blev aldrig disposed | Handler fjernet. `SoapAuthenticator` oprettes sammen med `IEasySoapService` i kalderens scope og sætter headeren pr. request i `RequestSenderService` | `Scoped_token_provider_gets_the_callers_scope` |
| R1-02 | Høj | README anbefalede `AddStandardResilienceHandler()`: retry af ikke-idempotente POSTs på HTTP 500/timeout → dubletter | README-afsnit om retries omskrevet med advarsel og regler | — (dokumentation) |
| R1-03 | Høj | `[XmlArray]`/`[XmlArrayItem(typeof(X))]` uden navn giver `""` → Create/Update kastede | Tomme navne normaliseres til fallback (som XmlSerializer); tydelig fejl ved tomt element-navn | `Array_attributes_without_names_fall_back_like_XmlSerializer` |
| R1-04 | Høj | `DateOnly`/`TimeOnly` læses stille som default af XmlSerializer på .NET 8 → Update kunne overskrive datoer | Runtime-probe (`XmlSerializerSupport`); på .NET 8 kastes `NotSupportedException` ved model-typer med disse properties (parsing og envelopes) | `DateOnly_round_trips_or_is_rejected_never_silently_defaulted`, `DateOnly_is_sent_where_the_runtime_can_read_it_back_and_rejected_otherwise` |
| R1-05 | Medium | Primær handler blev altid erstattet med ny `SocketsHttpHandler` → app-wide defaults (proxy, certs) ignoreret | Handleren erstattes aldrig; Windows-credentials sættes på den eksisterende (Sockets/HttpClientHandler), ellers tydelig fejl | `App_wide_primary_handler_defaults_still_apply`, `Windows_mode_sets_credentials_on_the_primary_handler_without_replacing_it`, `Windows_mode_with_unknown_primary_handler_fails_clearly` |
| R1-06 | Medium | Keyed klient faldt tilbage til ikke-keyed token-provider → token for tenant A sendt til tenant B | Keyed klienter bruger kun keyed providers (fail closed) | `Keyed_client_does_not_fall_back_to_non_keyed_token_provider` |
| R1-07 | Medium | Samme klient registreret to gange stakkede to auth-handlere | Forsvundet med R1-01 | `Registering_the_same_client_twice_authenticates_once_per_request` |
| R1-08 | Medium | Validator krævede Username for Basic, også med egen `ICredentialsProvider` | Tjekket flyttet til default `CredentialsService` (første request) | `Default_credentials_without_username_fail_at_request_time_but_custom_provider_needs_none` |
| R1-09 | Medium | Sammensatte Read-nøgler sendt i tilfældig rækkefølge (`ImmutableDictionary`) — også i 2.7.0 | `ReadRequest.Parameters` er nu ordnet liste; gentaget navn beholder position | `Compound_read_keys_keep_their_order` |
| R1-10 | Medium-lav | `.sln` refererede gitignoret `EasySoapClient.UserTest` → build fejler på frisk clone; ingen `global.json`; `latest`-analyzers + NuGet audit som fejl kunne bryde CI | UserTest fjernet fra sln, `global.json` (10.0.100, latestFeature), `AnalysisLevel=10.0-recommended`, NU1901-1904 ikke som fejl | `dotnet build EasySoapClient.sln` |
| R1-11 | Lav | Fallback-dekodning ignorerede XML-deklarationens encoding | BOM → deklareret encoding → UTF-8 | `Sanitizing_fallback_honours_declared_encoding` |
| R1-12 | Lav | `[Flags]`-enums skrevet som `"A, B"` (XmlSerializer: `"A B"`) | Mellemrums-separeret med XmlEnum-navne | `Flags_enums_are_space_separated_with_xml_enum_names` |
| R1-13 | Lav | Create/Update brugte `item.ServiceName` i envelope men type-cachet navn i URL/parsing | Type-cachet navn overalt (dokumenteret: ServiceName må ikke afhænge af instans) | — |
| R1-14 | Lav | Ugyldigt ServiceName gav `TypeInitializationException` for altid | Lazy beregning, tydelig `InvalidOperationException` | — |
| R1-15 | Lav | Serializer-cache: samtidig første brug kunne bygge to serializere | `Lazy<XmlSerializer>` i cachen | `Concurrent_first_use_generates_at_most_one_serializer_assembly` (runde 3; mutation uden Lazy giver 11–14 assemblies → fejler) |
| R1-16 | Lav | Test-huller: Windows, ValidateOnStart, dobbelt-registrering, disposal, vakuøs Authorization-assert | Tests tilføjet; vakuøs assert fjernet | `Invalid_options_fail_host_start`, `Response_is_disposed_after_parsing` m.fl. |
| R1-17 | Lav | Options-reload inkonsistent dokumenteret | README præciserer hvad der læses pr. request, pr. service og pr. handler | — |

### Review-runde 2 (to nye uafhængige reviewere)

| ID | Alvor | Fund | Fix | Test |
|---|---|---|---|---|
| R2-01 | Høj | Windows-mode (R1-05) skrev credentials på en primær handler, der kan være delt mellem klienter → klient A autentificerede som B | Handler med *andre* credentials afvises; samme credentials = no-op | `Windows_mode_refuses_a_primary_handler_shared_between_clients` |
| R2-02 | Medium | DateTime-formatet ændret i forhold til 2.x (brøkdele af sekunder + `Z` ved UTC) → mulig tidszone-konvertering i NAV | Tilbage til 2.x-format `yyyy-MM-ddTHH:mm:ss` uanset Kind; `DateTimeOffset` til eksplicit offset | `Datetime_is_sent_as_whole_seconds_without_zone_like_2x` (3 kinds) |
| R2-03 | Medium | README-model med `string … = ""` sender tom streng ved update (overskriver felter) | README: nullable felter anbefales, tomme strenge dokumenteret | — (dokumentation) |
| R2-04 | Medium | Migrationsguide manglede ~12 breaking changes | Tilføjet (klasse-omdøbning, nye interface-medlemmer, sealed/readonly typer, ctor-ændringer, HttpClient-navne, GetIdFromKey, logging-niveau m.m.) | — |
| R2-05 | Medium | R1-03 ufuldstændig: collection-elementnavne fulgte ikke XmlSerializer (`<String>` vs `<string>`, `[XmlType]` ignoreret) | `DefaultElementName`: XSD-navne for primitiver, `[XmlType]`, ellers typenavn | `Collection_item_names_follow_XmlSerializer_defaults` (sammenlignet med XmlSerializer selv) |
| R2-06 | Medium | Fallback med ukendt deklareret encoding (fx windows-1252) faldt stille tilbage til UTF-8 → korrupt tekst; utf-16-deklaration på UTF-8-bytes fejlede | Code pages via `CodePagesEncodingProvider`; ukendt encoding → `XmlException` med navnet; UTF-16/32-deklaration på ASCII-kompatible bytes læses som UTF-8 | `Fallback_reads_windows_code_pages`, `Fallback_refuses_unknown_encoding_instead_of_guessing`, `Fallback_reads_utf8_bytes_declared_as_utf16_as_utf8` |
| R2-07 | Lav-medium | Log-kontekst var hele linjer; NAV svarer på én linje → hele svaret i loggen | Kontekst skæres til ±120 tegn omkring fejlpositionen | `Logged_context_is_a_window_not_the_whole_single_line_response` |
| R2-08 | Lav-medium | DateOnly-guard (R1-04) så ikke på public fields, men afviste get-only computed properties | Tjekker præcis de members XmlSerializer læser (fields, read/write props, get-only collections) | `DateOnly_support_matches_the_runtime` (nu pr. runtime-version, ikke tautologisk) |
| R2-09 | Lav | Public fields blev ikke sendt (men læst af XmlSerializer) | `TypeMetadata` inkluderer public, ikke-readonly fields | `Public_fields_are_sent_like_XmlSerializer_does` |
| R2-10 | Lav | Sanitizer kvadratisk ved mange `&` uden `;` (400k → 1,4 s); timing-test kunne ikke opdage det | Afgrænset søgning efter `;`; testene skærpet og mutation-verificeret (gammel kode: 9,5 s → fejler) | `Many_ampersands_without_semicolon_are_handled_in_linear_time`, `Many_unterminated_comments…` |
| R2-11 | Lav | `[Flags]` 0 uden navngivet medlem sendt som `"0"` | Tom værdi som XmlSerializer | `Flags_enum_zero_without_named_member_is_empty` |
| R2-12 | Lav | `ReadRequest` havde reference-lighed | Værdi-lighed + hash | `ReadRequest_has_value_equality` |
| R2-13 | Lav | `CodeUnitRequest with { Parameters = … }` omgik snapshot | Snapshot i `init`-accessor | `With_expression_also_snapshots_parameters` |
| R2-14 | Lav | Disposed scope gav ObjectDisposedException selv i Basic (options hentet via scope pr. request) | `IOptionsMonitor` hentes én gang; scope-binding dokumenteret | — |
| R2-15 | Lav | Probe-exceptions ud over InvalidOperationException ville blive cachet | Alle exceptions → "ikke understøttet" (fail closed) | — |
| R2-16 | Lav | XML-docs: "resilience" anbefalet, "null returns all records", Error-log/netværks-exceptions udokumenteret, LICENSE-link virker ikke på nuget.org | Rettet | — |
| R2-17 | Lav | Svage tests: cancellation med for-annulleret token, "registering twice" registrerede aldrig samme navn, Windows-test testede kun None, cache-test ikke samtidig | Ny blokerende-handler cancellation-test; samme-navn registrering; Windows-tests; `Concurrent_first_use_builds_one_serializer`; `Type_metadata_is_built_once_per_type`; `Invalid_service_name_fails_clearly_every_time` | se navne |
| R2-18 | Lav | CI: `10.0.x` + `latestFeature` kunne trække nyt SDK-band | `setup-dotnet` læser `global.json` | — |

Verificeret uændret OK af runde 2: R1-01, 06, 07, 08, 09, 10, 12, 13, 14, 15; pakning og CI-trin kørt på frisk clone (92 → 109 tests efter runde 2; 133 efter runde 4).

### Review-runde 3 (to nye uafhængige reviewere)

| ID | Alvor | Fund | Fix | Test |
|---|---|---|---|---|
| R3-01 | Medium | BaseUri med query (`?tenant=t1`, multitenant BC) tabte firma og tenant (også i 2.7.0) | Sti normaliseres med `/`, query sendes med på hver request; fragment afvises af validatoren | `BaseUri_query_is_kept_on_every_request`, `BaseUri_with_fragment_is_rejected` |
| R3-02 | Medium | Public fields (R2-09) ignorerede `FooSpecified`, og flaget blev sendt | `TypeMetadata` omskrevet: fields og properties behandles ens (Specified, ShouldSerialize, Key, XmlIgnore/XmlAttribute) | `Specified_pattern_works_for_public_fields_and_flag_is_never_sent` |
| R3-03 | Medium | Migrationsguide manglede: dobbelt-escaping for brugere der selv escapede; Authorization pr. request overstyrer `DefaultRequestHeaders` | Tilføjet i README | `None_mode_keeps_a_default_authorization_header_set_by_the_user` |
| R3-04 | Lav-medium | `[Flags]` med negativt medlem → `OverflowException` | Sammenligning med `Enum.ToObject(type, 0)` | `Flags_enum_with_negative_member_is_formatted` |
| R3-05 | Lav-medium | "Samme credentials = no-op"-grenen (R2-01) var utestet | Test tilføjet | `Windows_mode_accepts_a_handler_that_already_has_the_same_credentials` |
| R3-06 | Lav | `CodeUnitRequest` sammenlignede snapshot-array pr. reference; `ToString` viste typenavn | Værdi-lighed, hash og `PrintMembers` | `CodeUnitRequest_has_value_equality_and_readable_ToString` |
| R3-07 | Lav | Overskrevet virtuel property skrevet sidst i stedet for på base-positionen | Position fra original deklaration (`GetBaseDefinition`) | `Overridden_property_keeps_base_class_position_like_XmlSerializer` (sammenlignet med XmlSerializer) |
| R3-08 | Lav | Sanitizer beholdt ulovlige referencer > 24 tegn (foranstillede nuller) og overflow-værdier, som 2.7.0 fjernede | Cifre scannes manuelt (vilkårlig længde, stadig lineært); overflow = ulovlig | 4 nye `Removes_only_illegal_characters`-cases |
| R3-09 | Lav | Encoding-fallback: deklaration > 256 bytes gættede UTF-8; `utf-7` gav `NotSupportedException` | 1024 bytes; for lang deklaration og ikke-understøttede encodings → `XmlException` | `Fallback_with_unsupported_or_unreadable_encoding_throws_xml_exception` |
| R3-10 | Lav | README: .NET 9 ikke nævnt, log-kontekst pr. linje, AggregateException ved host-start, DateTimeOffset-brøkdele; migrationsguide manglede PropertyInfoExtensions, slettede (ikke internal) typer, Specified/fields/XmlAttribute, UpdateAsync-constraint, codeunit uden body | Rettet; NotSupportedException-teksten nævner den faktiske runtime | — |
| R3-11 | Lav | Arbejdslisten citerede ikke-eksisterende testnavne; R1-15 havde reelt ingen test | Rettet; ny R1-15-test mutation-verificeret | — |

Bekræftet OK af runde 3: R2-01 (handler-rotation, DefaultCredentials-identitet), R2-02, R2-05, R2-06, R2-07, R2-08, R2-10, R2-11, R2-12, R2-13, R2-14, R2-15; ingen flakiness over 3×2 kørsler; 8/10 mutationer fanget (de 2 overlevende er lukket ovenfor).

### Review-runde 4 (to nye uafhængige reviewere) — **in the clear**

Begge reviewere: ingen fund af Medium eller højere. Kode: alle runde 3-ændringer verificeret mod XmlSerializer med probes. Docs/tests: alle 18 README-kodeblokke kompilerer (net8 + net10), public API 2.7.0 ↔ 3.0 sammenlignet medlem for medlem mod migrationsguiden, 8/8 mutationer af runde 3-fixes fanget, ingen flakiness (3×2 kørsler).

Lave punkter, rettet alligevel:

| ID | Fund | Fix | Test |
|---|---|---|---|
| R4-01 | Get-only collections (CA2227-mønster) blev ikke sendt; XmlSerializer skriver dem | Sendes nu | `Get_only_collections_are_sent_like_XmlSerializer_does` |
| R4-02 | Field + property med samme navn (`new`) → `ToDictionary`-crash; `new` med anden type → dobbelte elementer; position for hidden members | Medlemmer dedupliceres pr. navn: mest afledte bruges, på første deklarations position | `Hidden_members_and_getter_only_overrides_are_written_once` |
| R4-03 | Override med kun getter af virtuel get/set-property blev ikke sendt | Setter på original deklaration tæller | samme test |
| R4-04 | Cykliske objektgrafer → StackOverflow (kan ikke fanges) | `InvalidOperationException` ved cyklus | `Circular_references_fail_instead_of_overflowing_the_stack` |
| R4-05 | Udefineret enum-værdi sendt som tal; `char` sendt som tegn | Som XmlSerializer: exception / talværdi | `Undefined_enum_values_and_chars_follow_XmlSerializer` |
| R4-06 | Misvisende fejltekst ved afkortet XML-deklaration | Omformuleret | — |
| R4-08 | (Fundet ved verifikation af R4-01, Medium) Get-only collections typet som interface (fx beregnet `IReadOnlyCollection<T>`) blev sendt; XmlSerializer udelader dem | Kun konkrete, ikke-abstrakte, ikke-dictionary collections sendes get-only; samme regel i DateOnly-guarden | `Get_only_interface_collections_and_dictionaries_are_not_sent` |
| R4-09 | `new`-hiding af samme type: forkert position; getter-only `new` over get/set-base blev ikke sendt | Position og skrivbarhed ud fra alle deklarationer af navnet i hierarkiet (base først) | `New_hiding_properties_match_XmlSerializer_order_and_values` (sammenlignet med XmlSerializer) |
| R4-10 | (Verifikation af R4-09, Lav) `new` over en base-deklaration som XmlSerializer ikke skriver (get-only) fik base-positionen | Position fra første deklaration der faktisk skrives | `Settable_new_over_get_only_base_is_positioned_like_XmlSerializer` |
| R4-11 | DateOnly-guarden så kun på den mest afledte setter (getter-only override slap igennem på .NET 8) og sprang `[XmlAttribute]` over | Fælles regel `TypeMetadata.IsSerializedProperty` + attribut-properties | `DateOnly_guard_covers_getter_only_overrides_and_attributes` (net8) |
| R4-12 | `List<DateOnly>`-elementer hed `<DateOnly>` (XmlSerializer: `<dateOnly>`) | XSD-navne for DateOnly/TimeOnly | `DateOnly_list_items_are_named_like_XmlSerializer` (net10) |
| R4-07 | Forældede rækker i denne fil (F-05, F-18, AnalysisLevel, testantal) og README-præciseringer (runtime-check-tidspunkt, log-vindue, codeunit uden result, tenant-query i 2.x) | Rettet | — |

Afsluttende verifikation af R4-08/R4-09: 75 modeller sammenlignet med XmlSerializer på net8 og net10 — identisk output (in the clear). R4-10..12 er derefter rettet og verificeret med tests der sammenligner direkte med XmlSerializer.

Ikke rettet (bevidst, eksotisk og uden betydning for NAV-modeller): en public base-property skjult af en `internal new` sendes ikke (XmlSerializer sender base-værdien); items i ikke-generisk `ArrayList` får typenavn i stedet for `anyType`; udefinerede `[Flags]`-værdier sendes som tal.

Ikke ændret (bevidst): Warning-logning ved parse-fejl indeholder fortsat et par linjers kontekst fra svaret (funktion indført i 2.6.1); dokumenteret i README.

### Live-verifikation mod produktions-NAV (2026-10-05, kun læsning)

Testprojektet `EasySoapClient/EasySoapClient.UserTest` (gitignoret; credentials i user-secrets; en `ReadOnlyGuard`-handler afviser alt andet end page-læsninger og er selv testet offline først) kørte mod `https://fiprosnav.dk:10047/WEBSERVICE/WS/Fipros A/S/` og Fipros Nutrition ApS under da-DK-kultur: 17 læse-requests, 0 fejl.

Verificeret live: ReadMultiple, filtre med `<>` og `&` (F-01), GetAllAsync-paging over 3 sider = manuel bookmark-paging, Read (fundet + `null` ved ikke-eksisterende, F-09), GetRecIdFromKey, ReadByRecId, IsUpdated, DateTime-parsing under da-DK, keyed klient med andet firma i BaseUri med mellemrum (F-03), det nye SOAPAction-format (`"urn:…:Operation"`, F-06) og SOAP-fault-parsing (F-10: NAV's fejltekst kom ud som `FaultString`).

Ikke verificeret live (bevidst, da det er skrivninger): Create, CreateMultiple, Update, UpdateMultiple, Delete og codeunit-kald — de er testet mod forventet XML.

## Anbefalet rækkefølge

1. F-22 (tests, skal fejle på F-01/02/05/06 først)
2. 2.7.1 hurtige fixes: F-06, F-04, F-07, F-03, F-24, F-12, F-13, F-23 (Version-linjen)
3. 2.8.0 envelope-omskrivning: F-01 + F-02 + F-05 + F-14 + F-28 + codeunit-del af F-20
4. 2.9.0 robusthed: F-08, F-09 (exception-variant), F-10, F-15, F-16, F-27, F-30
5. Pakning: F-11 (net10.0; .NET 8+9 EOL 2026-11-10), resten af F-23, CI (F-35 er klaret)
6. 3.0 breaking: F-17, F-18, F-19, F-21, F-29, F-31, F-32, F-34

## Harness (reproduktion)

net10.0-konsol med `<ProjectReference>` til `EasySoapClient.csproj` og `Microsoft.Extensions.Logging.Abstractions`. Kerne:

```csharp
CultureInfo.CurrentCulture = new CultureInfo("da-DK");
Console.WriteLine(CodeUnitRequest.CreateRequest("MyCU", "DoIt").GenerateSoapActionDefinedNamespace());
Console.WriteLine(new Uri(new Uri("https://d/BC/WS/CRONUS"), "Page/Customer"));
var env = new SoapEnvelopeService(NullLogger<SoapEnvelopeService>.Instance);
XDocument.Parse(env.CreateCreateEnvelope(new M { Name = "Jensen & Søn" }));          // kaster
XDocument.Parse(env.CreateReadMultipleEnvelope([new("Balance", "<>0")], 10, null, new M())); // kaster
var ps = new ParsingService(NullLogger<ParsingService>.Instance, new XmlSanitizerService());
int before = AppDomain.CurrentDomain.GetAssemblies().Length;
for (int i = 0; i < 50; i++) ps.ParseSoapResponseSingle<M>(readResponseXml, new M());
Console.WriteLine($"{before} -> {AppDomain.CurrentDomain.GetAssemblies().Length}");
```

Disse cases bør blive til rigtige tests i F-22.
