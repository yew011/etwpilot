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

        public bool HasLiveSessions()
        {
            return LiveSessionCache.Count > 0;
        }

        public LiveSessionViewModel CreateLiveSession(SessionFormModel Model, string TabName)
        {
            var vm = new LiveSessionViewModel(Model);
            Debug.Assert(!LiveSessionCache.ContainsKey(TabName));
            LiveSessionCache.Add(TabName, vm);
            return vm;
        }

        public async Task StopAllLiveSessions()
        {
            var total = LiveSessionCache.Count;
            var i = 1;
            foreach (var kvp in LiveSessionCache)
            {
                var vm = kvp.Value;
                StateManager.ProgressState.UpdateProgressMessage(
                    $"Please wait, stopping live session {i++} of {total}...");
                await vm.Stop();
            }
        }

        public LiveSessionViewModel? GetVmForTab(string TabName)
        {
            Debug.Assert(LiveSessionCache.ContainsKey(TabName));
            if (LiveSessionCache.ContainsKey(TabName))
            {
                return LiveSessionCache[TabName];
            }
            return null;
        }
    }
}
