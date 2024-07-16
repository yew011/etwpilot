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
        // validated form
        //
        public SessionFormModel SessionFormModel { get; set; }

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

        public LiveSessionModel(SessionFormModel sessionFormModel)
        {
            SessionFormModel = sessionFormModel;
        }
    }
}
