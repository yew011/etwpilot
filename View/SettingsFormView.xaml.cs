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
using EtwPilot.Model;
using EtwPilot.Utilities;
using EtwPilot.ViewModel;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;

namespace EtwPilot.View
{
    using UserControl = System.Windows.Controls.UserControl;

    public partial class SettingsFormView : UserControl
    {
        public SettingsFormView()
        {
            InitializeComponent();
        }

        private void BrowseSymbolPathButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFolderDialog();
            browser.Title = "Select a location";
            browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            SymbolPathTextBox.Text = browser.FolderName;
        }

        private void BrowseDbgHelpDllPathButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFileDialog();
            browser.Title = "Select a dbghelp.dll file";
            browser.Filter = "DLL files (*.dll)|*.dll";
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            DbgHelpDllPathTextBox.Text = browser.FileName;
        }

        private void BrowseProviderCachePathButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFolderDialog();
            browser.Title = "Select a location";
            browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            ProviderCachePathTextBox.Text = browser.FolderName;
        }

        private void BrowseModelPathButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFolderDialog();
            browser.Title = "Select a location";
            browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            var vm = UiHelper.GetViewModelFromFrameworkElement<
                SettingsFormViewModel>(sender as FrameworkElement);
            if (vm == null)
            {
                return;
            }
            ModelPathTextbox.Text = browser.FolderName;
            vm.TryCreateModelConfig();
        }

        private void BrowseEmbeddingsModelFileButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFileDialog();
            browser.Title = "Select a text embeddings model file";
            browser.Filter = "ONNX model files (*.onnx)|*.onnx";
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            var vm = UiHelper.GetViewModelFromFrameworkElement<
                SettingsFormViewModel>(sender as FrameworkElement);
            if (vm == null)
            {
                return;
            }
            EmbeddingsModelPathTextbox.Text = browser.FileName;
            vm.TryCreateModelConfig();
        }

        private void UpdateFormatterButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettingsFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            var selectedFormatter = FormattersListBox.SelectedItem as Formatter;
            if (selectedFormatter == null)
            {
                Debug.Assert(false);
                return;
            }

            //
            // Manually build the formatter object from the form fields. If it validates,
            // the source binding is already updated by the command.
            //
            var newFormatter = new Formatter()
            {
                Id = selectedFormatter.Id,
                ClassName = selectedFormatter.ClassName,
                Namespace = selectedFormatter.Namespace,
                Inheritence = selectedFormatter.Inheritence,
                FunctionName = FunctionName.Text,
                Body = FunctionBody.Text
            };

            if (vm.UpdateFormatterCommand.CanExecute(newFormatter))
            {
                vm.UpdateFormatterCommand.Execute(newFormatter);
            }
        }

        private void SettingsFormViewControl_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettingsFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            vm.InitializeFromCodeBehind();
        }
    }
}
