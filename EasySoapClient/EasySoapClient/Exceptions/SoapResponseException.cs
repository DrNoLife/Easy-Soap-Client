namespace EasySoapClient.Exceptions;

/// <summary>
/// Thrown when a successful response does not have the expected shape,
/// e.g. a <c>Create</c> response without the created record.
/// </summary>
public class SoapResponseException : Exception
{
    /// <summary>Creates a new exception.</summary>
    public SoapResponseException(string message, Exception? innerException = null)
        : base(message, innerException)
    { }
}
