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
using etwlib;
using Fluent;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using Brushes = System.Windows.Media.Brushes;
using System.Windows.Forms;
using System.Text;
using EtwPilot.Utilities;
using System.Windows.Controls.Primitives;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;

    internal class MainWindowViewModel : ViewModelBase
    {
        internal enum ExportFormat
        {
            Csv = 1,
            Xml,
            Json,
            Custom
        }

        #region Properties

        private ProgressState _m_ProgressState;
        public ProgressState m_ProgressState
        {
            get => _m_ProgressState;
            set
            {
                if (_m_ProgressState != value)
                {
                    _m_ProgressState = value;
                    OnPropertyChanged("m_ProgressState");
                }
            }
        }

        private ViewModelBase _CurrentViewModel;
        public ViewModelBase CurrentViewModel
        {
            get => _CurrentViewModel;
            set
            {
                if (_CurrentViewModel != value)
                {
                    _CurrentViewModel = value;
                    OnPropertyChanged("CurrentViewModel");
                }
            }
        }

        public ProviderViewModel m_ProviderViewModel { get; set; }
        public SessionViewModel m_SessionViewModel { get; set; }
        public SessionFormViewModel m_SessionFormViewModel { get; set; }
        public InsightsViewModel m_InsightsViewModel { get; set; }

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

        private Visibility _CancelCommandButtonVisibility;
        public Visibility CancelCommandButtonVisibility
        {
            get => _CancelCommandButtonVisibility;
            set
            {
                if (_CancelCommandButtonVisibility != value)
                {
                    _CancelCommandButtonVisibility = value;
                    OnPropertyChanged("CancelCommandButtonVisibility");
                }
            }
        }

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
        #endregion

        #region commands

        public AsyncRelayCommand LoadProvidersCommand { get; set; }
        public AsyncRelayCommand LoadSessionsCommand { get; set; }
        public AsyncRelayCommand<Guid> LoadProviderManifestCommand { get; set; }
        public AsyncRelayCommand<ExportFormat> ExportProvidersCommand { get; set; }
        public AsyncRelayCommand<ExportFormat> ExportSessionsCommand { get; set; }
        public AsyncRelayCommand<ExportFormat> ExportLiveSessionDataCommand { get; set; }
        public AsyncRelayCommand DumpProviderManifestsCommand { get; set; }
        public AsyncRelayCommand SaveProvidersToClipboardCommand { get; set; }
        public AsyncRelayCommand SaveSessionsToClipboardCommand { get; set; }
        public AsyncRelayCommand SaveLiveSessionDataToClipboardCommand { get; set; }
        public AsyncRelayCommand<SelectionChangedEventArgs> SwitchCurrentViewModelCommand { get; set; }
        public AsyncRelayCommand<RoutedEventArgs> WindowLoadedCommand { get; set; }
        public AsyncRelayCommand CancelCurrentCommandCommand { get; set; }
        public AsyncRelayCommand NewSessionFromProviderCommand { get; set; }
        public AsyncRelayCommand NewSessionCommand { get; set; }
        public AsyncRelayCommand StartSessionCommand { get; set; }
        public AsyncRelayCommand StopSessionCommand { get; set; }
        public AsyncRelayCommand StopAllSessionsCommand { get; set; }
        public AsyncRelayCommand ShowFormPreviewCommand { get; set; }
        public AsyncRelayCommand SendToInsightsCommand { get; set; }
        public AsyncRelayCommand ShowDebugLogsCommand { get; set; }
        public AsyncRelayCommand ExitCommand { get; set; }

        public AsyncRelayCommand m_CurrentCommand { get; private set; }

        #endregion

        public MainWindowViewModel()
        {
            MiscHelper.ListResources();

            //
            // Set up commands
            //
            LoadProvidersCommand = new AsyncRelayCommand(Command_LoadProviders, CanExecute);
            LoadSessionsCommand = new AsyncRelayCommand(Command_LoadSessions, CanExecute);
            LoadProviderManifestCommand = new AsyncRelayCommand<Guid>(
                Command_LoadProviderManifest, _ => CanExecute());
            ExportProvidersCommand = new AsyncRelayCommand<ExportFormat>(
                Command_ExportProviders, _ => CanExecute());
            ExportSessionsCommand = new AsyncRelayCommand<ExportFormat>(
                Command_ExportSessions, _ => CanExecute());
            ExportLiveSessionDataCommand = new AsyncRelayCommand<ExportFormat>(
                Command_ExportLiveSessionData, _ => CanExecute());
            DumpProviderManifestsCommand = new AsyncRelayCommand(
                Command_DumpProviderManifests, CanExecute);
            SaveProvidersToClipboardCommand = new AsyncRelayCommand(
                Command_SaveProvidersToClipboard, CanExecute);
            SaveSessionsToClipboardCommand = new AsyncRelayCommand(
                Command_SaveSessionsToClipboard, CanExecute);
            SaveLiveSessionDataToClipboardCommand = new AsyncRelayCommand(
                Command_SaveLiveSessionDataToClipboard, CanExecute);
            SwitchCurrentViewModelCommand = new AsyncRelayCommand<SelectionChangedEventArgs>(
                Command_SwitchCurrentViewModel, _ => true);
            WindowLoadedCommand = new AsyncRelayCommand<RoutedEventArgs>(
                Command_WindowLoaded, _ => CanExecute());
            CancelCurrentCommandCommand = new AsyncRelayCommand(Command_CancelCurrentCommand);
            NewSessionFromProviderCommand = new AsyncRelayCommand(
                Command_NewSessionFromProvider, CanExecute);
            NewSessionCommand = new AsyncRelayCommand(
                Command_NewSession, CanExecute);
            StartSessionCommand = new AsyncRelayCommand(
                Command_StartSession, CanExecuteStartSession);
            StopSessionCommand = new AsyncRelayCommand(
                Command_StopSession, CanExecuteStopSession);
            StopAllSessionsCommand = new AsyncRelayCommand(
                Command_StopAllSessions, CanExecuteStopAllSessions);
            ShowFormPreviewCommand = new AsyncRelayCommand(
                Command_ShowFormPreview, CanExecuteStartSession);
            SendToInsightsCommand = new AsyncRelayCommand(
                Command_SendToInsights, CanExecuteSendToInsights);
            ShowDebugLogsCommand = new AsyncRelayCommand(
                Command_ShowDebugLogs, () => { return true; });
            ExitCommand = new AsyncRelayCommand(
                async () => { System.Windows.Application.Current.Shutdown(); }, () => { return true; });

            //
            // Our progress state is tied to statemanager.
            //
            m_ProgressState = new ProgressState();

            //
            // Setup StateManager for other viewmodels.
            //
            StateManager.ProgressState = m_ProgressState;
            StateManager.Settings = SettingsFormViewModel.LoadDefault();

            //
            // Subscribe to changes to the settings instance, so that autosave kicks in.
            // Note that the internal Save() routine in this class resets this value.
            //
            StateManager.Settings.PropertyChanged += (obj, p) =>
            {
                if (p.PropertyName == "HasUnsavedChanges")
                {
                    return;
                }
                StateManager.Settings.HasUnsavedChanges = true;
                if (p.PropertyName == "ModelPath" ||
                    p.PropertyName == "EmbeddingsModelFile" ||
                    p.PropertyName == "ModelConfig")
                {
                    StateManager.Settings.HasModelRelatedUnsavedChanges = true;
                }
            };

            //
            // Instatiate other viewmodels. SettingsFormViewModel must be instantiated first
            // as it sets StateManager.SettingsModel which other viewmodels use.
            //
            m_ProviderViewModel = new ProviderViewModel();
            m_SessionViewModel = new SessionViewModel();
            m_SessionFormViewModel = new SessionFormViewModel(); // lazy init
            m_InsightsViewModel = new InsightsViewModel();

            //
            // Subscribe to property change events in session form, so when the form becomes valid,
            // the session control buttons and associated commands are available.
            //
            m_SessionFormViewModel.ErrorsChanged += delegate (object? sender, DataErrorsChangedEventArgs e)
            {
                NotifyCanExecuteChangeForLiveSessionRelatedCommands();
            };

            //
            // Set current viewmodel
            //
            CurrentViewModel = m_ProviderViewModel;
            ProviderManifestVisible = Visibility.Hidden;
            LiveSessionsVisible = Visibility.Hidden;
            CancelCommandButtonVisibility = Visibility.Hidden;

            //
            // Initialize traces and set trace levels
            //
            etwlib.TraceLogger.Initialize();
            etwlib.TraceLogger.SetLevel(StateManager.Settings.TraceLevelEtwlib);
            EtwPilot.Utilities.TraceLogger.Initialize();
            EtwPilot.Utilities.TraceLogger.SetLevel(StateManager.Settings.TraceLevelApp);
            symbolresolver.TraceLogger.Initialize();
            symbolresolver.TraceLogger.SetLevel(StateManager.Settings.TraceLevelSymbolresolver);

            Trace(TraceLoggerType.MainWindow,
                  TraceEventType.Information,
                  "MainWindow opened");
        }

        private async Task Command_WindowLoaded(RoutedEventArgs Args)
        {
            //
            // This is the default tab displayed, so load it with content.
            //
            await Command_LoadProviders();
        }

        private async Task Command_CancelCurrentCommand()
        {
            if (m_CurrentCommand == null || !m_CurrentCommand.CanBeCanceled)
            {
                return;
            }
            m_CurrentCommand.Cancel();
        }

        private async Task Command_LoadProviders()
        {
            StateManager.ProgressState.InitializeProgress(2);
            CurrentViewModel = m_ProviderViewModel;
            await m_ProviderViewModel.LoadProviders();
            m_ProgressState.FinalizeProgress($"Loaded {m_ProviderViewModel.Providers.Count} providers");
        }

        private async Task Command_LoadProviderManifest(Guid Id)
        {
            m_ProgressState.InitializeProgress(1);
            var manifest = await m_ProviderViewModel.LoadProviderManifest(Id);
            if (manifest == null)
            {
                return;
            }
            CurrentViewModel = manifest;
            ProviderManifestVisible = Visibility.Visible;
            m_ProgressState.FinalizeProgress($"Loaded manifest for provider {Id}");
        }

        private async Task Command_LoadSessions()
        {
            StateManager.ProgressState.InitializeProgress(2);
            CurrentViewModel = m_SessionViewModel;
            await m_SessionViewModel.LoadSessions();
            m_ProgressState.FinalizeProgress($"Loaded {m_SessionViewModel.Sessions.Count} sessions");
        }

        private async Task Command_NewSession()
        {
            RibbonTabControlSelectedIndex = 1;
        }

        private async Task Command_NewSessionFromProvider()
        {
            var providers = GetSelectedProvidersFromVm();
            if (providers == null)
            {
                return;
            }
            m_SessionFormViewModel.InitialProviders = providers;
            RibbonTabControlSelectedIndex = 1;
        }

        private async Task<bool> Command_StartSession()
        {
            //
            // Load the form data into a model object
            //
            var model = m_SessionFormViewModel.GetFormData();
            if (model == null)
            {
                return false;
            }

            //
            // Create a new live session view model that represents this new trace session
            // CurrentViewModel must be updated now before the session tab is created,
            // hence we must do this outside of session view model.
            //
            var vm = new LiveSessionViewModel(
                model,
                () => {
                    NotifyCanExecuteChangeForLiveSessionRelatedCommands();
                },
                () => {
                    NotifyCanExecuteChangeForLiveSessionRelatedCommands();
                });
            CurrentViewModel = vm;

            //
            // Start it.
            //
            return await m_SessionViewModel.StartLiveSession(vm);
        }

        private async Task<bool> Command_StopSession()
        {
            var vm = CurrentViewModel as LiveSessionViewModel;
            if (vm == null)
            {
                return false;
            }
            var result = await m_SessionViewModel.StopLiveSession(vm);
            NotifyCanExecuteChangeForLiveSessionRelatedCommands();
            return result;
        }

        private async Task<bool> Command_StopAllSessions()
        {
            var result = await m_SessionViewModel.StopAllLiveSessions();
            NotifyCanExecuteChangeForLiveSessionRelatedCommands();
            return result;
        }

        private async Task<bool> Command_ShowFormPreview()
        {
            var popup = new Popup() {
                StaysOpen = false,
                Placement = PlacementMode.Mouse
            };
            var popBorder = new Border()
            {
                BorderBrush = Brushes.Black,
                Background = Brushes.LightYellow,
                BorderThickness = new Thickness(1)
            };
            var popContent = new TextBlock()
            {
                Text = $"{m_SessionFormViewModel}",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
            };
            popBorder.Child = popContent;
            popup.IsOpen = true;            
            popup.Child = popBorder;
            return true;
        }

        private async Task Command_ExportProviders(ExportFormat Format)
        {
            var providers = GetSelectedProvidersFromVm();
            if (providers == null)
            {
                return;
            }

            await DataExporter.Export(providers, Format, StateManager, "Providers");
        }

        private async Task Command_ExportSessions(ExportFormat Format)
        {
            var sessions = GetSelectedSessionsFromVm();
            if (sessions == null)
            {
                return;
            }

            if (Format == ExportFormat.Csv)
            {
                //
                // ParsedEtwSession has a list field that would not make sense for CSV.
                // So just stringify each object and pass that for export.
                //
                m_ProgressState.InitializeProgress(1);
                var lines = new List<string>();
                foreach (var session in sessions)
                {
                    lines.Add(session.ToString());
                }

                await DataExporter.Export(lines, ExportFormat.Custom, StateManager, "Sessions");
            }
            else
            {
                await DataExporter.Export(sessions, Format, StateManager, "Sessions");
            }
        }

        private async Task Command_ExportLiveSessionData(ExportFormat Format)
        {
            var vm = CurrentViewModel as LiveSessionViewModel;
            if (vm == null || vm.CurrentProviderTraceData == null || vm.CurrentProviderTraceData.Data.Count == 0)
            {
                return;
            }
            var list = vm.CurrentProviderTraceData.Data.ToList();
            await DataExporter.Export(list, Format, StateManager, "LiveSession");
        }

        private async Task Command_DumpProviderManifests(CancellationToken Token)
        {
            var providers = GetSelectedProvidersFromVm();
            if (providers == null)
            {
                return;
            }

            var browser = new FolderBrowserDialog();
            browser.Description = "Select a location to save the data";
            browser.RootFolder = Environment.SpecialFolder.MyComputer;
            var result = browser.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }

            var root = Path.Combine(browser.SelectedPath, "Provider Manifests");
            try
            {
                Directory.CreateDirectory(root);
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.Settings,
                      TraceEventType.Warning,
                      $"Unable to create root directory " +
                      $"'{root}': {ex.Message}");
                return;
            }

            m_CurrentCommand = DumpProviderManifestsCommand;
            CancelCommandButtonVisibility = Visibility.Visible;
            m_ProgressState.InitializeProgress(providers.Count);

            var numErrors = 0;
            var i = 1;
            foreach (var provider in providers)
            {
                if (Token.IsCancellationRequested)
                {
                    CancelCommandButtonVisibility = Visibility.Hidden;
                    m_ProgressState.FinalizeProgress("Operation cancelled");
                    return;
                }

                m_ProgressState.UpdateProgressMessage(
                    $"Dumping manifest for provider {provider.Name} ({i++} of {providers.Count})...");
                var target = Path.Combine(root, $"{provider.Id}.xml");
                var manifest = await m_ProviderViewModel.LoadProviderManifest(provider.Id);
                if (manifest == null)
                {
                    numErrors++;
                    m_ProgressState.UpdateProgressValue();
                    continue;
                }
                File.WriteAllText(target, manifest.SelectedProviderManifest.ToXml());
                m_ProgressState.UpdateProgressValue();
            }
            CancelCommandButtonVisibility = Visibility.Hidden;
            m_ProgressState.FinalizeProgress(
                $"Exported {providers.Count} manifests to {root} ({numErrors} errors)");
        }

        private async Task Command_SaveProvidersToClipboard()
        {
            var providers = GetSelectedProvidersFromVm();
            if (providers == null)
            {
                return;
            }
            m_ProgressState.InitializeProgress(1);
            System.Windows.Clipboard.SetText(DataExporter.GetDataAsCsv(providers));
            m_ProgressState.FinalizeProgress($"Copied {providers.Count} rows to clipboard");
        }

        private async Task Command_SaveSessionsToClipboard()
        {
            var sessions = GetSelectedSessionsFromVm();
            if (sessions == null)
            {
                return;
            }
            m_ProgressState.InitializeProgress(1);
            var sb = new StringBuilder();
            foreach (var session in sessions)
            {
                sb.AppendLine(session.ToString());
            }
            System.Windows.Clipboard.SetText(sb.ToString());
            m_ProgressState.FinalizeProgress($"Copied {sessions.Count} rows to clipboard");
        }

        private async Task Command_SaveLiveSessionDataToClipboard()
        {
            var vm = CurrentViewModel as LiveSessionViewModel;
            if (vm == null || vm.CurrentProviderTraceData == null || vm.CurrentProviderTraceData.Data.Count == 0)
            {
                return;
            }
            var list = vm.CurrentProviderTraceData.Data.ToList();
            System.Windows.Clipboard.SetText(DataExporter.GetDataAsCsv(list));
            m_ProgressState.FinalizeProgress($"Copied {list.Count} rows to clipboard");
        }

        private async Task Command_SwitchCurrentViewModel(SelectionChangedEventArgs Args)
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

            switch (tab.Name)
            {
                case "ProvidersTab":
                    {
                        LiveSessionsVisible = Visibility.Hidden;
                        if (m_ProviderViewModel.Providers.Count == 0)
                        {
                            await Command_LoadProviders();
                        }
                        else
                        {
                            CurrentViewModel = m_ProviderViewModel;
                        }
                        if (m_ProviderViewModel.SelectedProviders.Count > 0)
                        {
                            ProviderManifestVisible = Visibility.Visible;
                        }
                        break;
                    }
                case "SessionsTab":
                    {
                        ProviderManifestVisible = Visibility.Hidden;
                        if (m_SessionViewModel.Sessions.Count == 0)
                        {
                            await Command_LoadSessions();
                        }
                        else
                        {
                            CurrentViewModel = m_SessionViewModel;
                        }
                        LiveSessionsVisible = m_SessionViewModel.HasLiveSessions() ?
                            Visibility.Visible : Visibility.Hidden;
                        break;
                    }
                case "InsightsTab":
                    {
                        LiveSessionsVisible = Visibility.Hidden;
                        ProviderManifestVisible = Visibility.Hidden;
                        CurrentViewModel = m_InsightsViewModel;
                        break;
                    }
                default:
                    {
                        //
                        // Important: do not NULL CurrentViewModel here.
                        // It could be a live session tab whose VM has
                        // not be setup yet and nulling here breaks.
                        //
                        LiveSessionsVisible = Visibility.Hidden;
                        ProviderManifestVisible = Visibility.Hidden;
                        break;
                    }
            }

            //
            // For provider manifest and live session contextual tabs,
            // we want to simply retrieve the correct underlying VM from
            // the cache inside the owning viewmodel.
            //
            if (tab.Name.StartsWith("Manifest_"))
            {
                ProviderManifestVisible = Visibility.Visible;
                var vm = m_ProviderViewModel.GetVmForTab(tab.Name)!;
                if (vm != null)
                {
                    CurrentViewModel = vm;
                }
                Debug.Assert(CurrentViewModel != null);
            }
            else if (tab.Name.StartsWith("LiveSession_"))
            {
                //
                // NB: if we're being invoked as a side effect of Command_StartSession,
                // the tab has been created but the VM has not - so this will return
                // null here. This is fine, because Command_StartSession sets the
                // CurrentViewModel once it completes setting up the trace.
                //
                LiveSessionsVisible = Visibility.Visible;
                var vm = m_SessionViewModel.GetVmForTab(tab.Name)!;
                if (vm != null)
                {
                    CurrentViewModel = vm;
                }
                //
                // Force stop button to re-evaluate if it should be enabled or disabled
                // based on the VM's live session we are switch to.
                //
                NotifyCanExecuteChangeForLiveSessionRelatedCommands();
            }
        }

        private async Task Command_SendToInsights()
        {
            var vm = CurrentViewModel as LiveSessionViewModel;
            if (vm == null)
            {
                return;
            }
            CurrentViewModel = m_InsightsViewModel;
            m_InsightsViewModel.LoadData(vm);
        }

        private async Task Command_ShowDebugLogs()
        {
            var psi = new ProcessStartInfo();
            psi.FileName = SettingsFormViewModel.DefaultWorkingDirectory;
            psi.UseShellExecute = true;
            Process.Start(psi);
        }

        private List<ParsedEtwProvider>? GetSelectedProvidersFromVm()
        {
            var providers = m_ProviderViewModel.SelectedProviders;
            if (providers == null || providers.Count == 0)
            {
                if (m_ProviderViewModel.Providers == null || m_ProviderViewModel.Providers.Count == 0)
                {
                    return null;
                }
                return m_ProviderViewModel.Providers.ToList();
            }
            return providers;
        }

        private List<ParsedEtwSession>? GetSelectedSessionsFromVm()
        {
            var sessions = m_SessionViewModel.SelectedSessions;
            if (sessions == null || sessions.Count == 0)
            {
                if (m_SessionViewModel.Sessions == null || m_SessionViewModel.Sessions.Count == 0)
                {
                    return null;
                }
                return m_SessionViewModel.Sessions.ToList();
            }
            return sessions;
        }

        private bool CanExecute()
        {
            if (m_ProgressState.TaskInProgress())
            {
                return false;
            }
            return true;
        }

        private bool CanExecuteStartSession()
        {
            return !m_SessionFormViewModel.HasErrors;
        }

        private bool CanExecuteStopSession()
        {
            var vm = CurrentViewModel as LiveSessionViewModel;
            if (vm == null)
            {
                return false;
            }
            return vm.IsRunning();
        }

        private bool CanExecuteStopAllSessions()
        {
            return m_SessionViewModel.HasActiveLiveSessions();
        }

        private bool CanExecuteSendToInsights()
        {
            var vm = CurrentViewModel as LiveSessionViewModel;
            if (vm == null || string.IsNullOrEmpty(StateManager.Settings.ModelPath) || vm.CurrentProviderTraceData == null)
            {
                return false;
            }
            return vm.CurrentProviderTraceData.Data.Count > 0;
        }

        private void NotifyCanExecuteChangeForLiveSessionRelatedCommands()
        {
            StartSessionCommand.NotifyCanExecuteChanged();
            StopSessionCommand.NotifyCanExecuteChanged();
            StopAllSessionsCommand.NotifyCanExecuteChanged();
            ShowFormPreviewCommand.NotifyCanExecuteChanged();
            SendToInsightsCommand.NotifyCanExecuteChanged();
        }
    }
}
