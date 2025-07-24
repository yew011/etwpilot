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
using EtwPilot.Sk.Vector;
using EtwPilot.Utilities;
using EtwPilot.ViewModel;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace EtwPilot.Sk.Plugins
{
    using static EtwPilot.Utilities.TraceLogger;

    public class ProviderManifestSearchArguments
    {
        public string? ProviderName;
        public string? ProviderId;
        public string? ProviderKeywords;

        public override string ToString()
        {
            return $"ProviderName: '{ProviderName ?? "<null>"}', ProviderId: '{ProviderId ?? "<null>"}', ProviderKeywords: '{ProviderKeywords ?? "<null>"}'";
        }
    }

    internal class EtwAnalysis
    {
        private static int MAX_PROVIDERS = 50;
        private static readonly Dictionary<string, string> s_FollowupActions = new Dictionary<string, string>()
        {
            { "tell_general_error",
                $$"""
                    {
                        "status": "failed",
                        "message": "The operation failed: {0}.",
                        "next_step": "Do nothing."
                    }
                """
            },
            { "tell_no_matching_providers",
                $$"""
                    {
                        "status": "failed",
                        "message": "The supplied arguments '{0}' did not match any ETW provider manifests.",
                        "next_step": "Either try different keywords or ask the user for help, then try to start the ETW analysis agent again."
                    }
                """
            },
            { "ask_to_pick_providers",
                $$"""
                    {
                        "status": "success",
                        "message": "Here are {0} providers to choose from. Please follow system prompt instructions. Provider manifests: {1}",
                        "next_step": "Create exemplar events for a vector similarity search against realtime events and pass them to 
                        the appropriate tool."
                    }
                """
            },
            { "tell_etw_trace_no_events",
                $$"""
                    {
                        "status": "failed",
                        "message": "The requested ETW trace captured no events",
                        "next_step": "Either change the parameters of the trace and invoke this tool again to try to capture more ETW events, 
                        or invoke this tool again with the 'ProviderNamesOrIds' parameter set to null to search for exemplar events in the vector database."
                    }
                """
            },
            { "tell_no_exemplar_event_matches",
                $$"""
                    {
                        "status": "failed",
                        "message": "No realtime ETW events matched the given exemplar events",
                        "next_step": "Invoke the tool again with different values in the exemplar events or ask the user for guidance"
                    }
                """
            },
            { "ask_perform_realtime_event_analysis",
                $$"""
                    {
                        "status": "success",
                        "message": "The exemplar events had {0} matches: {1}",
                        "next_step": "Complete the analysis objective using the provided event matches."
                    }
                """
            },
        };

        public EtwAnalysis()
        {
        }

        [KernelFunction("GetMatchingProviderManifests")]
        [Description("Searches for ETW provider manifests.")]
        public async Task<string> GetMatchingProviderManifests(
            [Description("Provider matching criteria")] ProviderManifestSearchArguments Arguments,
            CancellationToken Token
            )
        {
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var progress = GlobalStateViewModel.Instance.g_InsightsViewModel.ProgressState;
            var inferenceService = kernel.GetRequiredService<InferenceService>();
            var vecDb = kernel.GetRequiredService<EtwVectorDbService>();
            if (!vecDb.m_Initialized)
            {
                return string.Format(s_FollowupActions["tell_general_error"], "Vector DB is not initialized");
            }

            if (!await LoadProviderManifests(Token))
            {
                return string.Format(s_FollowupActions["tell_general_error"], "Unable to locate any ETW providers");
            }

            //
            // Get all provider manifests in the vector database
            //
            var results = await vecDb.GetAsync<EtwProviderManifestRecord>(Token);
            if (results == null || results.Count == 0)
            {
                return string.Format(s_FollowupActions["tell_general_error"],
                    "No ETW provider manifests found in the vector database");
            }

            var filtered = results.Where(r => EtwProviderManifestPostFilter(r, Arguments)).Take(MAX_PROVIDERS);
            if (filtered.Count() == 0)
            {
                //
                // The model should call the plugin again with different parameters.
                //
                return string.Format(s_FollowupActions["tell_no_matching_providers"], $"{Arguments}");
            }

            //
            // Proceed to next step. The model should invoke the StartTrace plugin with
            // the selected providers and exemplar events.
            //
            var providerManifests = string.Join(", ", filtered.Select(p => p.ManifestJson));
            return string.Format(s_FollowupActions["ask_to_pick_providers"],
                filtered.Count(), providerManifests);
        }

        [KernelFunction("SearchForExemplarEvents")]
        [Description("Perform vector similarity search between exemplar events and realtime events captured in vector database.")]
        public async Task<string> SearchForExemplarEvents(
            [Description("List of exemplar events (in XML format) to search in vector database")][Required] List<string> ExemplarEvents,
            CancellationToken Token,
            [Description("Score threshold to consider a match from 0 to 100")] int ScoreThreshold = 75,
            [Description("If ETW events should be captured first, provide a list of ETW provider names or GUIDs")] List<string> ProviderNamesOrIds = null,
            [Description("If ETW events should be captured first, how long to run the trace between 0 and 60 seconds")] int TraceTimeout = 10
            )
        {
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var progress = GlobalStateViewModel.Instance.g_InsightsViewModel.ProgressState;
            var inferenceService = kernel.GetRequiredService<InferenceService>();
            var vecDb = kernel.GetRequiredService<EtwVectorDbService>();
            if (!vecDb.m_Initialized)
            {
                return string.Format(s_FollowupActions["tell_general_error"], "Vector DB is not initialized");
            }

            //
            // Run the trace if requested
            //
            if (ProviderNamesOrIds != null && ProviderNamesOrIds.Count > 0)
            {
                try
                {
                    var trace = new EtwTraceSession();
                    var numImported = await trace.RunEtwTraceAsync(ProviderNamesOrIds, TraceTimeout, Token);
                    if (numImported == 0)
                    {
                        return string.Format(s_FollowupActions["tell_etw_trace_no_events"]);
                    }
                }
                catch (Exception ex)
                {
                    Trace(TraceLoggerType.SkPlugin,
                          TraceEventType.Error,
                          $"Failed to run ETW trace: {ex.Message}");
                    return string.Format(s_FollowupActions["tell_general_error"], ex.Message);
                }
            }

            //
            // Perform vector similarity search against exemplar events
            //
            // Vector search occurs on the Embedding property automatically, which maps to
            // the full JSON encoding of the event data.
            //
            var searchOptions = new VectorSearchOptions<EtwEventRecord>()
            {
                VectorProperty = r => r.Embedding
            };
            try
            {
                var results = new List<EtwEventRecord>();
                foreach (var exemplar in ExemplarEvents)
                {
                    var matches = await vecDb.SearchAsync(exemplar, 1000, ScoreThreshold, searchOptions, Token);
                    if (matches != null && matches.Count > 0)
                    {
                        results.AddRange(matches);
                    }
                }
                if (results.Count == 0)
                {
                    return string.Format(s_FollowupActions["tell_no_exemplar_event_matches"]);
                }
                var matchesString = results.Select(r => $"{r.EventJson}");
                return string.Format(s_FollowupActions["ask_perform_realtime_event_analysis"],
                    results.Count, matchesString);
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.SkPlugin,
                      TraceEventType.Error,
                      $"Vector search failed: {ex.Message}");
                return string.Format(s_FollowupActions["tell_general_error"], ex.Message);
            }
        }

        private async Task<bool> LoadProviderManifests(CancellationToken Token)
        {
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var progress = GlobalStateViewModel.Instance.g_InsightsViewModel.ProgressState;
            progress.UpdateProgressMessage($"Agent is loading ETW providers, please wait...");

            try
            {
                var vectorDb = kernel.GetRequiredService<EtwVectorDbService>();
                //
                // This is an expensive operation. Do not perform it unnecessarily.
                //
                var count = await vectorDb.GetRecordCountAsync<EtwProviderManifestRecord>(Token);
                if (count > 0)
                {
                    Trace(TraceLoggerType.Agents,
                          TraceEventType.Information,
                          $"ETW provider manifests already loaded: {count} records found.");
                    return true;
                }
                var result = await Task.Run(() =>
                {
                    return ProviderParser.GetManifests();
                });
                if (result == null || result.Count == 0)
                {
                    Trace(TraceLoggerType.Agents, TraceEventType.Error, $"No provider manifests found");
                    return false;
                }
                var records = result.Values.ToList();
                progress.UpdateProgressMessage($"Importing data...");
                await vectorDb.ImportDataAsync<ParsedEtwManifest, EtwProviderManifestRecord>(records, Token, progress);
                return true;
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.Agents,
                      TraceEventType.Error,
                      $"Vector data import failed: {ex.Message}");
                return false;
            }
        }

        private bool EtwProviderManifestPostFilter(EtwProviderManifestRecord Record, ProviderManifestSearchArguments Arguments)
        {
            var providerName = Arguments.ProviderName?.Trim();
            var providerGuid = Arguments.ProviderId?.Trim();
            var keywords = Arguments.ProviderKeywords?.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim()).ToList() ?? new List<string>();
            if (!string.IsNullOrEmpty(providerName) &&
                !string.IsNullOrEmpty(Record.Name) &&
                !Record.Name.Equals(providerName, StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(providerGuid) && !string.IsNullOrEmpty(Record.ProviderId) &&
                Guid.TryParse(providerGuid, out Guid guid1) && Guid.TryParse(Record.ProviderId, out Guid guid2) &&
                guid1 != guid2)
            {
                return false;
            }
            if (keywords.Count > 0 &&
                !keywords.Any(keyword => Record.Keywords.Contains(keyword, StringComparer.CurrentCultureIgnoreCase)))
            {
                return false;
            }
            return true;
        }
    }
}
