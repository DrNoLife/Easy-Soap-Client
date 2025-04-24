using System.Reflection;

namespace EasySoapClient.Extensions;

public static class PropertyInfoExtensions
{
    /// <summary>
    /// If the property is DateTime or Nullable<DateTime> and the value is a DateTime,
    /// formats it to Navision string. Otherwise returns the original value.
    /// </summary>
    public static object? FormatNavisionValue(this PropertyInfo property, object value)
    {
        var propertyValue = property.GetValue(value);

        // check for DateTime or Nullable<DateTime>
        if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
        {
            if (propertyValue is DateTime dateTimeValue)
            {
                // use the DateTime extension
                return dateTimeValue.ToNavisionString();
            }
        }

        // not a DateTime (or value was null), return as-is
        return propertyValue;
    }
}
