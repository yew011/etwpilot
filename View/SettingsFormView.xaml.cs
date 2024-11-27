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
using System.Windows;
using System.Windows.Forms;

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
            var browser = new FolderBrowserDialog();
            browser.Description = "Select a location";
            browser.RootFolder = Environment.SpecialFolder.MyComputer;
            var result = browser.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            SymbolPathTextBox.Text = browser.SelectedPath;
        }

        private void BrowseDbgHelpDllPathButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFileDialog();
            browser.Title = "Select a dbghelp.dll file";
            browser.Filter = "DLL files (*.dll)|*.dll";
            var result = browser.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            DbgHelpDllPathTextBox.Text = browser.FileName;
        }

        private void BrowseProviderCachePathButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new FolderBrowserDialog();
            browser.Description = "Select a location";
            browser.RootFolder = Environment.SpecialFolder.MyComputer;
            var result = browser.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            ProviderCachePathTextBox.Text = browser.SelectedPath;
        }

        private void BrowseModelPathButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new FolderBrowserDialog();
            browser.Description = "Select a location";
            browser.RootFolder = Environment.SpecialFolder.MyComputer;
            var result = browser.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            var vm = UiHelper.GetViewModelFromFrameworkElement<
                SettingsFormViewModel>(sender as FrameworkElement);
            if (vm == null)
            {
                return;
            }
            ModelPathTextbox.Text = browser.SelectedPath;
            vm.TryCreateModelConfig();
        }

        private void BrowseEmbeddingsModelFileButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFileDialog();
            browser.Title = "Select a text embeddings model file";
            browser.Filter = "ONNX model files (*.onnx)|*.onnx";
            var result = browser.ShowDialog();
            if (result != DialogResult.OK)
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
    }
}
