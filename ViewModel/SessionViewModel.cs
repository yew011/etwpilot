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
using EtwPilot.Model;
using EtwPilot.Utilities;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;

    internal class SessionViewModel : ViewModelBase
    {
        public List<ParsedEtwSession> SelectedSessions;
        private SessionModel Model;
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

        private Dictionary<string, LiveSessionViewModel> LiveSessionCache;

        public SessionViewModel()
        {
            Sessions = new ObservableCollection<ParsedEtwSession>();
            Model = new SessionModel(StateManager);
            SelectedSessions = new List<ParsedEtwSession>();
            LiveSessionCache = new Dictionary<string, LiveSessionViewModel>();
        }

        public async Task LoadSessions()
        {
            var sessions = await Model.GetSessions();
            if (sessions == null)
            {
                return;
            }
            Sessions.Clear();
            sessions.ForEach(s => Sessions.Add(s));
        }

        public bool HasLiveSessions() => LiveSessionCache.Count > 0;
        public bool HasActiveLiveSessions() => LiveSessionCache.Values.Any(
            s => s.IsRunning() && !s.IsStopping());

        public async Task<bool> StartLiveSession(LiveSessionViewModel LiveSession)
        {
            //
            // Create a UI contextual tab for this live session
            //
            Func<Task<bool>> tabClosedCallback = async delegate ()
            {
                //
                // Stop the live session and cleanup
                //
                var result = await StopLiveSession(LiveSession);
                if (!result)
                {
                    return false;
                }

                //
                // Locate the corresponding tab name from our livesession cache
                // and remove the entry from the cache
                //
                var tabName = LiveSessionCache.Where(kvp => kvp.Value == LiveSession)?.Select(
                    kvp => kvp.Key).FirstOrDefault();
                if (tabName == null)
                {
                    Debug.Assert(false);
                    return false;
                }

                RemoveLiveSession(tabName);
                return true;
            };
            var tabName = UiHelper.GetUniqueTabName(LiveSession.Configuration.Id, "LiveSession");
            var tab = UiHelper.CreateRibbonContextualTab(
                    tabName,
                    LiveSession.Configuration.Name,
                    1,
                    new Dictionary<string, List<string>>() {
                        { "Control", new List<string> { "LiveSessionStopButtonStyle" } },
                        { "Actions", new List<string> { "LiveSessionInsightsButtonStyle" } },
                        { "Export", new List<string> { 
                            "LiveSessionExportJSONButtonStyle",
                            "LiveSessionExportCSVButtonStyle",
                            "LiveSessionExportXMLButtonStyle",
                            "LiveSessionExportClipboardButtonStyle",
                        } },
                    },
                    "SessionContextTabStyle",
                    "SessionContextTabText",
                    "SessionContextTabCloseButton",
                    tabClosedCallback);
            if (tab == null)
            {
                Trace(TraceLoggerType.LiveSession,
                      TraceEventType.Error,
                      $"Unable to create contextual tab {tabName}");
                return false;
            }
            AddLiveSession(LiveSession, tabName);

            //
            // Start the live session.
            //
            var success = await LiveSession.Start();
            return success;
        }

        public async Task<bool> StopLiveSession(LiveSessionViewModel LiveSession)
        {
            if (!LiveSession.IsRunning())
            {
                return true;
            }
            if (LiveSession.IsStopping())
            {
                StateManager.ProgressState.UpdateProgressMessage($"Stop in progress, please wait...");
                return false;
            }
            StateManager.ProgressState.UpdateProgressMessage($"Live session stop requested...");
            var result = await LiveSession.Stop();
            if (!result)
            {
                return result;
            }
            StateManager.ProgressState.UpdateProgressMessage($"Live session stopped");
            return true;
        }

        public void AddLiveSession(LiveSessionViewModel LiveSession, string TabName)
        {
            Debug.Assert(!LiveSessionCache.ContainsKey(TabName));
            LiveSessionCache.Add(TabName, LiveSession);
        }

        public void RemoveLiveSession(string TabName)
        {
            Debug.Assert(LiveSessionCache.ContainsKey(TabName));
            LiveSessionCache.Remove(TabName);
        }

        public async Task<bool> StopAllLiveSessions()
        {
            var total = LiveSessionCache.Count;
            var i = 1;
            foreach (var kvp in LiveSessionCache)
            {
                var vm = kvp.Value;
                StateManager.ProgressState.UpdateProgressMessage(
                    $"Please wait, stopping live session {i++} of {total}...");
                if (!await StopLiveSession(vm))
                {
                    return false;
                }
            }
            return true;
        }

        public async Task<bool> ShutdownAllLiveSessions()
        {
            var total = LiveSessionCache.Count;
            var i = 1;
            foreach (var kvp in LiveSessionCache)
            {
                var vm = kvp.Value;
                StateManager.ProgressState.UpdateProgressMessage(
                    $"Please wait, stopping live session {i++} of {total}...");
                if (!await StopLiveSession(vm))
                {
                    return false;
                }
            }
            LiveSessionCache.Clear();
            return true;
        }

        public LiveSessionViewModel? GetVmForTab(string TabName)
        {
            if (LiveSessionCache.ContainsKey(TabName))
            {
                return LiveSessionCache[TabName];
            }
            return null;
        }
    }
}
