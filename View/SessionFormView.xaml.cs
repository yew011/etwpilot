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
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace EtwPilot.View
{
    using UserControl = System.Windows.Controls.UserControl;
    using TabControl = System.Windows.Controls.TabControl;
    using static EtwPilot.Utilities.TraceLogger;

    public partial class SessionFormView : UserControl
    {
        public SessionFormView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //
            // Pre-load a provider form for each selected provider, if any
            //
            var vm = DataContext as MainWindowViewModel;
            if (vm == null)
            {
                return;
            }

            if (vm.m_SessionFormViewModel.InitialProviders != null &&
                vm.m_SessionFormViewModel.InitialProviders.Count > 0)
            {
                foreach (var provider in vm.m_SessionFormViewModel.InitialProviders)
                {
                    await AddProvider(provider);
                }
                vm.m_SessionFormViewModel.InitialProviders.Clear();
            }
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
            var provider = SelectedProvider.SelectedItem as ParsedEtwProvider;
            if (provider == null)
            {
                return;
            }
            await AddProvider(provider);
        }

        private async Task AddProvider(ParsedEtwProvider Provider)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null)
            {
                return;
            }

            if (vm.m_SessionFormViewModel.ActiveProcessList.Count == 0)
            {
                //
                // Lazy initialize on first "Add" button click
                //
                vm.StateManager.ProgressState.UpdateProgressMessage(
                    "Loading process list, please wait...");
                await vm.m_SessionFormViewModel.RefreshProcessList();
                vm.StateManager.ProgressState.UpdateProgressMessage(
                    $"Found {vm.m_SessionFormViewModel.ActiveProcessList.Count} processes.");
            }

            //
            // If the provider has never been loaded, parse and load its manifest.
            // If it has been accessed before, pull the VM from the cache. Each entry in
            // the cache points to a ProviderFilterFormViewModel.
            //
            var tabName = UiHelper.GetUniqueTabName(Provider.Id, "ProviderFilter");
            var newSessionFormVm = vm.m_SessionFormViewModel;
            var providerFilterVm = await newSessionFormVm.LoadProviderFilterForm(
                tabName, Provider.Id);
            if (providerFilterVm == null)
            {
                return;
            }

            //
            // If the tab already exists for this manifest, just select the tab, otherwise
            // we have to create the tab now.
            //
            Func<Task<bool>> tabClosedCallback = async delegate()
            {
                var vm = DataContext as MainWindowViewModel;
                if (vm == null)
                {
                    return false;
                }

                vm.m_SessionFormViewModel.RemoveProviderFilterForm(tabName);
                return true;
            };

            if (!UiHelper.CreateTabControlContextualTab(
                    ProviderFiltersTabControl,
                    providerFilterVm,
                    tabName,
                    Provider.Name!,
                    "ProviderFilterContextTabStyle",
                    "ProviderFilterContextTabText",
                    "ProviderFilterContextTabCloseButton",
                    null,
                    tabClosedCallback))
            {
                Trace(TraceLoggerType.MainWindow,
                      TraceEventType.Error,
                      $"Unable to create contextual tab {tabName}");
                return;
            }
        }

        private async void ProviderFiltersTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tabcontrol = sender as TabControl;
            if (tabcontrol == null)
            {
                return;
            }
            var tab = tabcontrol.SelectedValue as TabItem;
            if (tab == null)
            {
                return;
            }
            var vm = DataContext as MainWindowViewModel;
            if (vm == null)
            {
                return;
            }
            var tabName = tab.Name;
            var newSessionFormVm = vm.m_SessionFormViewModel;
            //
            // We don't need to bother parsing the GUID out of the tabName - it's not
            // needed because the VM should exist
            //
            var providerFilterVm = await newSessionFormVm.LoadProviderFilterForm(
                tabName, Guid.Empty);
            if (providerFilterVm == null)
            {
                return;
            }
            newSessionFormVm.CurrentProviderFilterForm = providerFilterVm;
        }

    }
}
