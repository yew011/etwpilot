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
using EtwPilot.Model;

namespace EtwPilot.View
{
    using static EtwPilot.Utilities.TraceLogger;

    public partial class LiveSessionView : UserControl
    {
        private LiveSessionViewModel _ViewModel; // hack

        public LiveSessionView()
        {
            InitializeComponent();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            UiHelper.Expander_Expanded(sender, e);
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            UiHelper.Expander_Collapsed(sender, e);
        }

        private void LiveSessionTabControl_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void LiveSessionTabControl_Loaded(object sender, RoutedEventArgs e)
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
            _ViewModel = vm;

            foreach (var provider in vm.Configuration.ConfiguredProviders)
            {
                var data = vm.RegisterProviderTab(provider);
                if (data == null)
                {
                    return;
                }
                if (!CreateTab(provider, data))
                {
                    return;
                }
            }
        }

        private bool CreateTab(ConfiguredProvider Provider, ProviderTraceData Data)
        {
            var tabName = UiHelper.GetUniqueTabName(Provider._EnabledProvider.Id, "ProviderLiveSession");
            Func<Task<bool>> tabClosedCallback = async delegate ()
            {
                Data.IsEnabled = false;
                return true;
            };
            var providerDataGrid = new DataGrid()
            {
                //
                // Important: Fixing the control to a MaxHeight and MaxWidth
                // is required, as otherwise the Grid will attempt to re-calculate
                // control bounds on every single row/ column.Etw captures are
                // noisy and will hang the UI.
                //
                Name = UiHelper.GetUniqueTabName(Provider._EnabledProvider.Id,"ProviderData"),
                AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue,
                EnableColumnVirtualization = true,
                EnableRowVirtualization = true,
                IsReadOnly = true,
                AutoGenerateColumns = true,
                RowHeight = 25,
                MaxHeight = 1600,
                MaxWidth = 1600,
                ItemsSource = Data.Data.AsObservable,
                DataContext = Data,
            };
            providerDataGrid.AutoGeneratingColumn += ProviderDataGrid_AutoGeneratingColumn;
            if (!UiHelper.CreateTabControlContextualTab(
                    LiveSessionTabControl,
                    providerDataGrid,
                    tabName,
                    Provider._EnabledProvider.Name!,
                    "LiveSessionTabStyle",
                    "LiveSessionTabText",
                    "LiveSessionTabCloseButton",
                    Data,
                    tabClosedCallback))
            {
                Trace(TraceLoggerType.LiveSession,
                      TraceEventType.Error,
                      $"Unable to create live session tab {tabName}");
                return false;
            }
            return true;
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
                    var iconverter = _ViewModel.GetIConverter(matchedColumn.UniqueName);
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
