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
using System.Windows.Controls;

namespace EtwPilot.View
{
    public partial class ProviderManifestView : UserControl
    {
        public ProviderManifestView()
        {
            InitializeComponent();
        }

        private void Expander_Expanded(object sender, System.Windows.RoutedEventArgs e)
        {
            Utilities.UiHelper.Expander_Expanded(sender, e);
        }

        private void Expander_Collapsed(object sender, System.Windows.RoutedEventArgs e)
        {
            Utilities.UiHelper.Expander_Collapsed(sender, e);
        }

        private void ManifestTabControl_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            var tabcontrol = sender as TabControl;
            if (tabcontrol == null)
            {
                return;
            }
            var vm = tabcontrol.DataContext as ProviderManifestViewModel;
            if (vm == null)
            {
                return;
            }
            //
            // Remember the last selected index to restore it when the tab is loaded again
            // This prevents multiple provider manifest tabs from clobbering each other as
            // user switches between those tabs.
            //
            vm.TabControlSelectedIndex = tabcontrol.SelectedIndex;
        }
    }
}
