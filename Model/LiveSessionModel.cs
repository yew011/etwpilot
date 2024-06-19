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
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;

namespace EtwPilot.Model
{
    public enum StopCondition
    {
        None,
        SizeMb,
        TimeSec,
        Max
    }

    internal class LiveSessionModel
    {
        //
        // Input fields from form.
        //
        public string Name { get; set; }
        public bool IsRealTime { get; set; }
        public string LogLocation { get; set; }
        public StopCondition StopCondition { get; set; }
        public int StopConditionValue { get; set; }
        public List<EnabledProvider> Providers { get; set; }
        public Dictionary<string, Type> Columns { get; set; }
        //
        // Output & tracking fields
        //
        public int EventsConsumed { get; set; }
        public uint BytesConsumed { get; set; }
        public Stopwatch Stopwatch { get; set; }
        public bool IsStopping { get; set; }
        public DataTable LiveSessionData { get; set; }

        private CancellationTokenSource CancellationSource;
        private AutoResetEvent TaskCompletedEvent;
        private bool IsRunning;

        public LiveSessionModel()
        {
            Providers = new List<EnabledProvider>();
            Columns = new Dictionary<string, Type>();
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new Exception("Session name is required.");
            }
            if (StopCondition > StopCondition.Max)
            {
                throw new Exception("Invalid stop condition.");
            }
            if (!IsRealTime && string.IsNullOrEmpty(LogLocation))
            {
                throw new Exception("Location required for log-based traces.");
            }
            if (Providers.Count == 0)
            {
                throw new Exception("At least one enabled provider is required.");
            }
            if (Columns.Count == 0)
            {
                throw new Exception("At least one column is required.");
            }
        }
    }

    public class EtwColumn
    {
        public bool Visible { get; set; }
        public string Name { get; set; }
        public Type Type { get; set; }

        public static ObservableCollection<EtwColumn> GetDefaultColumns()
        {
            return new ObservableCollection<EtwColumn> {
                new EtwColumn() { Visible=true, Name="Provider", Type=typeof(string) },
                new EtwColumn() { Visible=true, Name="EventId", Type=typeof(int) },
                new EtwColumn() { Visible=true, Name="Version", Type=typeof(int) },
                new EtwColumn() { Visible=true, Name="Level", Type=typeof(string) },
                new EtwColumn() { Visible=true, Name="Channel", Type=typeof(string) },
                new EtwColumn() { Visible=true, Name="Keywords", Type=typeof(string) },
                new EtwColumn() { Visible=true, Name="Task", Type=typeof(string) },
                new EtwColumn() { Visible=true, Name="Opcode", Type=typeof(string) },
                new EtwColumn() { Visible=true, Name="ProcessId", Type=typeof(int) },
                new EtwColumn() { Visible=true, Name="ThreadId", Type=typeof(string) },
                new EtwColumn() { Visible=true, Name="UserSid", Type=typeof(string) },
                new EtwColumn() { Visible=true, Name="ActivityId", Type=typeof(Guid) },
                new EtwColumn() { Visible=true, Name="Timestamp", Type=typeof(string) },
            };
        }
    }
}
