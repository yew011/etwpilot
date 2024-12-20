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
using System.Diagnostics;

namespace EtwPilot.View
{
    public partial class LiveSessionView : UserControl
    {
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

        private void LiveSessionProviderDataTabControl_LostFocus(object sender, RoutedEventArgs e)
        {
            //
            // When the provider data tab control loses focus, we want to retain the currently
            // selected tab page.
            //
            var vm = DataContext as LiveSessionViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            vm.SelectedProviderIndex = LiveSessionProviderDataTabControl.SelectedIndex;
        }

        private void LiveSessionProviderDataTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as LiveSessionViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            if (e.AddedItems.Count == 0)
            {
                return;
            }
            var tab = e.AddedItems[0] as TabItem;
            if (tab == null)
            {
                return;
            }
            var providerTraceData = tab.DataContext as ProviderTraceData;
            if (providerTraceData == null)
            {
                Debug.Assert(false);
                return;
            }
            vm.SelectedProviderTraceData = providerTraceData;
        }
    }
}
