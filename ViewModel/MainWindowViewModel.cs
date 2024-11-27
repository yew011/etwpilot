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
using Fluent;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using EtwPilot.Utilities;

namespace EtwPilot.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        #region observable properties
        
        private int m_RibbonTabControlSelectedIndex;
        public int RibbonTabControlSelectedIndex
        {
            get => m_RibbonTabControlSelectedIndex;
            set
            {
                if (m_RibbonTabControlSelectedIndex != value)
                {
                    m_RibbonTabControlSelectedIndex = value;
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
        public AsyncRelayCommand ShowDebugLogsCommand { get; set; }
        public AsyncRelayCommand ExitCommand { get; set; }

        #endregion

        public MainWindowViewModel() : base()
        {
            TabSelectionChangedCommand = new AsyncRelayCommand<SelectionChangedEventArgs>(
                Command_TabSelectionChanged, _ => true);
            WindowLoadedCommand = new AsyncRelayCommand<RoutedEventArgs>(
                Command_WindowLoaded, _ => true);
            ShowDebugLogsCommand = new AsyncRelayCommand(
                Command_ShowDebugLogs, () => { return true; });
            ExitCommand = new AsyncRelayCommand(
                async () => { System.Windows.Application.Current.Shutdown(); }, () => { return true; });
            ProviderManifestVisible = Visibility.Hidden;
            LiveSessionsVisible = Visibility.Hidden;
            ProgressState = new ProgressState();
        }

        private async Task Command_WindowLoaded(RoutedEventArgs Args)
        {
            GlobalStateViewModel.Instance.Initialize();

            //
            // Initialize traces and set trace levels
            //
            etwlib.TraceLogger.Initialize();
            etwlib.TraceLogger.SetLevel(GlobalStateViewModel.Instance.Settings.TraceLevelEtwlib);
            EtwPilot.Utilities.TraceLogger.Initialize();
            EtwPilot.Utilities.TraceLogger.SetLevel(GlobalStateViewModel.Instance.Settings.TraceLevelApp);
            symbolresolver.TraceLogger.Initialize();
            symbolresolver.TraceLogger.SetLevel(GlobalStateViewModel.Instance.Settings.TraceLevelSymbolresolver);

            //
            // This is the default tab displayed, so load it with content.
            //
            await GlobalStateViewModel.Instance.g_ProviderViewModel.ViewModelActivated();
        }

        private async Task Command_TabSelectionChanged(SelectionChangedEventArgs Args)
        {
            if (Args.AddedItems.Count == 0)
            {
                return;
            }
            var tab = Args.AddedItems[0] as RibbonTabItem;

            if (tab == null || tab.Name == null)
            {
                return;
            }

            ProviderManifestVisible = Visibility.Hidden;
            LiveSessionsVisible = Visibility.Hidden;

            if (tab.Name == "ProvidersTab")
            {
                await GlobalStateViewModel.Instance.g_ProviderViewModel.ViewModelActivated();
                if (GlobalStateViewModel.Instance.g_ProviderViewModel.SelectedProviders.Count > 0)
                {
                    ProviderManifestVisible = Visibility.Visible;
                }
            }
            else if (tab.Name == "SessionsTab")
            {
                await GlobalStateViewModel.Instance.g_SessionViewModel.ViewModelActivated();
                LiveSessionsVisible = GlobalStateViewModel.Instance.g_SessionViewModel.HasLiveSessions() ?
                    Visibility.Visible : Visibility.Hidden;
            }
            else if (tab.Name == "InsightsTab")
            {
                await GlobalStateViewModel.Instance.g_InsightsViewModel.ViewModelActivated();
            }
            else if (tab.Name.StartsWith("Manifest_"))
            {
                //
                // The provider VM maintains a list of instantiated manifest VMs.
                //
                await GlobalStateViewModel.Instance.g_ProviderViewModel.ActivateProviderManifestViewModel(tab.Name);
                ProviderManifestVisible = Visibility.Visible;
            }
            else if (tab.Name.StartsWith("LiveSession_"))
            {
                //
                // The session VM maintains a list of instantiated LiveSession VMs.
                //
                await GlobalStateViewModel.Instance.g_SessionViewModel.ActivateLiveSessionViewModel(tab.Name);
                LiveSessionsVisible = GlobalStateViewModel.Instance.g_SessionViewModel.HasLiveSessions() ? 
                    Visibility.Visible : Visibility.Hidden;
            }
        }

        private async Task Command_ShowDebugLogs()
        {
            var psi = new ProcessStartInfo();
            psi.FileName = SettingsFormViewModel.DefaultWorkingDirectory;
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
    }
}
