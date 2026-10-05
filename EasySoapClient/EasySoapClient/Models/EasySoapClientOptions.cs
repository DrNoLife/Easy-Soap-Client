namespace EasySoapClient.Models;

/// <summary>
/// Configuration for an EasySoapClient instance.
/// </summary>
public sealed class EasySoapClientOptions
{
    /// <summary>
    /// The web service base address including the company, e.g. <c>https://server:7047/BC/WS/CRONUS%20Danmark/</c>.
    /// A trailing slash is added automatically.
    /// </summary>
    public string BaseUri { get; set; } = String.Empty;

    /// <summary>User name for <see cref="SoapAuthenticationMode.Basic"/> or <see cref="SoapAuthenticationMode.Windows"/>.</summary>
    public string Username { get; set; } = String.Empty;

    /// <summary>Password for <see cref="SoapAuthenticationMode.Basic"/> or <see cref="SoapAuthenticationMode.Windows"/>.</summary>
    public string Password { get; set; } = String.Empty;

    /// <summary>Optional domain for <see cref="SoapAuthenticationMode.Windows"/>.</summary>
    public string? Domain { get; set; }

    /// <summary>How requests are authenticated. Defaults to <see cref="SoapAuthenticationMode.Basic"/>.</summary>
    public SoapAuthenticationMode AuthenticationMode { get; set; } = SoapAuthenticationMode.Basic;

    /// <summary>
    /// Whether <see cref="Exceptions.SoapRequestException.SoapEnvelope"/> is populated with the request that failed.
    /// Off by default, as envelopes can contain business data that ends up in logs.
    /// </summary>
    public bool IncludeEnvelopeInExceptions { get; set; }
}

/// <summary>
/// How requests are authenticated.
/// </summary>
public enum SoapAuthenticationMode
{
    /// <summary>HTTP Basic with <see cref="EasySoapClientOptions.Username"/> and <see cref="EasySoapClientOptions.Password"/> (UTF-8).</summary>
    Basic,

    /// <summary>
    /// Windows authentication (NTLM / Negotiate). Uses <see cref="EasySoapClientOptions.Username"/>,
    /// <see cref="EasySoapClientOptions.Password"/> and <see cref="EasySoapClientOptions.Domain"/>,
    /// or the process' default credentials when no user name is set.
    /// </summary>
    Windows,

    /// <summary>OAuth bearer tokens from a registered <see cref="Interfaces.IAccessTokenProvider"/>.</summary>
    OAuth,

    /// <summary>No authentication added by the library (e.g. when configured through the HttpClient builder).</summary>
    None,
}
