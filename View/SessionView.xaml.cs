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
using EtwPilot.ViewModel;
using System.Windows.Controls;

namespace EtwPilot.View
{
    public partial class SessionView : UserControl
    {
        public SessionView()
        {
            InitializeComponent();
        }

        private void SessionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null)
            {
                return;
            }

            //
            // Keep VM in sync with the view
            //
            if (SessionsDataGrid.SelectedItems == null ||
                SessionsDataGrid.SelectedItems.Count == 0)
            {
                vm.m_SessionViewModel.SelectedSessions.Clear();
                return;
            }

            vm.m_SessionViewModel.SelectedSessions =
                SessionsDataGrid.SelectedItems.Cast<ParsedEtwSession>().ToList();
        }

        private void Expander_Expanded(object sender, System.Windows.RoutedEventArgs e)
        {
            Utilities.UiHelper.Expander_Expanded(sender, e);
        }

        private void Expander_Collapsed(object sender, System.Windows.RoutedEventArgs e)
        {
            Utilities.UiHelper.Expander_Collapsed(sender, e);
        }
    }
}
