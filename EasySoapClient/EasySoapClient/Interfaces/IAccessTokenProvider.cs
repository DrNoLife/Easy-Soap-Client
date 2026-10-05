namespace EasySoapClient.Interfaces;

/// <summary>
/// Supplies OAuth bearer tokens when <see cref="Models.EasySoapClientOptions.AuthenticationMode"/> is
/// <see cref="Models.SoapAuthenticationMode.OAuth"/> (required by Business Central online).
/// Register an implementation in DI; keyed clients only use an implementation registered with their key.
/// It is resolved in the caller's scope together with each <see cref="IEasySoapService"/> instance and called for
/// every request, so cache tokens in a singleton (or in a service the implementation depends on).
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>Returns a valid access token (without the <c>Bearer</c> prefix).</summary>
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
