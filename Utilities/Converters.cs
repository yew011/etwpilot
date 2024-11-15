/* 
Licensed to the Apache Software Foundation (ASF) under one
or more contributor license agreements.  See the NOTICE file
distributed with this work for additional information
regarding copyright ownership.  The ASF licenses this file
to you under the Apache License, Version 2.0 (the
"License"); you may not use this file except in compliance
with the License.  You may obtain a copy of the License at

  http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing,
software distributed under the License is distributed on an
"AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
KIND, either express or implied.  See the License for the
specific language governing permissions and limitations
under the License.
*/
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using etwlib;
using EtwPilot.ViewModel;

namespace EtwPilot.Utilities.Converters
{
    using StopCondition = ViewModel.LiveSessionViewModel.StopCondition;
    using ChatTopic = ViewModel.InsightsViewModel.ChatTopic;

    public static class IConverterCode
    {
        //
        // Allows users to customize IConverter code for default ETW columns
        //
        public static readonly string s_DecimalToHexIConverterCode = @"
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var val = System.Convert.ToInt64(value);
            return $""0x{val:X}"";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }";
    }

    public class IsGreaterThanZero :IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((int)value <= 0)
            {
                return Visibility.Hidden;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DecimalToHex : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var val = System.Convert.ToInt64(value);
            return $"0x{val:X}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MillisecondToSecond : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            decimal d = (long)value;
            var elapsedSec = (int)Math.Floor(d / 1000);
            return $"{elapsedSec}s";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ByteSizeToFriendlyString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return FriendlyByteSize.Format(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StopConditionToBool : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || parameter == null)
            {
                return false;
            }
            var str = parameter as string;
            if (str == null)
            {
                return StopCondition.None;
            }
            if (!Enum.TryParse(typeof(StopCondition), str, true, out var parsedValue))
            {
                return StopCondition.None;
            }
            var val = (StopCondition)value;
            return val.Equals(parsedValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var val = (bool)value;
            if (!val)
            {
                return Binding.DoNothing;
            }
            var str = parameter as string;
            if (str == null)
            {
                return StopCondition.None;
            }
            if (!Enum.TryParse(typeof(StopCondition), str, true, out var parsedValue))
            {
                return StopCondition.None;
            }
            return parsedValue;
        }
    }

    public class ChatTopicToBool : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || parameter == null)
            {
                return false;
            }
            var str = parameter as string;
            if (str == null)
            {
                return ChatTopic.Invalid;
            }
            if (!Enum.TryParse(typeof(ChatTopic), str, true, out var parsedValue))
            {
                return ChatTopic.Invalid;
            }
            var val = (ChatTopic)value;
            return val.Equals(parsedValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var val = (bool)value;
            if (!val)
            {
                return Binding.DoNothing;
            }
            var str = parameter as string;
            if (str == null)
            {
                return ChatTopic.Invalid;
            }
            if (!Enum.TryParse(typeof(ChatTopic), str, true, out var parsedValue))
            {
                return ChatTopic.Invalid;
            }
            return parsedValue;
        }
    }

    public class ParsedEtwManifestEventToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var evt = value as ParsedEtwManifestEvent;
            if (evt == null)
            {
                return Binding.DoNothing;
            }
            return evt.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var str = value as string;
            if (string.IsNullOrEmpty(str))
            {
                return Binding.DoNothing;
            }
            var obj = ParsedEtwManifestEvent.FromString(str);
            if (obj == null)
            {
                return Binding.DoNothing;
            }
            return obj;
        }
    }

    public class IntegerToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return ((int)value).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var str = value as string;
            if (string.IsNullOrEmpty(str))
            {
                return 0;
            }
            if (!int.TryParse(str, out int result))
            {
                return 0;
            }
            return value;
        }
    }

    public class NullableBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var test = (bool?)value;
            var result = bool.Parse((string)parameter);

            if (test == result)
            {
                return true;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var result = bool.Parse((string)parameter);
            return result;
        }
    }

    public class DefaultConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
            {
                return Binding.DoNothing;
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MultiValueBooleanConverterOR : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            foreach (object value in values)
            {
                if ((value is bool) || (bool)value == true)
                {
                    return true;
                }
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        //
        // I hate that you make me do this, WPF. I hate you. You're awful.
        //
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    internal static class FriendlyByteSize
    {
        static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB" };

        public static string Format(dynamic Value, int DecimalPlaces = 2)
        {
            if (!decimal.TryParse(Value.ToString(), out decimal value))
            {
                return "NaN!";
            }

            if (value > int.MaxValue)
            {
                return "NaN!";
            }
            else if (value < 0)
            {
                return "-" + Format(-value, DecimalPlaces);
            }

            int i = 0;
            while (Math.Round(value, DecimalPlaces) >= 1000)
            {
                value /= 1024;
                i++;
            }

            return string.Format("{0:n" + DecimalPlaces + "} {1}", value, SizeSuffixes[i]);
        }
    }
}
