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
using MessageBox = System.Windows.MessageBox;
using System.Windows.Forms;
using System.Text;
using EtwPilot.Utilities;
using System.Drawing;
using System.Windows.Media.Imaging;

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

        private ProviderViewModel _m_ProviderViewModel;
        public ProviderViewModel m_ProviderViewModel
        {
            get => _m_ProviderViewModel;
            set
            {
                if (_m_ProviderViewModel != value)
                {
                    _m_ProviderViewModel = value;
                    OnPropertyChanged("m_ProviderViewModel");
                }
            }
        }

        private SettingsFormViewModel _m_SettingsFormViewModel;
        public SettingsFormViewModel m_SettingsFormViewModel
        {
            get => _m_SettingsFormViewModel;
            set
            {
                if (_m_SettingsFormViewModel != value)
                {
                    _m_SettingsFormViewModel = value;
                    OnPropertyChanged("m_SettingsFormViewModel");
                }
            }
        }

        private SessionViewModel _m_SessionViewModel;
        public SessionViewModel m_SessionViewModel
        {
            get => _m_SessionViewModel;
            set
            {
                if (_m_SessionViewModel != value)
                {
                    _m_SessionViewModel = value;
                    OnPropertyChanged("m_SessionViewModel");
                }
            }
        }

        private SessionFormViewModel _m_SessionFormViewModel;
        public SessionFormViewModel m_SessionFormViewModel
        {
            get => _m_SessionFormViewModel;
            set
            {
                if (_m_SessionFormViewModel != value)
                {
                    _m_SessionFormViewModel = value;
                    OnPropertyChanged("_m_SessionFormViewModel");
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

        #endregion

        #region commands

        public AsyncRelayCommand LoadProvidersCommand { get; set; }
        public AsyncRelayCommand LoadSessionsCommand { get; set; }
        public AsyncRelayCommand<Guid> LoadProviderManifestCommand { get; set; }
        public AsyncRelayCommand<ExportFormat> ExportProvidersCommand { get; set; }
        public AsyncRelayCommand<ExportFormat> ExportSessionsCommand { get; set; }
        public AsyncRelayCommand DumpProviderManifestsCommand { get; set; }
        public AsyncRelayCommand SaveProvidersToClipboardCommand { get; set; }
        public AsyncRelayCommand SaveSessionsToClipboardCommand { get; set; }
        public AsyncRelayCommand<SelectionChangedEventArgs> SwitchCurrentViewModelCommand { get; set; }
        public AsyncRelayCommand<RoutedEventArgs> BackstageMenuClickCommand { get; set; }
        public AsyncRelayCommand<CancelEventArgs> WindowClosingCommand { get; set; }
        public AsyncRelayCommand<RoutedEventArgs> WindowLoadedCommand { get; set; }
        public AsyncRelayCommand CancelCurrentCommandCommand { get; set; }
        public AsyncRelayCommand NewSessionFromProviderCommand { get; set; }
        public AsyncRelayCommand NewSessionCommand { get; set; }
        public AsyncRelayCommand StartSessionCommand { get; set; }
        public AsyncRelayCommand StopSessionCommand { get; set; }

        #endregion

        public AsyncRelayCommand m_CurrentCommand { get; private set; }

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

        public MainWindowViewModel()
        {
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
            DumpProviderManifestsCommand = new AsyncRelayCommand(
                Command_DumpProviderManifests, CanExecute);
            SaveProvidersToClipboardCommand = new AsyncRelayCommand(
                Command_SaveProvidersToClipboard, CanExecute);
            SaveSessionsToClipboardCommand = new AsyncRelayCommand(
                Command_SaveSessionsToClipboard, CanExecute);
            SwitchCurrentViewModelCommand = new AsyncRelayCommand<SelectionChangedEventArgs>(
                Command_SwitchCurrentViewModel, _ => true);
            BackstageMenuClickCommand = new AsyncRelayCommand<RoutedEventArgs>(
                Command_BackstageMenuClick, _ => CanExecute());
            WindowClosingCommand = new AsyncRelayCommand<CancelEventArgs>(
                Command_WindowClosing, _ => true);
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

            //
            // Our progress state is tied to statemanager.
            //
            m_ProgressState = new ProgressState();

            //
            // Setup StateManager for other viewmodels
            //
            StateManager.ProgressState = m_ProgressState;

            //
            // Instatiate other viewmodels. SettingsFormViewModel must be instantiated first
            // as it sets StateManager.SettingsModel which other viewmodels use.
            //
            m_SettingsFormViewModel = new SettingsFormViewModel();
            m_SettingsFormViewModel.LoadDefault();
            m_ProviderViewModel = new ProviderViewModel();
            m_SessionViewModel = new SessionViewModel();
            m_SessionFormViewModel = new SessionFormViewModel(); // lazy init

            //
            // Subscribe to property change events in session form, so when the form becomes valid,
            // the session control buttons and associated commands are available.
            //
            m_SessionFormViewModel.PropertyChanged += delegate (object? sender, PropertyChangedEventArgs args)
            {
                StartSessionCommand.NotifyCanExecuteChanged();
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
            etwlib.TraceLogger.SetLevel(m_SettingsFormViewModel.m_SettingsModel.TraceLevelEtwlib);
            EtwPilot.Utilities.TraceLogger.Initialize();
            EtwPilot.Utilities.TraceLogger.SetLevel(m_SettingsFormViewModel.m_SettingsModel.TraceLevelApp);
            symbolresolver.TraceLogger.Initialize();
            symbolresolver.TraceLogger.SetLevel(m_SettingsFormViewModel.m_SettingsModel.TraceLevelSymbolresolver);

            Trace(TraceLoggerType.MainWindow,
                  TraceEventType.Information,
                  "MainWindow opened");
        }

        private async Task Command_WindowClosing(CancelEventArgs Args)
        {
            await OnWindowClosing(Args);
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
            m_ProgressState.UpdateProgressMessage($"Loaded {m_ProviderViewModel.Providers.Count} providers");
            m_ProgressState.FinalizeProgress();
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
            m_ProgressState.UpdateProgressMessage($"Loaded manifest for provider {Id}");
            m_ProgressState.FinalizeProgress();
        }

        private async Task Command_LoadSessions()
        {
            StateManager.ProgressState.InitializeProgress(2);
            CurrentViewModel = m_SessionViewModel;
            await m_SessionViewModel.LoadSessions();
            m_ProgressState.UpdateProgressMessage($"Loaded {m_SessionViewModel.Sessions.Count} sessions");
            m_ProgressState.FinalizeProgress();
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

        private async Task Command_StartSession()
        {
            RibbonTabControlSelectedIndex = 1;

            //
            // Load the form data into a model object
            //
            var model = m_SessionFormViewModel.GetFormData();
            if (model == null)
            {
                return;
            }

            //
            // Create a new live session tab
            //
            var tabName = UiHelper.GetUniqueTabName(model.Id, "LiveSession");
            var vm = m_SessionViewModel.CreateLiveSession(model, tabName);
            Action tabClosedCallback = async () =>
            {
                //
                // If the last tab was closed, the ribbon's tabcontrol automatically
                // switches to the prior tab, which invokes Command_SwitchCurrentViewModel,
                // which could restore another view's VM. Set it back manually now for
                // the stop command.
                //
                CurrentViewModel = vm;

                //
                // Stop the live session and cleanup
                //
                await Command_StopSession();

                //
                // Hide the context tab group if no more tabs available
                //
                var ribbon = UiHelper.FindChild<Fluent.Ribbon>(
                    System.Windows.Application.Current.MainWindow, "MainWindowRibbon");
                if (ribbon == null)
                {
                    return;
                }
                if (ribbon.Tabs.Count == 3)
                {
                    LiveSessionsVisible = Visibility.Hidden;
                    ribbon.SelectedTabIndex = 1;
                }
            };
            //
            // Create a cache entry for the new live session and its tab.
            //
            var tab = UiHelper.CreateRibbonContextualTab(
                    tabName,
                    model.Name,
                    "SessionContextTabStyle",
                    "SessionContextTabText",
                    "SessionContextTabCloseButton",
                    tabClosedCallback);
            if (tab == null)
            {
                Trace(TraceLoggerType.MainWindow,
                      TraceEventType.Error,
                      $"Unable to create contextual tab {tabName}");
                return;
            }

            //
            // Add a stop button to the contextual tab, if it's a new tab.
            //
            if (!tab.Groups.Any(g => g.Name == "ControlGroup"))
            {
                var group = new RibbonGroupBox
                {
                    Name = "ControlGroup"
                };
                var icon = UiHelper.GetGlobalResource<BitmapImage>("stop");
                group.Items.Add(new Fluent.Button()
                {
                    Name = "StopTraceButton",
                    Icon = icon,
                    Command = StopSessionCommand,
                    Header = "Stop"
                });
                tab.Groups.Add(group);
            }

            //
            // Switch to and start the live session.
            //
            CurrentViewModel = vm;
            m_CurrentCommand = StartSessionCommand;
            await vm.Start();
            m_ProgressState.FinalizeProgress();
        }

        private async Task Command_StopSession()
        {
            var vm = CurrentViewModel as LiveSessionViewModel;
            if (vm == null)
            {
                return;
            }
            m_ProgressState.UpdateProgressMessage($"Live session stop requested...");
            await vm.Stop();
            m_ProgressState.UpdateProgressMessage($"Live session stopped");
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
                    m_ProgressState.UpdateProgressMessage($"Operation cancelled");
                    CancelCommandButtonVisibility = Visibility.Hidden;
                    m_ProgressState.FinalizeProgress();
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
            m_ProgressState.UpdateProgressMessage($"Exported {providers.Count} manifests to {root} ({numErrors} errors)");
            CancelCommandButtonVisibility = Visibility.Hidden;
            m_ProgressState.FinalizeProgress();
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
            m_ProgressState.UpdateProgressMessage($"Copied {providers.Count} rows to clipboard");
            m_ProgressState.FinalizeProgress();
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
            m_ProgressState.UpdateProgressMessage($"Copied {sessions.Count} rows to clipboard");
            m_ProgressState.FinalizeProgress();
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
                        if (m_SessionViewModel.HasLiveSessions())
                        {
                            LiveSessionsVisible = Visibility.Visible;
                        }
                        StartSessionCommand.NotifyCanExecuteChanged();
                        break;
                    }
                case "InsightsTab":
                    {
                        LiveSessionsVisible = Visibility.Hidden;
                        ProviderManifestVisible = Visibility.Hidden;
                        CurrentViewModel = null;
                        break;
                    }
                default:
                    {
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
                CurrentViewModel = m_ProviderViewModel.GetVmForTab(tab.Name)!;
            }
            else if (tab.Name.StartsWith("LiveSession_"))
            {
                LiveSessionsVisible = Visibility.Visible;
                CurrentViewModel = m_SessionViewModel.GetVmForTab(tab.Name)!;
                Debug.Assert(CurrentViewModel != null);
            }
        }

        private async Task Command_BackstageMenuClick(RoutedEventArgs Args)
        {
            var menuItem = Args.Source as BackstageTabItem;
            if (menuItem == null || menuItem.Name == null)
            {
                return;
            }

            //
            // Switch the content view back to the settings form
            //
            if (menuItem.Name != "MainContentMenuItem")
            {
                var tabControl = menuItem.Parent as BackstageTabControl;
                tabControl!.SelectedIndex = 0;
            }

            m_ProgressState.InitializeProgress(1);

            switch (menuItem.Name)
            {
                case "LoadSettingsMenuItem":
                    {
                        var dialog = new System.Windows.Forms.OpenFileDialog();
                        dialog.CheckFileExists = true;
                        dialog.CheckPathExists = true;
                        dialog.Multiselect = false;

                        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        {
                            break;
                        }

                        try
                        {
                            m_SettingsFormViewModel.Load(dialog.FileName);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Unable to load settings from {dialog.FileName}: {ex.Message}");
                            break;
                        }
                        m_ProgressState.UpdateProgressMessage($"Successfully loaded settings from {dialog.FileName}");
                        break;
                    }
                case "SaveSettingsMenuItem":
                    {
                        try
                        {
                            m_SettingsFormViewModel.Save();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Unable to save settings: {ex.Message}");
                            break;
                        }

                        m_ProgressState.UpdateProgressMessage($"Successfully saved settings");
                        break;
                    }
                case "SaveSettingsAsMenuItem":
                    {
                        var sfd = new System.Windows.Forms.SaveFileDialog();
                        sfd.Filter = "json files (*.json)|*.json";
                        sfd.RestoreDirectory = true;

                        if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        {
                            break;
                        }

                        try
                        {
                            m_SettingsFormViewModel.Save(sfd.FileName);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Unable to save settings: {ex.Message}");
                            break;
                        }
                        m_ProgressState.UpdateProgressMessage($"Successfully saved settings to {sfd.FileName}");
                        break;
                    }
                case "DebugLogsMenuItem":
                    {
                        var psi = new ProcessStartInfo();
                        psi.FileName = Model.SettingsModel.DefaultWorkingDirectory;
                        psi.UseShellExecute = true;
                        Process.Start(psi);
                        break;
                    }
                case "ExitMenuItem":
                    {
                        await OnWindowClosing(new CancelEventArgs());
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
            m_ProgressState.FinalizeProgress();
        }

        private async Task OnWindowClosing(CancelEventArgs Args)
        {
            await m_SessionViewModel.StopAllLiveSessions();
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
            if (m_CurrentCommand == null ||
                !m_CurrentCommand.IsRunning ||
                m_CurrentCommand != StartSessionCommand)
            {
                return false;
            }
            return true;
        }
    }
}
