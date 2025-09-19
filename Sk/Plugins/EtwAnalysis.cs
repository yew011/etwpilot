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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EtwPilot.Sk.Plugins
{
    //
    // TODO: Sk produces a manifest for these types and sends it to the model
    // but the models I have used (qwen, gpt-oss) appear to ignore it.
    //
    public class ProviderManifestSearchArguments
    {
        [Description("ETW provider name to search for")]
        [JsonPropertyName("provider_name")]
        public string? ProviderName { get; set; }
        [Description("ETW provider ID (GUID) to search for")]
        [JsonPropertyName("provider_id")]
        public string? ProviderId { get; set; }
        [Description("List of keywords to search for in provider manifests")]
        [JsonPropertyName("provider_keywords")]
        public List<string> ProviderKeywords { get; set; }

        public ProviderManifestSearchArguments()
        {
            ProviderKeywords = new List<string>();
        }

        public override string ToString()
        {
            return $"ProviderName: {ProviderName ?? "null"}, ProviderId: {ProviderId ?? "null"}, ProviderKeywords: [" +
                $"{(ProviderKeywords != null ? string.Join(", ", ProviderKeywords) : "null")}]";
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ProviderName) &&
                string.IsNullOrWhiteSpace(ProviderId) &&
                ProviderKeywords.Count == 0)
            {
                throw new ArgumentException("At least one of ProviderName, ProviderId, or ProviderKeywords must be provided.");
            }
        }
    }

    public class ModelFollowUpRequest
    {
        public string status { get; set; }
        public string message { get; set; }
        public string next_step { get; set; }
        public object? data { get; set; }
    }

    internal class EtwAnalysis
    {
        private static int MAX_PROVIDERS = 10;
        private static readonly Dictionary<string, ModelFollowUpRequest> s_FollowupRequests = new Dictionary<string, ModelFollowUpRequest>()
        {
            { "tell_no_matching_providers", new ModelFollowUpRequest() {
                status="failed",
                message="The supplied arguments did not match any ETW provider manifests",
                next_step="Either try different keywords or ask the user for help, then try to start the ETW analysis again."
                }
            },
            { "tell_etw_trace_no_events", new ModelFollowUpRequest() {
                status="failed",
                message="The requested ETW trace captured no events",
                next_step="No events were captured based on your provider/event selections. You might try another provider or event set."
                }
            },
            { "ask_to_pick_providers", new ModelFollowUpRequest() {
                status="success",
                message="The list of ETW provider manifests is provided in the 'data' field. Only use these names, do not make up a name.",
                next_step="Follow the instructions in the system prompt."
                }
            },
            { "ask_to_examine_manifests", new ModelFollowUpRequest() {
                status="success",
                message="The manifests for the requested ETW providers are provided in the 'data' field.",
                next_step="Follow the instructions in the system prompt."
                }
            },
            { "ask_perform_realtime_event_analysis", new ModelFollowUpRequest() {
                status="success",
                message="Matching ETW events are provided in the 'data' field.",
                next_step="Follow the instructions in the system prompt."
                }
            },
        };

        public EtwAnalysis()
        {
        }

        [KernelFunction("GetProviderList")]
        [Description("Retrieves a list of ETW providers available on the system.")]
        public async Task<string> GetProviderList(CancellationToken Token)
        {
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var progress = GlobalStateViewModel.Instance.g_InsightsViewModel.ProgressState;
            using var progressContext = progress.CreateProgressContext(2, $"Model is retrieving ETW provider list.");
            var inferenceService = kernel.GetRequiredService<InferenceService>();
            var vecDb = kernel.GetRequiredService<EtwVectorDbService>();
            if (!vecDb.m_Initialized)
            {
                throw new Exception("Vector DB is not initialized");
            }

            await LoadProviderManifests(Token);
            progress.UpdateProgressValue();

            //
            // Find manifests in vector db matching keyword list using hybrid search.
            // We'll pass a "neutral" vector (all zeros) to rely entirely on keyword matching.
            //
            var searchVector = vecDb.GetNeutralVector();
            var searchOptions = new HybridSearchOptions<EtwProviderManifestRecord>()
            {
                VectorProperty = r => r.Embedding,
                AdditionalProperty = r => r.Keywords
            };
            var results = await vecDb.GetAsync<EtwProviderManifestRecord>(Token);
            if (results == null || results.Count == 0)
            {
                throw new Exception("No ETW provider manifests found in the vector database");
            }

            progress.UpdateProgressValue();

            var providerNames = results.Select(r => r.Name).ToList();
            return FormatFollowupMessage("ask_to_pick_providers", providerNames);
        }

        [KernelFunction("GetProviderManifests")]
        [Description("Retrieves the manifests for the given ETW provider names.")]
        public async Task<string> GetProviderManifests(
            [Description("The list of provider names")][Required()] List<string> ProviderNames,
            CancellationToken Token
            )
        {
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var progress = GlobalStateViewModel.Instance.g_InsightsViewModel.ProgressState;
            using var progressContext = progress.CreateProgressContext(2, $"Model is retrieving manifests for selected ETW providers.");
            var inferenceService = kernel.GetRequiredService<InferenceService>();
            var vecDb = kernel.GetRequiredService<EtwVectorDbService>();
            if (!vecDb.m_Initialized)
            {
                throw new Exception("Vector DB is not initialized");
            }
            if (ProviderNames == null || ProviderNames.Count == 0)
            {
                throw new Exception("ProviderNames is required");
            }

            await LoadProviderManifests(Token);
            progress.UpdateProgressValue();

            //
            // We'll pass a "neutral" vector (all zeros) to rely entirely on filtering.
            //
            var searchVector = vecDb.GetNeutralVector();
            var searchOptions = new VectorSearchOptions<EtwProviderManifestRecord>()
            {
                VectorProperty = r => r.Embedding,
                Filter = r => ProviderNames.Contains(r.Name)
            };
            var results = await vecDb.SearchAsync(searchVector, MAX_PROVIDERS, 0, searchOptions, Token);
            if (results == null || results.Count == 0)
            {
                throw new Exception("No ETW provider manifests found in the vector database");
            }
            if (results.Count != ProviderNames.Count)
            {
                //
                // Some models are too creative, even if temperature is tuned.
                //
                var invalid = ProviderNames.Where(name => !results.Any(r => r.Name == name)).ToList();
                var error = $"The following provider names are invalid: {string.Join(',', invalid)}. "+
                    $"Retry with a list of valid provider names pulled from the provider list. Do not make up "+
                    $"names and ensure you are following the instructions in the system prompt.";
                throw new Exception(error);
            }

            progress.UpdateProgressValue();

            //
            // Each compressed manifest is a string that can be deserialized into an anonymous object.
            // Glue these together into a list for serialization. It's important to avoid multiple
            // serialization passes that introduce unnecessary string escapes that eat tokens.
            //
            var jsonObjects = results.Select(r => JsonSerializer.Deserialize<object>(r.ManifestCompressed)).ToList();
            return FormatFollowupMessage("ask_to_examine_manifests", jsonObjects);
        }

        [KernelFunction("StartRealTimeTrace")]
        [Description("Capture realtime system events.")]
        public async Task<string> StartRealTimeTrace(
            [Description("A list of provider names.")][Required] List<string> ProviderNames,
            CancellationToken Token,
            [Description("A list of provider event mappings, in the format <provider_name>:<event_id>:<event_version>. This parameter allows you to limit the trace of a given provider to only the listed events.")] List<string>? ProviderEventMappings = null,
            [Description("An optional list of process IDs to filter the trace")] List<int>? ProcessIds = null,
            [Description("An optional list of process names to filter the trace")] List<string>? ProcessNames = null,
            [Description("How many seconds to run the trace (between 0 and 60)")] int TraceTimeout = 10
            )
        {
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var progress = GlobalStateViewModel.Instance.g_InsightsViewModel.ProgressState;
            using var progressContext = progress.CreateProgressContext(4, $"Model has started an ETW trace");
            var inferenceService = kernel.GetRequiredService<InferenceService>();
            var vecDb = kernel.GetRequiredService<EtwVectorDbService>();
            if (!vecDb.m_Initialized)
            {
                throw new Exception("Vector DB is not initialized");
            }

            if (ProviderNames == null || ProviderNames.Count == 0)
            {
                throw new Exception("ProviderNames is required");
            }

            progress.UpdateProgressValue();
            progress.UpdateProgressMessage("Erasing existing ETW events from the vector database...");

            //
            // First, purge the event collection - eventually we want to retain history
            // to make the experience richer.
            //
            try
            {
                await vecDb.EraseAsync<EtwEventRecord>(Token);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to erase existing ETW events from the vector database: {ex.Message}");
            }

            progress.UpdateProgressValue();
            progress.UpdateProgressMessage("Starting ETW trace session...");

            //
            // Run the trace
            //
            try
            {
                //
                // Models don't appear to be great at adhering to tool schema even with simple
                // dictionary types. So we have to "unflatten" the provider event mappings ourselves.
                //
                var targetProviderInfo = new Dictionary<string, Dictionary<int, int>>();
                ProviderNames.ForEach(p => targetProviderInfo.Add(p, new Dictionary<int, int>()));
                if (ProviderEventMappings != null)
                {
                    foreach (var mapping in ProviderEventMappings)
                    {
                        var parts = mapping.Split(':');
                        if (parts.Length != 3)
                        {
                            throw new Exception($"Invalid ProviderEventMapping format: {mapping}. Expected format is <provider_name>:<event_id>:<event_version>");
                        }
                        var providerName = parts[0];
                        if (!targetProviderInfo.ContainsKey(providerName))
                        {
                            throw new Exception($"Provider name '{providerName}' in ProviderEventMappings is not in the ProviderNames list");
                        }
                        if (!int.TryParse(parts[1], out int eventId))
                        {
                            throw new Exception($"Invalid event ID in ProviderEventMapping: {mapping}");
                        }
                        if (!int.TryParse(parts[2], out int eventVersion))
                        {
                            throw new Exception($"Invalid event version in ProviderEventMapping: {mapping}");
                        }
                        targetProviderInfo[providerName][eventId] = eventVersion;
                    }
                }
                var processNames = ProcessNames != null ? ProcessNames : new List<string>();
                var processIds = ProcessIds != null ? ProcessIds : new List<int>();
                var trace = new EtwTraceSession();
                var numImported = await trace.RunEtwTraceAsync(
                    targetProviderInfo, processNames, processIds, TraceTimeout, Token);
                progress.UpdateProgressValue();
                if (numImported == 0)
                {
                    progress.FinalizeProgress("No ETW events captured during the trace");
                    return FormatFollowupMessage("tell_etw_trace_no_events", null);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to run ETW trace session: {ex.Message}");
            }

            //
            // Simply return all captured events for now.
            //
            // TODO: Find how vector similarity search can help here. Right now we're relying
            // on the model to provide provider ID and event ID list, which naturally should
            // cull down the event matches. We are also exposing process ID as a way to limit
            // the trace. This might be sufficient to keep token usage low, making vector search
            // unnecessary for simple evaluations - however, for complex investigative scenarios,
            // it will be needed.
            //
            var results = await vecDb.GetAsync<EtwEventRecord>(Token);
            progress.UpdateProgressValue();
            if (results == null || results.Count == 0)
            {
                throw new Exception("No ETW events found in the vector database");
            }

            //
            // The model should conclude analysis with the events found.
            // TODO: Compress the events to reduce token usage.
            //
            var jsonObjects = results.Select(r => JsonSerializer.Deserialize<object>(r.EventJson)).ToList();
            return FormatFollowupMessage("ask_perform_realtime_event_analysis", jsonObjects);
        }

        private async Task LoadProviderManifests(CancellationToken Token)
        {
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var progress = GlobalStateViewModel.Instance.g_InsightsViewModel.ProgressState;
            using var progressContext = progress.CreateProgressContext(2, $"Model is loading ETW providers, please wait...");
            var vectorDb = kernel.GetRequiredService<EtwVectorDbService>();
            //
            // This is an expensive operation. Do not perform it unnecessarily.
            //
            var count = await vectorDb.GetRecordCountAsync<EtwProviderManifestRecord>(Token);
            if (count > 0)
            {
                return;
            }
            var result = await Task.Run(() =>
            {
                return ProviderParser.GetManifests();
            });
            progress.UpdateProgressValue();
            if (result == null || result.Count == 0)
            {
                throw new Exception("No provider manifests found");
            }
            var records = result.Values.ToList();
            progress.UpdateProgressMessage($"Importing data...");
            await vectorDb.ImportDataAsync<ParsedEtwManifest, EtwProviderManifestRecord>(records, Token, progress);
            progress.UpdateProgressValue();
        }

        private static string FormatFollowupMessage(string key, object? data)
        {
            if (!s_FollowupRequests.TryGetValue(key, out var request))
            {
                throw new ArgumentException($"Key '{key}' not found in s_FollowupActions.");
            }
            request.data = data;
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(request, options);
        }
    }
}
