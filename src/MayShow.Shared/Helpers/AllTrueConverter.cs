using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MayShow.Helpers;

// from: https://docs.avaloniaui.net/docs/data-binding/multi-binding
public class AllTrueConverter : IMultiValueConverter
{
    public object? Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        foreach (var value in values)
        {
            if (value is not true)
                return false;
        }
        return true;
    }
}