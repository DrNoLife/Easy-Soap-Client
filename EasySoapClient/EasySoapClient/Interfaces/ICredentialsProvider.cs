namespace EasySoapClient.Interfaces;

/// <summary>
/// Supplies Basic authentication credentials. Resolved in the caller's scope together with each
/// <see cref="IEasySoapService"/> instance and called for every request, so an implementation can rotate credentials.
/// Register your own implementation (keyed with the client key, for keyed clients) to replace the default,
/// which reads <see cref="Models.EasySoapClientOptions.Username"/> and <see cref="Models.EasySoapClientOptions.Password"/>.
/// </summary>
public interface ICredentialsProvider
{
    /// <summary>Returns <c>base64(username:password)</c> using UTF-8.</summary>
    string GenerateBase64Credentials();
}
