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
using EtwPilot.Utilities;
using EtwPilot.ViewModel;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;
using System.IO;
using EtwPilot.Model;

namespace EtwPilot.View
{
    using UserControl = System.Windows.Controls.UserControl;

    public partial class SettingsFormView : UserControl
    {
        public SettingsFormView()
        {
            InitializeComponent();
        }

        #region Debug tab

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
        #endregion

        #region Insights - Onnx

        private void BrowseOnnxModelPathButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFolderDialog();
            browser.Title = "Select the model path";
            browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            OnnxModelPathTextbox.Text = browser.FolderName;
        }

        private void BrowseOnnxRuntimeConfigFileButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFileDialog();
            browser.Title = "Select a JSON config file";
            browser.Filter = "JSON files (*.json)|*.json";
            if (!string.IsNullOrEmpty(OnnxModelPathTextbox.Text))
            {
                browser.RootDirectory = OnnxModelPathTextbox.Text;
            }
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            OnnxRuntimeConfigFileTextbox.Text = browser.FileName;
        }

        private void BrowseBertModelPathButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFolderDialog();
            browser.Title = "Select a location";
            browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            BertModelPathTextbox.Text = browser.FolderName;
        }

        private async void runtimeOnnxRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettingsFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            vm.OllamaConfig = null;
            vm.OnnxGenAIConfig = new Model.OnnxGenAIConfigModel();
            await vm.Validate();
        }

        #endregion

        #region Insights - Ollama

        private async void OllamaDownloadNewModelButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettingsFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            var ollama = vm.OllamaConfig;
            if (ollama == null)
            {
                Debug.Assert(false);
                return;
            }
            if (!await ollama.DownloadNewModel(OllamaDownloadNewModelTextbox.Text))
            {
                return;
            }
        }

        private void OllamaCancelDownloadNewModelButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettingsFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            var ollama = vm.OllamaConfig;
            if (ollama == null)
            {
                Debug.Assert(false);
                return;
            }
            ollama.CancelNewModelDownload();
        }

        private void BrowseOllamaRuntimeConfigFileButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettingsFormViewModel;
            if (vm == null || vm.OllamaConfig == null)
            {
                Debug.Assert(false);
                return;
            }

            var modelFolder = vm.OllamaConfig.GetModelFileLocation();
            var browser = new OpenFileDialog();
            browser.Title = "Select a config file";
            browser.Filter = "JSON files (*.json)|*.json";
            if (!string.IsNullOrEmpty(modelFolder) && Directory.Exists(modelFolder))
            {
                browser.InitialDirectory = modelFolder;
            }
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            OllamaRuntimeConfigFileTextbox.Text = browser.FileName;
        }

        private void CreateOllamaRuntimeConfigFileButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettingsFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            var ollama = vm.OllamaConfig;
            if (ollama == null)
            {
                Debug.Assert(false);
                return;
            }
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Choose a location to save model runtime config file",
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = "json",
            };
            var modelFolder = OllamaRuntimeConfigFileTextbox.Tag as string;
            if (!string.IsNullOrEmpty(modelFolder) && Directory.Exists(modelFolder))
            {
                saveFileDialog.InitialDirectory = modelFolder;
            }
            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                File.WriteAllText(filePath, OllamaConfigModel.s_DefaultRuntimeConfigJson);
                ollama.RuntimeConfigFile = filePath;
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
        }

        private void runtimeOllamaRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettingsFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            vm.OnnxGenAIConfig = null;
            if (vm.OllamaConfig == null)
            {
                vm.OllamaConfig = new Model.OllamaConfigModel();
            }
        }

        #endregion

        #region Formatters

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

        #endregion

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

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

    }
}
