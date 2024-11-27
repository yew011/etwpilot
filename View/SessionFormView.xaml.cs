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
using System.Windows.Forms;

namespace EtwPilot.View
{
    using UserControl = System.Windows.Controls.UserControl;
    using TabControl = System.Windows.Controls.TabControl;

    public partial class SessionFormView : UserControl
    {
        public SessionFormView()
        {
            InitializeComponent();

            //
            // EtwPilot creates some UI elements dynamically. This is one of them. When a user
            // adds a provider to their new session form, we create a tab to hold all of the
            // ETW session options for that provider. All of that logic belongs in the ViewModel,
            // not here in the code-behind. But that means the VM also has to create the UI
            // element (ie, the new tab). It can only do that with the actual TabControl container,
            // which is why we're exposing it here.
            //
            GlobalStateViewModel.Instance.g_SessionFormViewModel.TabControl = ProviderFiltersTabControl;
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
            var tabName = tab.Name;
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel;
            await vm.SwitchToProviderFormTabCommand.ExecuteAsync(tabName);
        }

    }
}
