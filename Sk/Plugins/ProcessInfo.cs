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
using EtwPilot.Utilities;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Newtonsoft.Json;

namespace EtwPilot.Sk.Plugins
{
    internal class ProcessInfo
    {
        private SystemInfo m_Info;

        public ProcessInfo()
        {
            m_Info = new SystemInfo();
        }

        [KernelFunction("GetProcessByName")]
        [Description("Get details of a process running on the system")]
        public async Task<string?> GetProcessByName(string Name)
        {
            if (m_Info.ActiveProcessList.Count == 0)
            {
                await m_Info.Refresh();
            }
            var obj = m_Info.ActiveProcessList.FirstOrDefault(
                p => p.Name.ToLower().Contains(Name.ToLower()));
            if (obj == default)
            {
                return $"No details available for process named {Name}";
            }
            return JsonConvert.SerializeObject(obj);
        }

        [KernelFunction("GetProcessById")]
        [Description("Get details of a process running on the system")]
        public async Task<string?> GetProcessById(int ProcessId)
        {
            if (m_Info.ActiveProcessList.Count == 0)
            {
                await m_Info.Refresh();
            }
            var obj = m_Info.ActiveProcessList.FirstOrDefault(p => p.Pid == ProcessId);
            if (obj == default)
            {
                return $"No details available for process with ID {ProcessId}";
            }
            return JsonConvert.SerializeObject(obj);
        }
    }
}
