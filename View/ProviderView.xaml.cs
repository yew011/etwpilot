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
using System.Windows.Input;

namespace EtwPilot.View
{
    public partial class ProviderView : UserControl
    {
        public ProviderView()
        {
            InitializeComponent();
        }

        private async void ProvidersDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var provider = ((DataGrid)sender).CurrentCell.Item as ParsedEtwProvider;
            if (provider == null || provider.Id == Guid.Empty)
            {
                return;
            }

            var vm = DataContext as MainWindowViewModel;
            if (vm == null)
            {
                return;
            }

            //
            // Whenever a provider is double-clicked in our grid, we need to add a new contextual
            // tab to the MainWindow ribbon control. To do that, we need to find the tab control,
            // which is a visualtree child of our Fluent:Ribbon.
            //
            // This seems hacky, but we're doing it from code-behind so the WPF gods might smile.
            //
            var ribbon = UiHelper.FindChild<Fluent.Ribbon>(
                Application.Current.MainWindow, "MainWindowRibbon");
            if (ribbon == null)
            {
                return;
            }

            //
            // If the provider has never been double-clicked, parse and load its manifest.
            // If it has been accessed before, pull the VM from the cache. Each entry in
            // the cache points to a ProviderManifestViewModel.
            //
            var providerVm = vm.m_ProviderViewModel;
            var manifestVm = await providerVm.LoadProviderManifest(provider.Id);
            if (manifestVm == null)
            {
                providerVm.StateManager.ProgressState.UpdateProgressMessage(
                    $"Provider {provider.Id} has no manifest registered on the system.");
                return;
            }

            //
            // If the tab already exists for this manifest, just select the tab, otherwise
            // we have to create the tab now.
            //
            var tabName = UiHelper.GetUniqueTabName(provider.Id, "Manifest");
            var tab = ribbon.Tabs.Where(tab => tab.Name == tabName).FirstOrDefault();
            if (tab == null)
            {
                if (!CreateNewManifestTab(ribbon, tabName, provider))
                {
                    return;
                }

                //
                // Note: we're not done, we need to override parts of the HeaderTemplate for
                // the tab title and plumb up the "X" close button, but these are UI element
                // operations that cannot be done until the template is applied. These are
                // handled from within the tab's Loaded callback.
                //
            }
            else
            {
                tab.IsSelected = true;
            }

            //
            // Ask the mainwindow VM to display the manifest VM in its content control.
            //
            await vm.LoadProviderManifestCommand.ExecuteAsync(provider.Id);
        }

        private void ProvidersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null)
            {
                return;
            }

            //
            // Keep VM in sync with the view
            //
            if (ProvidersDataGrid.SelectedItems == null ||
                ProvidersDataGrid.SelectedItems.Count == 0)
            {
                vm.m_ProviderViewModel.SelectedProviders.Clear();
                return;
            }

            vm.m_ProviderViewModel.SelectedProviders =
                ProvidersDataGrid.SelectedItems.Cast<ParsedEtwProvider>().ToList();
        }

        private bool CreateNewManifestTab(Fluent.Ribbon Ribbon, string TabName, ParsedEtwProvider Provider)
        {
            var style = Ribbon.FindResource("ProviderContextTabStyle") as Style;
            if (style == null)
            {
                return false;
            }
            var newTab = new Fluent.RibbonTabItem
            {
                Name = TabName,
                Style = style,
                IsSelected = true,
            };

            newTab.Loaded += (s, e) =>
            {
                var ribbon = UiHelper.FindChild<Fluent.Ribbon>(
                                Application.Current.MainWindow, "MainWindowRibbon");
                if (ribbon == null)
                {
                    return;
                }
                UiHelper.FixupDynamicRibbonTab(ribbon,
                    newTab,
                    Provider.Name!,
                    "ProviderContextTabText",
                    "ProviderContextTabCloseButton",
                    () =>
                    {
                        var vm = DataContext as MainWindowViewModel;
                        if (vm == null)
                        {
                            return;
                        }
                        if (ribbon.Tabs.Count == 3)
                        {
                            vm.ProviderManifestVisible = Visibility.Hidden;
                            ribbon.SelectedTabIndex = 0;
                            ProvidersDataGrid.SelectedItems.Clear();
                        }
                    });
            };
            Ribbon.Tabs.Add(newTab);
            return true;
        }
    }
}
