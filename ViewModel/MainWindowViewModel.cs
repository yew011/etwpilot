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
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace EtwPilot.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        #region observable properties
        
        private int _RibbonTabControlSelectedIndex;
        public int RibbonTabControlSelectedIndex
        {
            get => _RibbonTabControlSelectedIndex;
            set
            {
                if (_RibbonTabControlSelectedIndex != value)
                {
                    _RibbonTabControlSelectedIndex = value;
                    OnPropertyChanged("RibbonTabControlSelectedIndex");
                }
            }
        }

        private Visibility _LiveSessionsVisible;
        public Visibility LiveSessionsVisible
        {
            get => _LiveSessionsVisible;
            set
            {
                if (_LiveSessionsVisible != value)
                {
                    _LiveSessionsVisible = value;
                    OnPropertyChanged("LiveSessionsVisible");
                }
            }
        }

        private Visibility _ProviderManifestVisible;
        public Visibility ProviderManifestVisible
        {
            get => _ProviderManifestVisible;
            set
            {
                if (_ProviderManifestVisible != value)
                {
                    _ProviderManifestVisible = value;
                    OnPropertyChanged("ProviderManifestVisible");
                }
            }
        }
        #endregion

        #region commands

        public AsyncRelayCommand<SelectionChangedEventArgs> TabSelectionChangedCommand { get; set; }
        public AsyncRelayCommand<RoutedEventArgs> WindowLoadedCommand { get; set; }
        public RelayCommand ShowDebugLogsCommand { get; set; }
        public RelayCommand ExitCommand { get; set; }
        public AsyncRelayCommand<ViewModelBase> CloseDynamicTab { get; set; }

        #endregion

        public MainWindowViewModel() : base()
        {
            TabSelectionChangedCommand = new AsyncRelayCommand<SelectionChangedEventArgs>(
                Command_TabSelectionChanged, _ => true);
            WindowLoadedCommand = new AsyncRelayCommand<RoutedEventArgs>(
                Command_WindowLoaded, _ => true);
            ShowDebugLogsCommand = new RelayCommand(
                Command_ShowDebugLogs, () => { return true; });
            ExitCommand = new RelayCommand(
                () => { Application.Current.Shutdown(); }, () => { return true; });
            CloseDynamicTab = new AsyncRelayCommand<ViewModelBase>(
                Command_CloseDynamicTab, _ => true);
            ProviderManifestVisible = Visibility.Hidden;
            LiveSessionsVisible = Visibility.Hidden;
            ProgressState = new ProgressState();
        }

        private async Task Command_WindowLoaded(RoutedEventArgs? Args)
        {
            //
            // Set our initial view to GlobalInitViewModel to show the "please wait" plea
            //
            await GlobalStateViewModel.Instance.g_InitViewModel.ViewModelActivated();

            //
            // Initialize traces and set trace levels
            // NB: EtwPilot.TraceLogger was initialized in GlobalStateViewModel ctor to catch
            // early errors.
            //
            etwlib.TraceLogger.Initialize();
            etwlib.TraceLogger.SetLevel(GlobalStateViewModel.Instance.Settings.TraceLevelEtwlib);
            EtwPilot.Utilities.TraceLogger.SetLevel(GlobalStateViewModel.Instance.Settings.TraceLevelApp);
            symbolresolver.TraceLogger.Initialize();
            symbolresolver.TraceLogger.SetLevel(GlobalStateViewModel.Instance.Settings.TraceLevelSymbolresolver);

            //
            // Kick off application-wide initialization. All other views will be hidden until
            // this completes.
            //
            await GlobalStateViewModel.Instance.g_InitViewModel.GlobalResourceInitialization();

            //
            // This is the default tab displayed, so load it with content.
            //
            await GlobalStateViewModel.Instance.g_ProviderViewModel.ViewModelActivated();
        }

        private async Task Command_TabSelectionChanged(SelectionChangedEventArgs? Args)
        {
            string tabName;
            object tabDataContext;

            if (Args == null)
            {
                return;
            }
            else if (Args.AddedItems.Count > 0)
            {
                //
                // A RibbonTabItem was just added to the ribbon tab control.
                //
                var tab = Args.AddedItems[0] as Fluent.RibbonTabItem;
                if (tab == null || tab.Name == null)
                {
                    Debug.Assert(false);
                    return;
                }
                tabName = tab.Name;
                tabDataContext = tab.DataContext;
            }
            else if (Args.RemovedItems.Count > 0)
            {
                //
                // A RibbonTabItem was just removed from the ribbon tab control.
                // The only way this can happen is from our contextual tab groups,
                // via UiHelper!RemoveRibbonContextualTab and that routine handles
                // setting the selected tab after removal.
                //
                return;
            }
            else
            {
                return;
            }

            GlobalStateViewModel.Instance.PrimaryViewEnabled = false;
            ProviderManifestVisible = Visibility.Hidden;
            LiveSessionsVisible = Visibility.Hidden;

            if (tabName == "ProvidersTab")
            {
                await GlobalStateViewModel.Instance.g_ProviderViewModel.ViewModelActivated();
                if (GlobalStateViewModel.Instance.g_ProviderViewModel.SelectedProviders.Count > 0)
                {
                    ProviderManifestVisible = Visibility.Visible;
                }
            }
            else if (tabName == "SessionsTab")
            {
                await GlobalStateViewModel.Instance.g_SessionViewModel.ViewModelActivated();
                LiveSessionsVisible = GlobalStateViewModel.Instance.g_SessionViewModel.HasLiveSessions() ?
                    Visibility.Visible : Visibility.Hidden;
            }
            else if (tabName == "InsightsTab")
            {
                await GlobalStateViewModel.Instance.g_InsightsViewModel.ViewModelActivated();
            }
            else if (tabName.StartsWith("Manifest_"))
            {
                //
                // The provider VM maintains a list of instantiated manifest VMs.
                //
                await GlobalStateViewModel.Instance.g_ProviderViewModel.ActivateProviderManifestViewModel(tabName);
                ProviderManifestVisible = Visibility.Visible;
            }
            else if (tabName.StartsWith("LiveSession_"))
            {
                if (tabDataContext is not LiveSessionViewModel)
                {
                    Debug.Assert(false);
                    return;
                }
                var vm = tabDataContext as LiveSessionViewModel;
                //
                // The session VM maintains a list of instantiated LiveSession VMs.
                //
                await GlobalStateViewModel.Instance.g_SessionViewModel.ShowSessionCommand.ExecuteAsync(vm);
                LiveSessionsVisible = Visibility.Visible;
            }

            GlobalStateViewModel.Instance.PrimaryViewEnabled = true;
        }

        private void Command_ShowDebugLogs()
        {
            var psi = new ProcessStartInfo();
            psi.FileName = SettingsFormViewModel.DefaultWorkingDirectory;
            psi.UseShellExecute = true;
            Process.Start(psi);
        }

        public async Task Command_CloseDynamicTab(ViewModelBase? ViewModel)
        {
            if (ViewModel == null)
            {
                Debug.Assert(false);
                return;
            }
            var vm = ViewModel as LiveSessionViewModel;
            if (vm != null)
            {
                await GlobalStateViewModel.Instance.g_SessionViewModel.CloseDynamicTab.ExecuteAsync(vm);
                return;
            }
            var vm2 = ViewModel as ProviderManifestViewModel;
            if (vm2 != null)
            {
                GlobalStateViewModel.Instance.g_ProviderViewModel.CloseDynamicTab.Execute(vm2);
                return;
            }
            var vm3 = ViewModel as ProviderFilterFormViewModel;
            if (vm3 != null)
            {
                GlobalStateViewModel.Instance.g_SessionFormViewModel.CloseDynamicTab.Execute(vm3);
                return;
            }
            Debug.Assert(false);
        }
    }
}
