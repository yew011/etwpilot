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
            var vm = GetVm(sender);
            if (ScopeFilterProcesses.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.Processes = ScopeFilterProcesses.SelectedItems.Cast<ProcessObject>().ToList();
        }

        private void ScopeFilterExes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GetVm(sender);
            if (ScopeFilterExes.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.ExeNames = ScopeFilterExes.SelectedItems.Cast<string>().ToList();
        }

        private void ScopeFilterAppIds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GetVm(sender);
            if (ScopeFilterAppIds.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.AppIds = ScopeFilterAppIds.SelectedItems.Cast<string>().ToList();
        }

        private void ScopeFilterPackageIds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GetVm(sender);
            if (ScopeFilterPackageIds.SelectedItems == null || vm == null)
            {
                return;
            }
            vm.ScopeFilter.PackageIds = ScopeFilterPackageIds.SelectedItems.Cast<string>().ToList();
        }

        #endregion

        #region AttributeFilter
        private void AttributeFilterEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = GetVm(sender);
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
            var vm = GetVm(sender);
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
            var vm = GetVm(sender);
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
            var vm = GetVm(sender);
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
            var vm = GetVm(sender);
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
            var vm = GetVm(sender);
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
            var vm = GetVm(sender);
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
            var vm = GetVm(sender);
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
            var vm = GetVm(sender);
            if (vm == null)
            {
                return;
            }
            vm.RemovePredicateCommand.NotifyCanExecuteChanged();
        }

        #endregion

        private ProviderFilterFormViewModel? GetVm(object sender)
        {
            var control = sender as FrameworkElement;
            if (control == null)
            {
                return null;
            }
            var vm = control.DataContext as ProviderFilterFormViewModel;
            if (vm == null)
            {
                return null;
            }
            return vm;
        }
    }
}
