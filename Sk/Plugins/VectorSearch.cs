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
using static EtwPilot.ViewModel.InsightsViewModel;
using EtwPilot.Sk.Vector;
using EtwPilot.Sk.Vector.EtwEvent;
using EtwPilot.Sk.Vector.EtwProviderManifest;

namespace EtwPilot.Sk.Plugins
{
    internal class VectorSearch
    {
        private readonly EtwVectorDb m_VectorDb;

        public VectorSearch(EtwVectorDb VectorDb)
        {
            m_VectorDb = VectorDb;
        }

        [KernelFunction("SearchEtwProviderManifests")]
        [Description("Locate an ETW provider manifest of event names, tasks, opcodes, keywords, channels or template fields.")]
        public async Task<string> SearchEtwProviderManifestsAsync(string query)
        {
            var searchOptions = new VectorSearchOptions
            {
                VectorPropertyName = nameof(EtwProviderManifestRecord.DescriptionEmbedding)
            };
            return await m_VectorDb.Search(ChatTopic.Manifests, query, searchOptions);
        }

        [KernelFunction("SearchEtwEvents")]
        [Description("Locate ETW events whose ID, provider, task, keyword, etc relate to a particular process, thread, "+
            "activity or user of interest on a running system.")]
        public async Task<string> SearchEtwEventsAsync(string query)
        {
            var searchOptions = new VectorSearchOptions
            {
                VectorPropertyName = nameof(EtwEventRecord.DescriptionEmbedding)
            };
            return await m_VectorDb.Search(ChatTopic.EventData, query, searchOptions);
        }
    }
}
