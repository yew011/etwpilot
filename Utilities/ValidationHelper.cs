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
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace EtwPilot.Utilities.ValidationHelper
{
    //
    // This BindingHelper is used by CustomErrorTemplate defined in App.xaml during WPF form validation.
    // The template utilizes BindingHelper to extract the full path of the bound property on a control
    // that is participating in validation (eg: OnnxGenAIConfigModel.ModelPath as opposed to just
    // ModelPath). The full path is needed because some viewmodels have properties that are themselves
    // classes with their own error tracking, and the full path provides a reliable way to disambiguate
    // if an error occurred on a property in the viewmodel or a property in the sub-class.
    //
    public static class BindingHelper
    {
        public static readonly DependencyProperty BindingPathProperty =
            DependencyProperty.RegisterAttached("BindingPath", typeof(string), typeof(BindingHelper));
        public static string GetBindingPath(DependencyObject obj) => (string)obj.GetValue(BindingPathProperty);
        public static void SetBindingPath(DependencyObject obj, string value) => obj.SetValue(BindingPathProperty, value);
    }

    public class BindingPathBehavior : Behavior<AdornedElementPlaceholder>
    {
        private static readonly Dictionary<Type, DependencyProperty> PrimaryProperties = new()
    {
        { typeof(TextBox), TextBox.TextProperty },
        { typeof(CheckBox), CheckBox.IsCheckedProperty },
        { typeof(ListBox), ListBox.SelectedItemProperty },
        { typeof(ComboBox), ComboBox.SelectedItemProperty },
        { typeof(Slider), Slider.ValueProperty }
    };

        protected override void OnAttached()
        {
            base.OnAttached();
            UpdateBindingPath();
        }

        private void UpdateBindingPath()
        {
            if (AssociatedObject.AdornedElement is FrameworkElement element)
            {
                var controlType = element.GetType();
                var primaryProperty = PrimaryProperties.FirstOrDefault(kvp => kvp.Key.IsAssignableFrom(controlType)).Value;
                if (primaryProperty != null)
                {
                    var binding = BindingOperations.GetBindingExpression(element, primaryProperty);
                    if (binding != null)
                    {
                        BindingHelper.SetBindingPath(element, binding.ParentBinding.Path.Path);
                    }
                }
            }
        }
    }
}
