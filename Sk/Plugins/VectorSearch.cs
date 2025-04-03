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
using Microsoft.SemanticKernel.Embeddings;
using System.Text;
using System.ComponentModel.DataAnnotations;

#pragma warning disable SKEXP0001

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
            [Description("ETW provider GUID or ID"), Required] string Provider,
            [Description("Comma-separated list of event IDs")] string EventIds,
            [Description("Comma-separated list of template field names")] string TemplateFieldNames,
            [Description("Comma-separated list of channels")] string Channels,
            [Description("Comma-separated list of tasks")] string Tasks,
            [Description("Minimum confidence score to count as a match")] int minScore,
            CancellationToken Token
            )
        {
            var vectorDb = m_Kernel.GetRequiredService<EtwVectorDb>();
            var searchOptions = new VectorSearchOptions<EtwProviderManifestRecord>
            {
                VectorProperty = r => r.DescriptionEmbedding
            };
            var query = EtwProviderManifestRecord.GetDescriptionForVectorSearch(Provider, EventIds, TemplateFieldNames, Channels);
            var textEmbSvc = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var results = await vectorDb.Search(EtwVectorDb.s_EtwProviderManifestCollectionName,
                query, textEmbSvc, searchOptions, Token);
            if (results == null || results.TotalCount == 0)
            {
                return string.Empty;
            }
            var items = await results.Results.ToListAsync(Token);
            var sb = new StringBuilder();
            foreach (var item in items)
            {
                if (item.Score < minScore)
                {
                    continue;
                }
                sb.AppendLine(item.Record.Description);
            }
            return sb.ToString();
        }

        [KernelFunction("SearchEtwEvents")]
        [Description("Locate ETW events whose ID, provider, task, keyword, etc relate to a particular process, thread, "+
            "activity or user of interest on a running system.")]
        public async Task<string> SearchEtwEventsAsync(
            [Description("ETW provider GUID or ID"), Required] string Provider,
            [Description("ETW event ID"), Required] int EventId,
            [Description("Minimum confidence score to count as a match")] int minScore,
            CancellationToken Token
            )
        {
            var vectorDb = m_Kernel.GetRequiredService<EtwVectorDb>();
            var searchOptions = new VectorSearchOptions<EtwEventRecord>
            {
                VectorProperty = r => r.DescriptionEmbedding
            };
            var query = EtwEventRecord.GetDescriptionForVectorSearch(EventId, Provider: Provider);
            var textEmbSvc = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var results = await vectorDb.Search(EtwVectorDb.s_EtwEventCollectionName,
                query, textEmbSvc, searchOptions, Token);
            if (results == null || results.TotalCount == 0)
            {
                return string.Empty;
            }
            var items = await results.Results.ToListAsync(Token);
            var sb = new StringBuilder();
            foreach (var item in items)
            {
                if (item.Score < minScore)
                {
                    continue;
                }
                sb.AppendLine(item.Record.Description);
            }
            return sb.ToString();
        }
    }
}
