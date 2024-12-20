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
using EtwPilot.ViewModel;
using symbolresolver;
using System.Diagnostics;
using System.Text;

namespace EtwPilot.Utilities
{
    public class StackwalkHelper
    {
        private SymbolResolver m_SymbolResolver;
        private bool m_Initialized;

        public StackwalkHelper()
        {
        }

        public async Task<bool> Initialize()
        {
            if (m_Initialized)
            {
                return true;
            }
            m_SymbolResolver = new SymbolResolver(
                GlobalStateViewModel.Instance.Settings.SymbolPath,
                GlobalStateViewModel.Instance.Settings.DbghelpPath);
            if (!await m_SymbolResolver.Initialize())
            {
                TraceLogger.Trace(
                    TraceLogger.TraceLoggerType.SymbolResolver,
                    TraceEventType.Error,
                    $"Unable to initialize SymbolResolver");
                return false;
            }
            m_Initialized = true;
            return true;
        }

        public async Task ResolveAddresses(int ProcessId, List<ulong> Addresses, StringBuilder Result)
        {
            if (!m_Initialized)
            {
                return;
            }
            Result.Clear();
            foreach (var address in Addresses)
            {
                var resolved = await m_SymbolResolver.ResolveUserAddress(
                    ProcessId, address, SymbolFormattingOption.SymbolAndModule);
                if (!string.IsNullOrEmpty(resolved))
                {
                    Result.AppendLine(resolved);
                }
            }
        }
    }
}
