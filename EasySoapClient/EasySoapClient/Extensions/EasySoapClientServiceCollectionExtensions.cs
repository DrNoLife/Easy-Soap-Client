using System.Net;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using EasySoapClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Placed in the DI namespace by convention, so the registration methods are found without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers EasySoapClient in an <see cref="IServiceCollection"/>.
/// </summary>
public static class EasySoapClientServiceCollectionExtensions
{
    private const string HttpClientNamePrefix = "EasySoapClient";

    /// <summary>
    /// Registers a non-keyed <see cref="IEasySoapService"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Configures base address and authentication.</param>
    /// <param name="configureHttpClientBuilder">
    /// Optional further configuration of the HttpClient (timeouts, handlers, proxies). Be careful with retry policies:
    /// every SOAP call is a non-idempotent POST and NAV returns HTTP 500 for business errors.
    /// </param>
    public static IServiceCollection AddEasySoapClient(
        this IServiceCollection services,
        Action<EasySoapClientOptions> configureOptions,
        Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);
        return services.AddEasySoapClientCore(serviceKey: null, builder => builder.Configure(configureOptions), configureHttpClientBuilder);
    }

    /// <summary>
    /// Registers a non-keyed <see cref="IEasySoapService"/> bound to a configuration section
    /// (e.g. <c>builder.Configuration.GetSection("Navision")</c>).
    /// </summary>
    public static IServiceCollection AddEasySoapClient(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddEasySoapClientCore(serviceKey: null, builder => builder.Bind(configuration), configureHttpClientBuilder);
    }

    /// <summary>
    /// Registers a keyed <see cref="IEasySoapService"/>, resolved with <c>[FromKeyedServices(key)]</c>.
    /// Use one key per NAV instance or company.
    /// </summary>
    public static IServiceCollection AddKeyedEasySoapClient(
        this IServiceCollection services,
        string key,
        Action<EasySoapClientOptions> configureOptions,
        Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(configureOptions);
        return services.AddEasySoapClientCore(key, builder => builder.Configure(configureOptions), configureHttpClientBuilder);
    }

    /// <summary>
    /// Registers a keyed <see cref="IEasySoapService"/> bound to a configuration section.
    /// </summary>
    public static IServiceCollection AddKeyedEasySoapClient(
        this IServiceCollection services,
        string key,
        IConfiguration configuration,
        Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddEasySoapClientCore(key, builder => builder.Bind(configuration), configureHttpClientBuilder);
    }

    private static IServiceCollection AddEasySoapClientCore(
        this IServiceCollection services,
        string? serviceKey,
        Action<OptionsBuilder<EasySoapClientOptions>> configureOptions,
        Action<IHttpClientBuilder>? configureHttpClientBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);

        string optionsName = serviceKey ?? Options.Options.DefaultName;

        // Never the default (unnamed) client: that one is shared with the rest of the application.
        string httpClientName = serviceKey is null ? HttpClientNamePrefix : $"{HttpClientNamePrefix}:{serviceKey}";

        OptionsBuilder<EasySoapClientOptions> optionsBuilder = services.AddOptions<EasySoapClientOptions>(optionsName);
        configureOptions(optionsBuilder);
        optionsBuilder.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EasySoapClientOptions>, EasySoapClientOptionsValidator>());

        // Stateless, shared by all clients.
        services.TryAddSingleton<IXmlSanitizerService, XmlSanitizerService>();
        services.TryAddSingleton<ISoapEnvelopeService, SoapEnvelopeService>();
        services.TryAddSingleton<IParsingService, ParsingService>();

        IHttpClientBuilder httpClientBuilder = services.AddHttpClient(httpClientName, (provider, client) =>
        {
            EasySoapClientOptions options = provider.GetRequiredService<IOptionsMonitor<EasySoapClientOptions>>().Get(optionsName);
            client.BaseAddress = CreateBaseAddress(options.BaseUri);
        });

        configureHttpClientBuilder?.Invoke(httpClientBuilder);

        // Registered after the user's configureHttpClientBuilder (and app-wide defaults) so it applies to the primary
        // handler they configured; the handler itself is never replaced.
        httpClientBuilder.ConfigurePrimaryHttpMessageHandler((handler, provider) =>
        {
            EasySoapClientOptions options = provider.GetRequiredService<IOptionsMonitor<EasySoapClientOptions>>().Get(optionsName);
            ConfigureWindowsAuthentication(handler, options, httpClientName);
        });

        if (serviceKey is null)
        {
            services.TryAddTransient<ICredentialsProvider>(provider =>
                new CredentialsService(provider.GetRequiredService<IOptionsMonitor<EasySoapClientOptions>>(), optionsName));
            services.TryAddTransient<IEasySoapService>(provider => CreateService(provider, serviceKey, httpClientName, optionsName));
        }
        else
        {
            services.TryAddKeyedTransient<ICredentialsProvider>(serviceKey, (provider, _) =>
                new CredentialsService(provider.GetRequiredService<IOptionsMonitor<EasySoapClientOptions>>(), optionsName));
            services.TryAddKeyedTransient<IEasySoapService>(serviceKey, (provider, _) => CreateService(provider, serviceKey, httpClientName, optionsName));
        }

        return services;
    }

    private static EasySoapService CreateService(IServiceProvider provider, string? serviceKey, string httpClientName, string optionsName)
    {
        EasySoapClientOptions options = provider.GetRequiredService<IOptionsMonitor<EasySoapClientOptions>>().Get(optionsName);
        HttpClient httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName);

        var requestSender = new RequestSenderService(
            provider.GetRequiredService<ILogger<RequestSenderService>>(),
            httpClient,
            GetBaseQuery(options.BaseUri),
            new SoapAuthenticator(provider, provider.GetRequiredService<IOptionsMonitor<EasySoapClientOptions>>(), serviceKey, optionsName),
            options.IncludeEnvelopeInExceptions);

        return new EasySoapService(
            provider.GetRequiredService<ISoapEnvelopeService>(),
            provider.GetRequiredService<IParsingService>(),
            requestSender);
    }

    /// <summary>
    /// Relative URLs ("Page/Customer") replace the last segment of a base address without a trailing slash,
    /// which would silently drop the company, and drop the base address' query (e.g. <c>?tenant=t1</c>).
    /// So the path always ends with a slash, and the query is kept apart and appended to every request.
    /// </summary>
    internal static Uri CreateBaseAddress(string baseUri)
    {
        var uri = new Uri(baseUri, UriKind.Absolute);
        string path = uri.GetLeftPart(UriPartial.Path);
        return new Uri(path.EndsWith('/') ? path : path + "/", UriKind.Absolute);
    }

    /// <summary>The query of the base address including '?', or an empty string.</summary>
    internal static string GetBaseQuery(string baseUri)
        => new Uri(baseUri, UriKind.Absolute).Query;

    private static void ConfigureWindowsAuthentication(HttpMessageHandler handler, EasySoapClientOptions options, string httpClientName)
    {
        if (options.AuthenticationMode != SoapAuthenticationMode.Windows)
        {
            return;
        }

        ICredentials credentials = String.IsNullOrEmpty(options.Username)
            ? CredentialCache.DefaultCredentials
            : new NetworkCredential(options.Username, options.Password, options.Domain);

        switch (handler)
        {
            case SocketsHttpHandler socketsHandler:
                if (EnsureCredentialsCanBeSet(socketsHandler.Credentials, credentials, httpClientName))
                {
                    socketsHandler.Credentials = credentials;
                    socketsHandler.PreAuthenticate = true;
                }

                break;

            case HttpClientHandler clientHandler:
                if (EnsureCredentialsCanBeSet(clientHandler.Credentials, credentials, httpClientName))
                {
                    clientHandler.UseDefaultCredentials = false;
                    clientHandler.Credentials = credentials;
                    clientHandler.PreAuthenticate = true;
                }

                break;

            default:
                throw new InvalidOperationException(
                    $"{httpClientName}: Windows authentication needs a SocketsHttpHandler or HttpClientHandler as primary handler, " +
                    $"but the configured primary handler is {handler.GetType().Name}. Set its credentials yourself and use AuthenticationMode.None.");
        }
    }

    /// <summary>
    /// Returns true if the credentials should be set, false if the handler already has exactly these credentials.
    /// A handler that already has other credentials is shared with another client (or configured by the user):
    /// overwriting them would make one client authenticate as another, so that is refused.
    /// </summary>
    private static bool EnsureCredentialsCanBeSet(ICredentials? existing, ICredentials wanted, string httpClientName)
    {
        if (existing is null)
        {
            return true;
        }

        bool same = (existing, wanted) switch
        {
            (NetworkCredential a, NetworkCredential b) => a.UserName == b.UserName && a.Password == b.Password && a.Domain == b.Domain,
            _ => ReferenceEquals(existing, wanted),
        };

        return same
            ? false
            : throw new InvalidOperationException(
                $"{httpClientName}: the primary handler already has other credentials (set on it directly, or by another client " +
                "sharing the handler). With Windows authentication the library sets the credentials, and every client needs its " +
                "own primary handler instance.");
    }
}
