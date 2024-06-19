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
using System.Diagnostics;
using EtwPilot.ViewModel;

namespace EtwPilot.Model
{
    using static EtwPilot.Utilities.TraceLogger;

    public class ProviderManifestModel
    {
        private ProgressState ProgressState;

        public ProviderManifestModel(ProgressState Progress)
        {
            ProgressState = Progress;
        }

        public async Task<ParsedEtwManifest?> GetProviderManifest(Guid Id)
        {
            ProgressState.InitializeProgress(2);
            var result = await Task.Run(() =>
            {
                ProgressState.UpdateProgressMessage("Loading provider manifest...");
                try
                {
                    ProgressState.UpdateProgressValue();
                    return ProviderParser.GetManifest(Id);
                }
                catch (Exception ex)
                {
                    Trace(TraceLoggerType.Providers,
                          TraceEventType.Error,
                          $"Unable to retrieve manifest for {Id}: {ex.Message}");
                }
                return null;
            });
            ProgressState.FinalizeProgress();
            return result;
        }
    }
}
