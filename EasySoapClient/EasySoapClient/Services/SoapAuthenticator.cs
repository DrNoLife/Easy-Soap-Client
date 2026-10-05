using System.Net.Http.Headers;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EasySoapClient.Services;

/// <summary>
/// Creates the Authorization header for each request from the current options.
/// Created together with the <see cref="IEasySoapService"/> instance, in the caller's DI scope, so scoped
/// and transient credential / token providers get the lifetime the caller expects.
/// </summary>
internal sealed class SoapAuthenticator(
    IServiceProvider services,
    IOptionsMonitor<EasySoapClientOptions> optionsMonitor,
    string? serviceKey,
    string optionsName)
{
    private readonly IServiceProvider _services = services;
    private readonly IOptionsMonitor<EasySoapClientOptions> _optionsMonitor = optionsMonitor;
    private readonly string? _serviceKey = serviceKey;
    private readonly string _optionsName = optionsName;

    private ICredentialsProvider? _credentialsProvider;
    private IAccessTokenProvider? _tokenProvider;

    public async ValueTask<AuthenticationHeaderValue?> CreateHeaderAsync(CancellationToken cancellationToken)
    {
        EasySoapClientOptions options = _optionsMonitor.Get(_optionsName);

        switch (options.AuthenticationMode)
        {
            case SoapAuthenticationMode.Basic:
                _credentialsProvider ??= Resolve<ICredentialsProvider>();
                return new AuthenticationHeaderValue("Basic", _credentialsProvider.GenerateBase64Credentials());

            case SoapAuthenticationMode.OAuth:
                _tokenProvider ??= Resolve<IAccessTokenProvider>();
                string token = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
                return new AuthenticationHeaderValue("Bearer", token);

            default:
                return null;
        }
    }

    // Keyed clients only use keyed providers: falling back to a non-keyed one could send one
    // tenant's credentials to another tenant's server.
    private T Resolve<T>() where T : class
        => (_serviceKey is null ? _services.GetService<T>() : _services.GetKeyedService<T>(_serviceKey))
        ?? throw new InvalidOperationException(_serviceKey is null
            ? $"EasySoapClient: no {typeof(T).Name} is registered."
            : $"EasySoapClient '{_serviceKey}': no {typeof(T).Name} is registered with the key '{_serviceKey}'.");
}
