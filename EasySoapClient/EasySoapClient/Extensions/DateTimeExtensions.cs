namespace EasySoapClient.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    /// Converts a DateTime to the Navision/ISO 8601 format yyyy-MM-ddTHH:mm:ss.
    /// </summary>
    public static string ToNavisionString(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
    }
}
