// <copyright file="IntegerCompareConverter.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Utilities
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    /// <summary>
    /// Compare the ints and return a bool. Used for determining whether a radio box should be checked.
    /// </summary>
    public class IntegerCompareConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int intValue = (int)value;
            int intParameter = int.Parse((string)parameter);

            return intValue == intParameter;
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(bool)value)
            {
                return DependencyProperty.UnsetValue;
            }

            return parameter;
        }
    }
}
