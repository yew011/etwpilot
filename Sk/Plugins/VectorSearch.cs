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
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using EtwPilot.Sk.Vector;
using System.Text;
using EtwPilot.ViewModel;

namespace EtwPilot.Sk.Plugins
{
    internal class VectorSearch
    {
        private readonly Kernel m_Kernel;

        public VectorSearch(Kernel _Kernel)
        {
            m_Kernel = _Kernel;
        }

        [KernelFunction("SearchEtwProviderManifests")]
        [Description("Locate an ETW provider manifest of event names, tasks, opcodes, keywords, channels or template fields.")]
        public async Task<string> SearchEtwProviderManifestsAsync(
            CancellationToken Token,
            string ProviderName,
            string? ProviderGuid = "",
            [Description("Keywords such as event names, template field names, channels, tasks, etc")] string Keywords = "",
            [Description("Minimum confidence score to count as a match (from 0 to 100, where 100 is 100% confidence)")] int MinScore = 60,
            [Description("Return the top N results")] int Top = -1
            )
        {
            ProgressState? progress = null;

            if (m_Kernel.Data.TryGetValue("ProgressState", out object? _progress))
            {
                progress = _progress as ProgressState;
            }
            progress?.UpdateProgressMessage($"The model has started a vector search of provider manifest data.");

            var vectorDb = m_Kernel.GetRequiredService<EtwVectorDb>();

            var count = await vectorDb.GetRecordCount(vectorDb.s_EtwProviderManifestCollectionName, Token);
            if (count == 0)
            {
                return $$"""
                    {
                        "status": "failed",
                        "message": "There are no ETW provider manifests in the database.",
                        "next_step": "Call the appropriate tool to populate the provider manifests."
                    }
                    """;
            }

            //
            // Vector search occurs on the Embedding property automatically, which maps to
            // the full JSON encoding of the provider manifest. The embeddings for that JSON
            // blob is compared against the input keywords.
            // Sk and Qdrant's support of pre-filtering is awful (linq expressions only, limited operators)
            // so we'll use a post filter
            //
            var searchOptions = new VectorSearchOptions<EtwProviderManifestRecord>()
            {
                VectorProperty = r => r.Embedding
            };
            var results = await vectorDb.Search(
                vectorDb.s_EtwProviderManifestCollectionName, Keywords, Top, MinScore, searchOptions, Token);
            if (results == null || results.Count == 0)
            {
                return $$"""
                    {
                        "status": "failed",
                        "message": "The search produced no results.",
                        "next_step": "Retry the operation if you can resolve the error, otherwise ask the user."
                    }
                    """;
            }
            //
            // Apply post-filter
            //
            var filtered = results.Where(r => EtwProviderManifestPostFilter(
                r, ProviderName, ProviderGuid)).Take(Top);
            if (filtered.Count() == 0)
            {
                return $$"""
                    {
                        "status": "failed",
                        "message": "The search produced results, but parameters passed to this function "
                        "resulted in all results being filtered out.",
                        "next_step": "Retry the operation if you can resolve the error, otherwise ask the user."
                    }
                    """;
            }

            var sb = new StringBuilder();
            foreach (var record in filtered)
            {
                sb.AppendLine($"{record.ManifestJson}");
            }
            return $$"""
                    {
                        "status": "success",
                        "message": "The search produced results, supplied in the 'records' property.",
                        "records": "{{sb}}",
                        "next_step": "Address the user's original question with this additional context."
                    }
                    """;
        }

