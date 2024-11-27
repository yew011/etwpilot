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
using System.Data;
using System.Diagnostics;
using System.Windows;
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

        private void ScopeFilterProcess_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            if (ScopeFilterProcesses.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.Processes = ScopeFilterProcesses.SelectedItems.Cast<ProcessObject>().Where(
                p => p.Pid != 0 && p.Name != "[None]").ToList();
        }

        private void ScopeFilterExes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            if (ScopeFilterExes.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.ExeNames = ScopeFilterExes.SelectedItems.Cast<string>().Where(e =>
                e != "[None]").ToList();
        }

        private void ScopeFilterAppIds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            if (ScopeFilterAppIds.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.AppIds = ScopeFilterAppIds.SelectedItems.Cast<string>().Where(e =>
                e != "[None]").ToList();
        }

        private void ScopeFilterPackageIds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            if (ScopeFilterPackageIds.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.PackageIds = ScopeFilterPackageIds.SelectedItems.Cast<string>().Where(e =>
                e != "[None]").ToList();
        }

        #endregion

        #region AttributeFilter
        private void AttributeFilterEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            if (AttributeFilterEvents.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.AttributeFilter.Events.Clear();
            AttributeFilterEvents.SelectedItems.Cast<
                ParsedEtwManifestEvent>().ToList().ForEach(e => vm.AttributeFilter.Events.Add(e));
        }

        private void AttributeFilterAnyKeywords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
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
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
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
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            if (AttributeFilterAllKeywords.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.StackwalkFilter.Events.Clear();
            StackwalkFilterEvents.SelectedItems.Cast<
                ParsedEtwManifestEvent>().ToList().ForEach(e => vm.StackwalkFilter.Events.Add(e));
        }

        private void StackwalkLevelKeywordFilterAnyKeywords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
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
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
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
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            if (vm == null)
            {
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
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            if (vm == null)
            {
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
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            if (vm == null)
            {
                return;
            }
            vm.RemovePredicateCommand.NotifyCanExecuteChanged();
        }

        #endregion

        #region Choose Columns

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
            {
                return;
            }
            var tab = e.AddedItems[0] as TabItem;
            if (tab == null || tab.Name != "ChooseEtwColumnsTab")
            {
                return;
            }
            RefreshAvailableEtwColumns(sender);
        }

        private void TabControl_GotFocus(object sender, RoutedEventArgs e)
        {
            //
            // This is hacky. Because we only use one static instance of this UI form,
            // relying on XAML binding updates to take care of refreshing form content,
            // the tab control's SelectionChanged callback won't get invoked when a
            // new provider form is added when there was already another provider form
            // and the "Choose Etw Columns" tab was already selected on that existing form.
            // Thus, in that case, the new provider's form will have NO available columns
            // to select from. To work around this, we'll just check for this case
            // whenever the tab receives focus.
            //
            if (AvailableEtwColumnsDataGrid.Items.Count > 0)
            {
                return;
            }
            RefreshAvailableEtwColumns(sender);
        }

        private void RefreshAvailableEtwColumns(object sender)
        {
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            var uniqueEvents = new List<ParsedEtwManifestEvent>();
            if (AttributeFilterEvents.SelectedItems != null)
            {
                var evt = AttributeFilterEvents.SelectedItems.Cast<
                ParsedEtwManifestEvent>().ToList();
                uniqueEvents = uniqueEvents.Union(evt).ToList();
            }
            if (StackwalkFilterEvents.SelectedItems != null)
            {
                var evt = StackwalkFilterEvents.SelectedItems.Cast<
                ParsedEtwManifestEvent>().ToList();
                uniqueEvents = uniqueEvents.Union(evt).ToList();
            }
            if (PayloadFilterEvents.SelectedItems != null)
            {
                var evt = PayloadFilterEvents.SelectedItems.Cast<
                ParsedEtwManifestEvent>().ToList();
                uniqueEvents = uniqueEvents.Union(evt).ToList();
            }
            if (uniqueEvents.Count == 0)
            {
                //
                // The template fields from all events will be available
                // for the user to pick from.
                //
                uniqueEvents = AttributeFilterEvents.Items.Cast<ParsedEtwManifestEvent>().ToList().Union(
                    StackwalkFilterEvents.Items.Cast<
                        ParsedEtwManifestEvent>().ToList().Union(
                        PayloadFilterEvents.Items.Cast<ParsedEtwManifestEvent>().ToList())).ToList();
            }
            Debug.Assert(uniqueEvents.Count > 0);
            vm.SetAvailableEtwColumnsFromUniqueEvents(uniqueEvents);
        }

        private void ChosenEtwColumnsDataGrid_DataGridCellDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var cell = sender as DataGridCell;
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            if (vm == null || cell == null || cell.Column.Header.ToString() != "IConverter")
            {
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
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            vm.EditingEtwColumn = false;
            NotifyCanExecuteChangedCommandButtons(vm);
        }

        private void ChosenEtwColumnsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
            NotifyCanExecuteChangedCommandButtons(vm);
        }

        private void AvailableEtwColumnsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GlobalStateViewModel.Instance.g_SessionFormViewModel.CurrentProviderFilterForm;
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

    }
}
