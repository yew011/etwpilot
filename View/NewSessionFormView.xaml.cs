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
using etwlib;
using EtwPilot.Utilities;
using EtwPilot.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
namespace EtwPilot.View
{
    using UserControl = System.Windows.Controls.UserControl;

    public partial class NewSessionFormView : UserControl
    {
        public NewSessionFormView()
        {
            InitializeComponent();
        }

        private void BrowseLogLocationButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new FolderBrowserDialog();
            browser.Description = "Select a location";
            browser.RootFolder = Environment.SpecialFolder.MyComputer;
            var result = browser.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            SaveTraceLogLocationTextbox.Text = browser.SelectedPath;
        }

        private async void AddProviderFilterButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null)
            {
                return;
            }

            var provider = SelectedProvider.SelectedItem as ParsedEtwProvider;
            if (provider == null)
            {
                return;
            }

            //
            // If the provider has never been loaded, parse and load its manifest.
            // If it has been accessed before, pull the VM from the cache. Each entry in
            // the cache points to a NewProviderFilterFormViewModel.
            //
            var newSessionFormVm = vm.m_NewSessionFormViewModel;
            var providerFilterVm = await newSessionFormVm.LoadProviderFilterForm(provider.Id);
            if (providerFilterVm == null)
            {
                return;
            }

            //
            // If the tab already exists for this manifest, just select the tab, otherwise
            // we have to create the tab now.
            //
            var existingTabs = ProviderFiltersTabControl.Items.Cast<TabItem>().ToList();
            var tabName = UiHelper.GetUniqueTabName(provider.Id, "ProviderFilter");
            var tab = existingTabs.Where(tab => tab.Name == tabName).FirstOrDefault();
            if (tab == null)
            {
                if (!CreateProviderFiltersTab(tabName, provider, providerFilterVm.Manifest))
                {
                    return;
                }
            }
            else
            {
                tab.IsSelected = true;
            }
        }

        private bool CreateProviderFiltersTab(string TabName, ParsedEtwProvider Provider, ParsedEtwManifest Manifest)
        {
            var style = ProviderFiltersTabControl.FindResource("ProviderFilterContextTabStyle") as Style;
            if (style == null)
            {
                return false;
            }
            var newTab = new TabItem
            {
                Name = TabName,
                Style = style,
                IsSelected = true,
                Content = new NewProviderFilterFormViewModel(Manifest)
            };

            newTab.Loaded += (s, e) =>
            {
                UiHelper.FixupDynamicTab(ProviderFiltersTabControl,
                    newTab,
                    Provider.Name!,
                    "ProviderFilterContextTabText",
                    "ProviderFilterContextTabCloseButton",
                    null);
            };

            ProviderFiltersTabControl.Items.Add(newTab);
            return true;
        }
    }
}
