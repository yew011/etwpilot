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
using System.Windows.Data;
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
            await GlobalStateViewModel.Instance.g_ProviderViewModel.LoadProviderManifestCommand.ExecuteAsync(provider);
        }

        private void ProvidersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GlobalStateViewModel.Instance.g_ProviderViewModel;

            //
            // Keep VM in sync with the view
            //
            if (ProvidersDataGrid.SelectedItems == null ||
                ProvidersDataGrid.SelectedItems.Count == 0)
            {
                vm.SelectedProviders.Clear();
                return;
            }

            vm.SelectedProviders =
                ProvidersDataGrid.SelectedItems.Cast<ParsedEtwProvider>().ToList();
        }

        private void filterByIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(sender);
        }

        private void filterByNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(sender);
        }

        private void ApplyFilter(object sender)
        {
            var searchBox = sender as TextBox;
            if (searchBox == null)
            {
                return;
            }
            var filter = searchBox.Text;
            var cv = CollectionViewSource.GetDefaultView(ProvidersDataGrid.ItemsSource);
            if (string.IsNullOrEmpty(filter))
            {
                cv.Filter = null;
            }
            else
            {
                cv.Filter = o =>
                {
                    var provider = o as ParsedEtwProvider;
                    if (provider == null)
                    {
                        return true;
                    }

                    switch (searchBox.Name)
                    {
                        case "filterByIdTextBox":
                            {
                                return provider.Id.ToString().Contains(filter, StringComparison.InvariantCultureIgnoreCase);
                            }
                        case "filterByNameTextBox":
                            {
                                return string.IsNullOrEmpty(provider.Name) ? false :
                                    provider.Name.ToString().Contains(filter, StringComparison.InvariantCultureIgnoreCase);
                            }
                        default:
                            {
                                return true;
                            }
                    }
                };
            }
        }
    }
}
