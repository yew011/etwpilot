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
using System.Windows.Input;

namespace EtwPilot.View
{
    /// <summary>
    /// Interaction logic for ProviderView.xaml
    /// </summary>
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
            var command = vm.LoadProviderManifestCommand;

            if (vm != null && command != null)
            {
                await command.ExecuteAsync(provider.Id);
            }
        }
    }
}
