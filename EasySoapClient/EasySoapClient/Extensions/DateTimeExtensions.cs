using System.Globalization;

namespace EasySoapClient.Extensions;

/// <summary>
/// Date helpers.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Converts a DateTime to the Navision/ISO 8601 format yyyy-MM-ddTHH:mm:ss, independent of the current culture.
    /// </summary>
    public static string ToNavisionString(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
    }
}
