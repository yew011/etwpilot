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
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using etwlib;

namespace EtwPilot.Utilities.Converters
{
    using StopCondition = ViewModel.LiveSessionViewModel.StopCondition;
    using ChatTopic = ViewModel.InsightsViewModel.ChatTopic;

    public class HasErrorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
            {
                Debug.Assert(false);
                return false;
            }
            var vm = values[0] as NotifyPropertyAndErrorInfoBase;
            var fullPropertyPath = values[1] as string;
            if (vm == null || string.IsNullOrEmpty(fullPropertyPath))
            {
                Debug.Assert(false);
                return false;
            }
            var hasErrors = vm.PropertyHasErrors(fullPropertyPath);
            return hasErrors;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ErrorMessageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
            {
                Debug.Assert(false);
                return string.Empty;
            }
            var vm = values[0] as NotifyPropertyAndErrorInfoBase;
            var fullPropertyPath = values[1] as string;
            if (vm == null || string.IsNullOrEmpty(fullPropertyPath))
            {
                Debug.Assert(false);
                return string.Empty;
            }
            if (!vm.PropertyHasErrors(fullPropertyPath))
            {
                Debug.Assert(false);
                return string.Empty;
            }
            var errors = vm.GetErrors(fullPropertyPath).Cast<string>().ToList();
            var truncated = $"{errors[0]}";
            if (errors.Count > 1)
            {
                truncated += $" (+{errors.Count - 1} more)";
            }
            return truncated;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NotNullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return Binding.DoNothing;
            }
            return null!;
        }
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
            var evt = value as ParsedEtwEvent;
            var target = value;
            if (evt != null)
            {
                //
                // The column name from the DataGrid corresponds to a field name in the 'value' object.
                // We have to use reflection to get it.
                //
                var fieldName = parameter as string;
                if (fieldName == null)
                {
                    Debug.Assert(false);
                    return "";
                }
                target = ReflectionHelper.GetPropertyValue(evt, fieldName);
            }
            var val = System.Convert.ToInt64(target);
            return $"0x{val:X}";
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

    public class StringNullOrEmptyToVisibilityConverter : System.Windows.Markup.MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string)
                ? Visibility.Hidden : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return "";
        }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
