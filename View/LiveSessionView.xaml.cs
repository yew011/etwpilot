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
using EtwPilot.ViewModel;
using System.Windows;
using System.Windows.Controls;
using EtwPilot.Utilities;
using System.Windows.Data;
using System.Diagnostics;

namespace EtwPilot.View
{
    public partial class LiveSessionView : UserControl
    {
        public LiveSessionView()
        {
            InitializeComponent();
            //
            // EtwPilot creates some UI elements dynamically. This is one of them. When a user
            // creates a live session, each enabled provider in the ETW session has its own tab
            // that contains a datagrid of logged etw data. All of that logic belongs in the ViewModel,
            // not here in the code-behind. But that means the VM also has to create the UI
            // element (ie, the new tab). It can only do that with the actual TabControl container,
            // which is why we're exposing it here.
            //
            GlobalStateViewModel.Instance.g_SessionFormViewModel.TabControl = LiveSessionTabControl;
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            UiHelper.Expander_Expanded(sender, e);
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            UiHelper.Expander_Collapsed(sender, e);
        }

        private async void LiveSessionTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            //
            // Every time the user switches away from the TabControl, the tabs and their
            // contents are destroyed (this is by design in WPF), so we have to recreate
            // them. Luckily this is not an expensive operation.
            //
            var vm = UiHelper.GetViewModelFromFrameworkElement<
                LiveSessionViewModel>(sender as FrameworkElement);
            if (vm == null)
            {
                return;
            }
            await vm.CreateTabsForProvidersCommand.ExecuteAsync(ProviderDataGrid_AutoGeneratingColumn);
        }

        private void ProviderDataGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var providerData = UiHelper.GetViewModelFromFrameworkElement<
                ProviderTraceData>(sender as FrameworkElement);
            if (providerData == null)
            {
                return;
            }

            //
            // Remove the column if not in the list specified for this provider.
            //
            var columnHeader = e.Column.Header.ToString();
            var matchedColumn = providerData.Columns.Where(c => c.UniqueName == columnHeader).FirstOrDefault();
            if (matchedColumn == default || matchedColumn == null)
            {
                e.Cancel = true;
                return;
            }

            //
            // Set a custom IConverter if supplied
            //
            if (!string.IsNullOrEmpty(matchedColumn.IConverterCode))
            {
                var textColumn = e.Column as DataGridTextColumn;
                if (textColumn != null)
                {
                    var iconverter = GlobalStateViewModel.Instance.g_ConverterLibrary.GetIConverter(
                        matchedColumn.UniqueName);
                    if (iconverter != null)
                    {
                        textColumn.Binding = new Binding()
                        {
                            Path = new PropertyPath(matchedColumn.Name), // Note: 'Name' here.
                            Converter = iconverter,
                        };
                    }
                    else
                    {
                        Debug.Assert(false);
                    }
                }
            }
        }

        private void LiveSessionTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
            var vm = DataContext as LiveSessionViewModel;
            if (vm == null)
            {
                return;
            }
            var tabVm = tab.DataContext as ProviderTraceData;
            if (tabVm == null)
            {
                return;
            }
            vm.CurrentProviderTraceData = tabVm;
        }
    }
}