        [KernelFunction("SearchEtwEvents")]
        [Description("Locate ETW events whose ID, provider, task, keyword, etc relate to a particular process, thread, "+
            "activity or user of interest on a running system.")]
        public async Task<string> SearchEtwEventsAsync(
            CancellationToken Token,
            string ProviderName,
            string ProviderGuid = "",
            int EventId = -1,
            int EventVersion = -1,
            [Description("ID of the process that caused the event to be generated")] int ProcessId = -1,
            [Description("Start key of the process that caused the event to be generated")] long ProcessStartKey = -1,
            [Description("ID of the thread that caused the event to be generated")] int ThreadId = -1,
            [Description("User SID for the account that caused the event to be generated")] string UserSid = "",
            [Description("Activity ID that correlates this event to other events")] string ActivityId = "",
            [Description("Timestamp of the event")] string Timestamp = "",
            [Description("Search terms such as level, channel, keywords, task, opcode, stackwalk function names, template fields, etc")] string Keywords = "",
            [Description("Minimum confidence score to count as a match (from 0 to 100, where 100 is 100% confidence)")] int MinScore = 60,
            [Description("Return the top N results")] int Top = -1)
        {
            ProgressState? progress = null;
            if (m_Kernel.Data.TryGetValue("ProgressState", out object? _progress))
            {
                progress = _progress as ProgressState;
            }
            progress?.UpdateProgressMessage($"The model has started a vector search of event data.");

            var vectorDb = m_Kernel.GetRequiredService<EtwVectorDb>();

            var count = await vectorDb.GetRecordCount(vectorDb.s_EtwEventCollectionName, Token);
            if (count == 0)
            {
                return $$"""
                    {
                        "status": "failed",
                        "message": "There are no ETW events in the database.",
                        "next_step": "Call the appropriate tool to generate some ETW events."
                    }
                    """;
            }

            //
            // Vector search occurs on the Embedding property automatically, which maps to
            // the full JSON encoding of the event data. The embeddings for that JSON
            // blob is compared against the input keywords.
            // Sk and Qdrant's support of pre-filtering is awful (linq expressions only, limited operators)
            // so we'll use a post filter
            //
            var searchOptions = new VectorSearchOptions<EtwEventRecord>()
            {
                VectorProperty = r => r.Embedding
            };
            var results = await vectorDb.Search(
                vectorDb.s_EtwEventCollectionName, Keywords, Top, MinScore, searchOptions, Token);
            if (results == null || results.Count == 0)
            {
                return $$"""
                    {
                        "status": "failed",
                        "message": "The search produced no results.",
                        "next_step": "Retry the operation if you can resolve the error, otherwise ask the user."
                    }
                    """;
            }
            //
            // Apply post-filter
            //
            var filtered = results.Where(r => EtwEventPostFilter(r, ProviderName, ProviderGuid, EventId,
                EventVersion, ProcessId, ProcessStartKey, ThreadId, UserSid, ActivityId, Timestamp)).Take(Top);
            if (filtered.Count() == 0)
            {
                return $$"""
                    {
                        "status": "failed",
                        "message": "The search produced results, but parameters passed to this function "
                        "resulted in all results being filtered out.",
                        "next_step": "Retry the operation if you can resolve the error, otherwise ask the user."
                    }
                    """;
            }
            var sb = new StringBuilder();
            foreach (var record in filtered)
            {
                sb.AppendLine($"{record.EventJson}");
            }
            return $$"""
                    {
                        "status": "success",
                        "message": "The search produced results, supplied in the 'records' property.",
                        "records": "{{sb}}",
                        "next_step": "Address the user's original question with this additional context."
                    }
                    """;
        }

        private bool EtwProviderManifestPostFilter(EtwProviderManifestRecord Record,
            string ProviderName,
            string ProviderGuid)
        {
            if (!string.IsNullOrEmpty(ProviderName) &&
                !string.IsNullOrEmpty(Record.Name) &&
                !Record.Name.Equals(ProviderName, StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(ProviderGuid) && !string.IsNullOrEmpty(Record.ProviderId) &&
                Guid.TryParse(ProviderGuid, out Guid guid1) && Guid.TryParse(Record.ProviderId, out Guid guid2) &&
                guid1 != guid2)
            {
                return false;
            }
            return true;
        }

        private bool EtwEventPostFilter(
            EtwEventRecord Record,
            string ProviderName,
            string ProviderGuid,
            int EventId,
            int EventVersion,
            int ProcessId,
            long ProcessStartKey,
            int ThreadId,
            string UserSid,
            string ActivityId,
            string Timestamp)
        {
            Guid guid1, guid2;

            if (!string.IsNullOrEmpty(ProviderName) && 
                !string.IsNullOrEmpty(Record.ProviderName) &&
                !Record.ProviderName.Equals(ProviderName, StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(ProviderGuid) && !string.IsNullOrEmpty(Record.ProviderId) &&
                Guid.TryParse(ProviderGuid, out guid1) && Guid.TryParse(Record.ProviderId, out guid2) &&
                guid1 != guid2)
            {
                return false;
            }
            if (EventId > 0 && EventId != Record.EventId)
            {
                return false;
            }
            if (EventVersion > 0 && EventVersion != Record.EventVersion)
            {
                return false;
            }
            if (ProcessId > 0 && ProcessId != Record.ProcessId)
            {
                return false;
            }
            if (ProcessStartKey > 0 && ProcessStartKey != Record.ProcessStartKey)
            {
                return false;
            }
            if (ThreadId > 0 && ThreadId != Record.ThreadId)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(UserSid) &&
                !string.IsNullOrEmpty(Record.UserSid) &&
                !Record.UserSid.Equals(UserSid, StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(ActivityId) && !string.IsNullOrEmpty(Record.ActivityId) &&
                Guid.TryParse(ActivityId, out guid1) && Guid.TryParse(Record.ActivityId, out guid2) &&
                guid1 != guid2)
            {
                return false;
            }
            return true;
        }
    }
}
