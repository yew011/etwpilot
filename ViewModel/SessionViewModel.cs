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
using EtwPilot.Model;
using EtwPilot.Utilities;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.DataExporter;
    using static EtwPilot.Utilities.TraceLogger;

    public class SessionViewModel : ViewModelBase
    {
        #region observable properties

        private ObservableCollection<ParsedEtwSession> _sessions;
        public ObservableCollection<ParsedEtwSession> Sessions
        {
            get => _sessions;
            set
            {
                if (_sessions != value)
                {
                    _sessions = value;
                    OnPropertyChanged("Sessions");
                }
            }
        }

        //
        // This is required in order to have something to bind to LiveSessionView.xaml.
        // It is updated whenever the selected contextual tab changes for the live
        // session, from MainWindowViewModel, which invokes SessionViewModel's
        // ShowSessionCommand, which calls the appropriate LiveSessionViewModel's
        // ViewModelActivated().
        //
        private LiveSessionViewModel _CurrentLiveSession;
        public LiveSessionViewModel CurrentLiveSession
        {
            get => _CurrentLiveSession;
            set
            {
                if (_CurrentLiveSession != value)
                {
                    _CurrentLiveSession = value;
                    OnPropertyChanged("CurrentLiveSession");
                }
            }
        }

        #endregion

        #region commands

        public AsyncRelayCommand LoadSessionsCommand { get; set; }
        public RelayCommand NewSessionCommand { get; set; }
        public AsyncRelayCommand StartSessionCommand { get; set; }
        public AsyncRelayCommand<LiveSessionViewModel> ShowSessionCommand { get; set; }
        public AsyncRelayCommand StopAllSessionsCommand { get; set; }
        public AsyncRelayCommand<LiveSessionViewModel> CloseDynamicTab { get; set; }

        #endregion

        public List<ParsedEtwSession> SelectedSessions;
        private SessionModel Model;
        public ObservableCollection<LiveSessionViewModel> LiveSessions;

        public SessionViewModel() : base()
        {
            Sessions = new ObservableCollection<ParsedEtwSession>();
            Model = new SessionModel();
            SelectedSessions = new List<ParsedEtwSession>();
            LiveSessions = new ObservableCollection<LiveSessionViewModel>();

            LoadSessionsCommand = new AsyncRelayCommand(Command_LoadSessions, () => { return true; });
            NewSessionCommand = new RelayCommand(
                Command_NewSession, () => { return true; });
            StartSessionCommand = new AsyncRelayCommand(
                Command_StartSession, CanExecuteStartSession);
            StopAllSessionsCommand = new AsyncRelayCommand(
                Command_StopAllSessions, CanExecuteStopAllSessions);
            ShowSessionCommand = new AsyncRelayCommand<LiveSessionViewModel>(
                Command_ShowSession, _ => true);
            CloseDynamicTab = new AsyncRelayCommand<LiveSessionViewModel>(
                Command_CloseDynamicTab, _ => true);
        }

        public override async Task ViewModelActivated()
        {
            if (Sessions.Count == 0)
            {
                await LoadSessionsCommand.ExecuteAsync(null);
            }
            GlobalStateViewModel.Instance.CurrentViewModel = this;
        }

        private async Task Command_LoadSessions()
        {
            using var progressContext = ProgressState.CreateProgressContext(1, $"");
            var sessions = await Model.GetSessions();
            if (sessions == null)
            {
                ProgressState.FinalizeProgress($"No sessions available");
                return;
            }
            ProgressState.FinalizeProgress($"Loaded {sessions.Count} sessions", Sticky: true);
            Sessions.Clear();
            sessions.ForEach(s => Sessions.Add(s));
        }

        private void Command_NewSession()
        {
            var g_MainWindowVm = UiHelper.GetGlobalResource<MainWindowViewModel>(
                "g_MainWindowViewModel");
            if (g_MainWindowVm == null)
            {
                return;
            }
            g_MainWindowVm.RibbonTabControlSelectedIndex = 1;
        }

        public async Task Command_ShowSession(LiveSessionViewModel? LiveSession)
        {
            if (LiveSession == null)
            {
                Debug.Assert(false);
                return;
            }
            //
            // Note: In the StartLiveSession workflow, the VM is activated before the
            // tab has been created, so it won't show in the cache here!
            //
            var livesession = LiveSessions.FirstOrDefault(ls => ls.Configuration.Id == LiveSession.Configuration.Id);
            if (livesession != default)
            {
                await livesession.ViewModelActivated();
            }
            NotifyCanExecuteChanged();
        }

        private async Task Command_StartSession()
        {
            //
            // Load the form data into a model object
            //
            var model = GlobalStateViewModel.Instance.g_SessionFormViewModel.GetFormData();
            if (model == null)
            {
                return;
            }

            //
            // Create a new live session view model that represents this new trace session.
            // CurrentViewModel must be updated now before the session tab is created,
            // hence we must do this outside of session view model.
            //
            var vm = new LiveSessionViewModel(
                model,
                () => {
                    NotifyCanExecuteChanged();
                },
                () => {
                    NotifyCanExecuteChanged();
                });

            if (!await vm.Initialize())
            {
                ProgressState.EphemeralStatusText = "Live session failed to initialize";
                return;
            }

            if (!vm.StartCommand.CanExecute(null))
            {
                //
                // If this happens, we forgot to do something here.
                //
                Debug.Assert(false);
                ProgressState.EphemeralStatusText = "Live session cannot be started";
                return;
            }

            //
            // Create a UI contextual tab for this live session.
            //
            var tabName = UiHelper.GetUniqueTabName(vm.Configuration.Id, "LiveSession");
            if (!UiHelper.CreateRibbonContextualTab(
                    tabName,
                    vm.Configuration.Name,
                    1,
                    new Dictionary<string, List<string>>() {
                        { "Control", new List<string> { "LiveSessionStopButtonStyle" } },
                        { "Actions", new List<string> { "LiveSessionInsightsButtonStyle" } },
                    },
                    vm))
            {
                Trace(TraceLoggerType.LiveSession,
                      TraceEventType.Error,
                      $"Unable to create contextual tab {tabName}");
                return;
            }

            LiveSessions.Add(vm);

            //
            // Display the VM
            //
            await vm.ViewModelActivated();

            //
            // Disable CS4014 - we don't want to await this call, because it doesn't return
            // until the LiveSession is stopped...
            //
#pragma warning disable CS4014
            vm.StartCommand.ExecuteAsync(null);
#pragma warning restore CS4014
        }

        private async Task<bool> Command_StopAllSessions()
        {
            var total = LiveSessions.Count;
            var i = 1;
            using var progressContext = ProgressState.CreateProgressContext(total, $"");
            foreach (var session in LiveSessions)
            {
                ProgressState.UpdateProgressMessage(
                    $"Please wait, stopping live session {i++} of {total}...");
                if (!session.IsRunning() || session.IsStopping())
                {
                    continue;
                }
                ProgressState.UpdateProgressMessage($"Live session stop requested...");
                await session.StopCommand.ExecuteAsync(null);
                ProgressState.UpdateProgressMessage($"Live session stopped");
                ProgressState.UpdateProgressValue();
            }
            ProgressState.FinalizeProgress("All live sessions stopped.", Sticky: true);
            NotifyCanExecuteChanged();
            return true;
        }

        public async Task Command_CloseDynamicTab(LiveSessionViewModel? ViewModel)
        {
            if (ViewModel == null)
            {
                Debug.Assert(false);
                return;
            }
            //
            // Stop the live session if necessary and remove it from our list.
            //
            if (ViewModel.StopCommand.CanExecute(null))
            {
                await ViewModel.StopCommand.ExecuteAsync(null);
            }
            var result = LiveSessions.Remove(ViewModel);
            Debug.Assert(result);

            //
            // Remove the contextual tab from the main ribbon tab control.
            //
            var tabName = UiHelper.GetUniqueTabName(ViewModel.Configuration.Id, "LiveSession");
            if (!UiHelper.RemoveRibbonContextualTab(tabName))
            {
                Debug.Assert(false);
                return;
            }
        }

        protected override async Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
        {
            using var progressContext = ProgressState.CreateProgressContext(1, $"Exporting data...");
            List<ParsedEtwSession> sessions = SelectedSessions;
            if (sessions == null || sessions.Count == 0)
            {
                if (Sessions == null || Sessions.Count == 0)
                {
                    ProgressState.FinalizeProgress("No sessions available for export.");
                    return;
                }
                sessions = Sessions.ToList();
            }

            try
            {
                (int, string?) result;
                if (Format == ExportFormat.Csv)
                {
                    //
                    // ParsedEtwSession has a list field that would not make sense for CSV.
                    // So just stringify each object and pass that for export.
                    //
                    var lines = new List<string>();
                    foreach (var session in sessions)
                    {
                        lines.Add(session.ToString());
                    }
                    result = await DataExporter.Export<List<string>>(
                        lines, ExportFormat.Custom, "Sessions",Token);
                }
                else
                {
                    result = await DataExporter.Export<List<ParsedEtwSession>>(
                        sessions, Format, "Sessions", Token);
                }
                if (result.Item1 == 0 || result.Item2 == null)
                {
                    return;
                }
                ProgressState.FinalizeProgress($"Exported {result.Item1} records to {result.Item2}", Sticky: true);
                if (Format != DataExporter.ExportFormat.Clip)
                {
                    ProgressState.SetFollowupActionCommand.Execute(
                        new FollowupAction()
                        {
                            Title = "Open",
                            Callback = new Action<dynamic>((args) =>
                            {
                                var psi = new ProcessStartInfo();
                                psi.FileName = result.Item2;
                                psi.UseShellExecute = true;
                                Process.Start(psi);
                            }),
                            CallbackArgument = null
                        });
                }
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"ExportData failed: {ex.Message}", Sticky: true);
            }
        }

        public bool HasLiveSessions() => LiveSessions.Count > 0;
        public bool HasActiveLiveSessions() => LiveSessions.Any(
            s => s.IsRunning() && !s.IsStopping());
        public LiveSessionViewModel GetMostRecentLiveSession() => LiveSessions.Where(
            s => s.IsRunning() && !s.IsStopping()).OrderByDescending(s => s.StartTime).First();

        public void NotifyCanExecuteChanged()
        {
            StartSessionCommand.NotifyCanExecuteChanged();
            StopAllSessionsCommand.NotifyCanExecuteChanged();
        }

        private bool CanExecuteStartSession()
        {
            return !GlobalStateViewModel.Instance.g_SessionFormViewModel.HasErrors &&
                !GlobalStateViewModel.Instance.Settings.HasErrors;
        }

        private bool CanExecuteStopAllSessions()
        {
            return GlobalStateViewModel.Instance.g_SessionViewModel.HasActiveLiveSessions();
        }
    }
}
