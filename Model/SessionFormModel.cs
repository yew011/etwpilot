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
using EtwPilot.ViewModel;

namespace EtwPilot.Model
{
    using StopCondition = ViewModel.LiveSessionViewModel.StopCondition;
    using static EtwPilot.Utilities.TraceLogger;

    public class ConfiguredProvider
    {
        public EnabledProvider _EnabledProvider { get; set; }
        public List<EtwColumnViewModel> Columns { get; set; }
    }

    public class SessionFormModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsRealTime { get; set; }
        public string LogLocation { get; set; }
        public StopCondition StopCondition { get; set; }
        public int StopConditionValue { get; set; }
        public List<ConfiguredProvider> ConfiguredProviders { get; set; }

        public SessionFormModel()
        {
            ConfiguredProviders = new List<ConfiguredProvider>();
            Id = Guid.NewGuid();
        }
    }
}
