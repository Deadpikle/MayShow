
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MayShow.Helpers;

public class DateFormatConverter : IMultiValueConverter
{
    public object? Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is DateOnly date && values[1] is string format)
        {
            return date.ToString(format);
        }
        if (values.Count >= 2 && values[0] is string dateFormat && values[1] is DateOnly dateOnly)
        {
            return dateOnly.ToString(dateFormat);
        }
        if (values.Count >= 2 && values[0] is DateTime dateTime && values[1] is string format3)
        {
            return dateTime.ToString(format3);
        }
        if (values.Count >= 2 && values[0] is string format4 && values[1] is DateTime dateTime2)
        {
            return dateTime2.ToString(format4);
        }
        return "";
    }
}