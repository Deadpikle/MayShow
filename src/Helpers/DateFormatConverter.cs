
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
        return 0.0;
    }
}