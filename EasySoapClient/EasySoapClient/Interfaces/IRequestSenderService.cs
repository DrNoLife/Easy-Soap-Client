namespace EasySoapClient.Interfaces;

internal interface IRequestSenderService
{
    /// <summary>
    /// Posts the envelope and hands the (buffered, seekable) response body to <paramref name="parse"/>
    /// before the response is disposed.
    /// </summary>
    Task<TResult> SendAsync<TResult>(
        string relativeUrl,
        string soapAction,
        string soapEnvelope,
        Func<Stream, TResult> parse,
        CancellationToken cancellationToken);
}
