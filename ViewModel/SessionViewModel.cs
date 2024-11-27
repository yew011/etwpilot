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

        #endregion

        #region commands

        public AsyncRelayCommand LoadSessionsCommand { get; set; }
        public AsyncRelayCommand NewSessionCommand { get; set; }
        public AsyncRelayCommand StartSessionCommand { get; set; }
        public AsyncRelayCommand StopAllSessionsCommand { get; set; }

        #endregion

        public List<ParsedEtwSession> SelectedSessions;
        private SessionModel Model;
        private Dictionary<string, LiveSessionViewModel> LiveSessionCache;

        public SessionViewModel() : base()
        {
            Sessions = new ObservableCollection<ParsedEtwSession>();
            Model = new SessionModel();
            SelectedSessions = new List<ParsedEtwSession>();
            LiveSessionCache = new Dictionary<string, LiveSessionViewModel>();

            LoadSessionsCommand = new AsyncRelayCommand(Command_LoadSessions, () => { return true; });
            NewSessionCommand = new AsyncRelayCommand(
                Command_NewSession, () => { return true; });
            StartSessionCommand = new AsyncRelayCommand(
                Command_StartSession, CanExecuteStartSession);
            StopAllSessionsCommand = new AsyncRelayCommand(
                Command_StopAllSessions, CanExecuteStopAllSessions);
        }

        public override async Task ViewModelActivated()
        {
            if (Sessions.Count == 0)
            {
                await LoadSessionsCommand.ExecuteAsync(null);
            }
            GlobalStateViewModel.Instance.CurrentViewModel = this;
        }

        public async Task ActivateLiveSessionViewModel(string TabName)
        {
            //
            // Note: In the StartLiveSession workflow, the VM is activated before the
            // tab has been created, so it won't show in the cache here!
            //
            if (LiveSessionCache.ContainsKey(TabName))
            {
                await LiveSessionCache[TabName].ViewModelActivated();
            }
            NotifyCanExecuteChanged();
        }

        private async Task Command_LoadSessions()
        {
            GlobalStateViewModel.Instance.CurrentViewModel = this;
            ProgressState.InitializeProgress(1);
            var sessions = await Model.GetSessions();
            if (sessions == null)
            {
                ProgressState.FinalizeProgress($"No sessions available");
                return;
            }
            ProgressState.FinalizeProgress($"Loaded {sessions.Count} sessions");
            Sessions.Clear();
            sessions.ForEach(s => Sessions.Add(s));
        }

        private async Task Command_NewSession()
        {
            var g_MainWindowVm = UiHelper.GetGlobalResource<MainWindowViewModel>(
                "g_MainWindowViewModel");
            if (g_MainWindowVm == null)
            {
                return;
            }
            g_MainWindowVm.RibbonTabControlSelectedIndex = 1;
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
            await vm.ViewModelActivated();


            //
            // Create a UI contextual tab for this live session
            //
            Func<string, Task<bool>> tabClosedCallback = async delegate (string TabName)
            {
                var result = await StopLiveSession(vm);
                if (!result)
                {
                    return false;
                }
                LiveSessionCache.Remove(TabName);
                return true;
            };
            var tabName = UiHelper.GetUniqueTabName(vm.Configuration.Id, "LiveSession");
            var tab = UiHelper.CreateRibbonContextualTab(
                    tabName,
                    vm.Configuration.Name,
                    1,
                    new Dictionary<string, List<string>>() {
                        { "Control", new List<string> { "LiveSessionStopButtonStyle" } },
                        { "Actions", new List<string> { "LiveSessionInsightsButtonStyle" } },
                        { "Export", new List<string> {
                            "ExportJSONButtonStyle",
                            "ExportCSVButtonStyle",
                            "ExportXMLButtonStyle",
                            "ExportClipboardButtonStyle",
                        } },
                    },
                    "SessionContextTabStyle",
                    "SessionContextTabText",
                    "SessionContextTabCloseButton",
                    vm,
                    tabClosedCallback);
            if (tab == null)
            {
                Trace(TraceLoggerType.LiveSession,
                      TraceEventType.Error,
                      $"Unable to create contextual tab {tabName}");
                return;
            }

            Debug.Assert(!LiveSessionCache.ContainsKey(tabName));
            LiveSessionCache.Add(tabName, vm);
            //
            // Disable CS4014 - we don't want to await this call, because it doesn't return
            // until the LiveSession is stopped...
            //
#pragma warning disable CS4014
            vm.StartLiveSessionCommand.ExecuteAsync(null);
#pragma warning restore CS4014
        }

        private async Task<bool> Command_StopAllSessions()
        {
            var total = LiveSessionCache.Count;
            var i = 1;
            ProgressState.InitializeProgress(total);
            foreach (var kvp in LiveSessionCache)
            {
                var vm = kvp.Value;
                ProgressState.UpdateProgressMessage(
                    $"Please wait, stopping live session {i++} of {total}...");
                if (!await StopLiveSession(vm))
                {
                    return false;
                }
                ProgressState.UpdateProgressValue();
            }
            ProgressState.FinalizeProgress("All live sessions stopped.");
            NotifyCanExecuteChanged();
            return true;
        }

        protected override async Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
        {
            ProgressState.InitializeProgress(1);
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
                ProgressState.FinalizeProgress($"Exported {result.Item1} records to {result.Item2}");
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"ExportData failed: {ex.Message}");
            }
        }

        public bool HasLiveSessions() => LiveSessionCache.Count > 0;
        public bool HasActiveLiveSessions() => LiveSessionCache.Values.Any(
            s => s.IsRunning() && !s.IsStopping());
        public LiveSessionViewModel GetMostRecentLiveSession() => LiveSessionCache.Values.Where(
            s => s.IsRunning() && !s.IsStopping()).OrderByDescending(s => s.StartTime).First();

        private async Task<bool> StopLiveSession(LiveSessionViewModel LiveSession)
        {
            if (!LiveSession.IsRunning())
            {
                return true;
            }
            if (LiveSession.IsStopping())
            {
                ProgressState.UpdateProgressMessage($"Stop in progress, please wait...");
                return false;
            }
            ProgressState.UpdateProgressMessage($"Live session stop requested...");
            await LiveSession.StopLiveSessionCommand.ExecuteAsync(null);
            ProgressState.UpdateProgressMessage($"Live session stopped");
            ProgressState.FinalizeProgress(null);
            return true;
        }

        public void NotifyCanExecuteChanged()
        {
            StartSessionCommand.NotifyCanExecuteChanged();
            StopAllSessionsCommand.NotifyCanExecuteChanged();
        }

        private bool CanExecuteStartSession()
        {
            return !GlobalStateViewModel.Instance.g_SessionFormViewModel.HasErrors;
        }

        private bool CanExecuteStopAllSessions()
        {
            return GlobalStateViewModel.Instance.g_SessionViewModel.HasActiveLiveSessions();
        }
    }
}
