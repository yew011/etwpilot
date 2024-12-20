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
using System.Data;
using System.Diagnostics;
using System.Windows.Controls;

namespace EtwPilot.View
{
    public partial class ProviderFilterFormView : UserControl
    {
        public ProviderFilterFormView()
        {
            InitializeComponent();
        }

        #region ScopeFilter

        private void ScopeFilterProcesses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (ScopeFilterProcesses.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.Processes = ScopeFilterProcesses.SelectedItems.Cast<ProcessObject>().Where(
                p => p.Pid != 0 && p.Name != "[None]").ToList();
        }

        private void ScopeFilterExes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (ScopeFilterExes.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.ExeNames = ScopeFilterExes.SelectedItems.Cast<string>().Where(
                p => p != "[None]").ToList();
        }

        private void ScopeFilterAppIds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (ScopeFilterAppIds.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.AppIds = ScopeFilterAppIds.SelectedItems.Cast<string>().Where(
                p => p != "[None]").ToList();
        }

        private void ScopeFilterPackageIds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (ScopeFilterPackageIds.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.PackageIds = ScopeFilterPackageIds.SelectedItems.Cast<string>().Where(
                p => p != "[None]").ToList();
        }

        #endregion

        #region AttributeFilter
        private void AttributeFilterEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (AttributeFilterEvents.SelectedItems == null || vm == null)
            {
                return;
            }
            var chosen = AttributeFilterEvents.SelectedItems.Cast<
                ParsedEtwManifestEvent>().ToList();
            //
            // Synchronize with VM's list
            //
            vm.AttributeFilter.Events.Clear();
            chosen.ForEach(e => vm.AttributeFilter.Events.Add(e));
        }

        private void AttributeFilterAnyKeywords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (AttributeFilterAnyKeywords.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.AttributeFilter.AnyKeywords.Clear();
            AttributeFilterAnyKeywords.SelectedItems.Cast<
                ParsedEtwManifestField>().ToList().ForEach(k => vm.AttributeFilter.AnyKeywords.Add(k));
        }

        private void AttributeFilterAllKeywords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (AttributeFilterAllKeywords.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.AttributeFilter.AllKeywords.Clear();
            AttributeFilterAllKeywords.SelectedItems.Cast<
                ParsedEtwManifestField>().ToList().ForEach(k => vm.AttributeFilter.AllKeywords.Add(k));
        }
        #endregion

        #region StackwalkFilter
        private void StackwalkFilterEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (StackwalkFilterEvents.SelectedItems == null || vm == null)
            {
                return;
            }

            //
            // Synchronize with VM's list
            //
            vm.StackwalkFilter.Events.Clear();
            var chosen = StackwalkFilterEvents.SelectedItems.Cast<
                ParsedEtwManifestEvent>().ToList();
            chosen.ForEach(e => vm.StackwalkFilter.Events.Add(e));
        }

        private void StackwalkLevelKeywordFilterAnyKeywords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (StackwalkLevelKeywordFilterAnyKeywords.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.StackwalkFilter.LevelKeywordFilterAnyKeywords.Clear();
            StackwalkLevelKeywordFilterAnyKeywords.SelectedItems.Cast<
                ParsedEtwManifestField>().ToList().ForEach(k => vm.StackwalkFilter.LevelKeywordFilterAnyKeywords.Add(k));
        }

        private void StackwalkLevelKeywordFilterAllKeywords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (StackwalkLevelKeywordFilterAllKeywords.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.StackwalkFilter.LevelKeywordFilterAllKeywords.Clear();
            StackwalkLevelKeywordFilterAllKeywords.SelectedItems.Cast<
                ParsedEtwManifestField>().ToList().ForEach(k => vm.StackwalkFilter.LevelKeywordFilterAllKeywords.Add(k));
        }
        #endregion

        #region PayloadFilter

        private void PayloadFilterEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            var evt = PayloadFilterEvents.SelectedItem as ParsedEtwManifestEvent;
            if (evt == null)
            {
                PredicateFieldName.ItemsSource = new List<ParsedEtwManifestField>();
                return;
            }

            var eventSchema = vm.Manifest.Events.Where(
                e => e.Id == evt.Id && e.Version == evt.Version).FirstOrDefault();
            if (eventSchema == null)
            {
                Debug.Assert(false);
                return;
            }
            var template = eventSchema.Template;
            if (string.IsNullOrEmpty(template) || !vm.Manifest.Templates.ContainsKey(template))
            {
                Debug.Assert(false);
                return;
            }
            PredicateFieldName.ItemsSource = vm.Manifest.Templates[template];
        }

        private void PayloadFilterPredicates_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            var filter = ((DataGrid)sender).CurrentCell.Item as PayloadFilterPredicateViewModel;
            if (filter == null)
            {
                return;
            }
            vm.SelectedPredicate = filter;
            vm.SelectedPredicate.IsUpdateMode = true;
        }

        private void PayloadFilterPredicates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            vm.RemovePredicateCommand.NotifyCanExecuteChanged();
        }

        #endregion

        #region Choose Columns

        private void ChosenEtwColumnsDataGrid_DataGridCellDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var cell = sender as DataGridCell;
            if (cell == null || cell.Column.Header.ToString() != "IConverter")
            {
                return;
            }
            var vm = DataContext as ProviderFilterFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            var info = new DataGridCellInfo(cell);
            var filter = info.Item as EtwColumnViewModel;
            if (filter == null)
            {
                return;
            }
            vm.EditingEtwColumn = true;
            NotifyCanExecuteChangedCommandButtons(vm);
        }

        private void ChosenEtwColumnsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            vm.EditingEtwColumn = false;
            NotifyCanExecuteChangedCommandButtons(vm);
        }

        private void ChosenEtwColumnsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            NotifyCanExecuteChangedCommandButtons(vm);
        }

        private void AvailableEtwColumnsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as ProviderFilterFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            NotifyCanExecuteChangedCommandButtons(vm);
        }

        private void NotifyCanExecuteChangedCommandButtons(ProviderFilterFormViewModel Vm)
        {
            Vm.RemoveEtwColumnCommand.NotifyCanExecuteChanged();
            Vm.ClearEtwColumnCommand.NotifyCanExecuteChanged();
            Vm.AddEtwColumnsCommand.NotifyCanExecuteChanged();
            Vm.AddDefaultEtwColumnsCommand.NotifyCanExecuteChanged();
        }

        #endregion

        private void ProviderFilterFormTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //
            // When the ChooseEtwColumnsTab becomes selected, we have to refresh available
            // columns - if no events from applicable tabs have been selected, we need to
            // add all possible field/column names to the lsit.
            //
            var tabControl = sender as TabControl;
            if (tabControl == null)
            {
                Debug.Assert(false);
                return;
            }
            var tab = tabControl.SelectedItem as TabItem;
            if (tab == null || tab.Name != "ChooseEtwColumnsTab")
            {
                return;
            }
            var vm = DataContext as ProviderFilterFormViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            e.Handled = true;
            vm.RefreshAvailableEtwColumnsCommand.Execute(null);
        }
    }
}
