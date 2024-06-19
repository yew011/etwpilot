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
using System.IO;
using Newtonsoft.Json;
using EtwPilot.ViewModel;

namespace EtwPilot.Model
{
    using static EtwPilot.Utilities.TraceLogger;

    internal class ProviderModel
    {
        private readonly StateManager StateManager;

        public ProviderModel(StateManager Manager)
        {
            StateManager = Manager;
        }

        public async Task<List<ParsedEtwProvider>?> GetProviders()
        {
            try
            {
                return await LoadFromCache(false);
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.Providers,
                      TraceEventType.Error,
                      $"Failed to retrieve providers: {ex.Message}");
            }
            return null;
        }

        public async Task<ParsedEtwManifest?> GetProviderManifest(Guid Id)
        {
            var result = await Task.Run(() =>
            {
                try
                {
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
            return result;
        }

        private async Task<List<ParsedEtwProvider>?> LoadFromCache(bool ForceRefresh = false)
        {
            var cache = StateManager.SettingsModel.ProviderCacheLocation;
            Debug.Assert(!string.IsNullOrEmpty(cache));

            if (!File.Exists(cache) || ForceRefresh)
            {
                StateManager.ProgressState.UpdateProgressMessage("Querying providers...");
                var providers = await QueryProviders();
                if (providers == null)
                {
                    return null;
                }

                //
                // Write the cache now.
                //
                try
                {
                    StateManager.ProgressState.UpdateProgressMessage("Writing cache...");
                    var json = JsonConvert.SerializeObject(providers, Formatting.Indented);
                    File.WriteAllText(cache, json);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Could not serialize the provider objects " +
                        $"to JSON: {ex.Message}");
                }
                return providers;
            }
            else
            {
                StateManager.ProgressState.UpdateProgressMessage("Reading cache...");
                //
                // Read from the cache
                //
                try
                {
                    var json = File.ReadAllText(cache);
                    var result = (List<ParsedEtwProvider>)JsonConvert.DeserializeObject(
                        json, typeof(List<ParsedEtwProvider>))!;
                    return result;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Could not deserialize provider data: {ex.Message}");
                }
            }
        }

        private async Task<List<ParsedEtwProvider>?> QueryProviders()
        {
            try
            {
                return await Task<List<ParsedEtwProvider>>.Run(() =>
                {
                    var providers = ProviderParser.GetProviders();
                    if (providers == null || providers.Count == 0)
                    {
                        Trace(TraceLoggerType.Providers,
                              TraceEventType.Error,
                              "No ETW providers found");
                        return null;
                    }
                    return providers;
                });
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.Providers,
                      TraceEventType.Critical,
                      $"Exception occurred while retrieving provider list: {ex.Message}");
            }
            return null;
        }
    }
}
