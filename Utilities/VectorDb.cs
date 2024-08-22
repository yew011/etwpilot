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
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client.Grpc;
using Qdrant.Client;
using Microsoft.SemanticKernel.Embeddings;
using EtwPilot.ViewModel;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.VectorData;
using static EtwPilot.Utilities.EtwProviderManifestVectorDb;

//
// Remove supression after SK vector store is out of alpha
//
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0020

namespace EtwPilot.Utilities
{
    internal class EtwProviderManifestVectorDb
    {
        #region class defs

        public class EtwProviderManifestRecord
        {
            public Guid ProviderId { get; set; }
            public string ProviderName { get; set; }
            public string ProviderSource { get; set; }
            public List<string> EventIds { get; set; }
            public List<string> Channels { get; set; }
            public List<string> Tasks { get; set; }
            public List<string> Keywords { get; set; }
            public List<string> Strings { get; set; }
            public List<string> TemplateFields { get; set; }
            public ReadOnlyMemory<float> ChannelsEmbedding { get; set; }
            public ReadOnlyMemory<float> TasksEmbedding { get; set; }
            public ReadOnlyMemory<float> KeywordsEmbedding { get; set; }
            public ReadOnlyMemory<float> StringsEmbedding { get; set; }
            public ReadOnlyMemory<float> TemplateFieldEmbedding { get; set; }
        }

        internal class Mapper : IVectorStoreRecordMapper<EtwProviderManifestRecord, PointStruct>
        {
            public PointStruct MapFromDataToStorageModel(EtwProviderManifestRecord Record)
            {
                var ps = new PointStruct
                {
                    Id = new PointId { Uuid = Record.ProviderId.ToString() },
                    Vectors = new Vectors() { },
                    Payload = {
                        { "ProviderName", Record.ProviderName },
                        { "ProviderSource", Record.ProviderSource },
                        { "EventIds", IntListAsProtoValue(Record.EventIds) },
                        { "Channels", StringListAsProtoValue(Record.Channels) },
                        { "Tasks", StringListAsProtoValue(Record.Tasks) },
                        { "Keywords", StringListAsProtoValue(Record.Keywords) },
                        { "Strings", StringListAsProtoValue(Record.Strings) },
                        { "TemplateFields", StringListAsProtoValue(Record.TemplateFields) },
                    },
                };

                var namedVectors = new NamedVectors();
                namedVectors.Vectors.Add("ChannelsEmbedding", Record.ChannelsEmbedding.ToArray());
                namedVectors.Vectors.Add("TasksEmbedding", Record.TasksEmbedding.ToArray());
                namedVectors.Vectors.Add("KeywordsEmbedding", Record.KeywordsEmbedding.ToArray());
                namedVectors.Vectors.Add("StringsEmbedding", Record.StringsEmbedding.ToArray());
                namedVectors.Vectors.Add("TemplateFieldsEmbedding", Record.TemplateFieldEmbedding.ToArray());
                ps.Vectors.Vectors_ = namedVectors;
                return ps;
            }

            public EtwProviderManifestRecord MapFromStorageToDataModel(PointStruct Ps, StorageToDataModelMapperOptions Options)
            {
                //
                // TODO
                //
                return new EtwProviderManifestRecord();
            }

            private static Value IntListAsProtoValue(List<string> Values)
            {
                var integers = Values.Select(
                    e => Int32.TryParse(e, out int n) ? n : (int?)null)
                    .Where(n => n.HasValue)
                    .Select(n => n.Value)
                    .ToList();
                var values = new Value();
                values.ListValue = new ListValue();
                integers.ForEach(e => values.ListValue.Values.Add(e));
                return values;
            }

            private static Value StringListAsProtoValue(List<string> Values)
            {
                var ret = new Value();
                ret.ListValue = new ListValue();
                Values.ForEach(c => ret.ListValue.Values.Add(c.ToString()));
                return ret;
            }
        }

        #endregion

        #region record defs
        public static readonly VectorStoreRecordDefinition s_EtwProviderManifestRecordDefinition =
            new VectorStoreRecordDefinition
            {
                Properties = new List<VectorStoreRecordProperty>
                    {
                        new VectorStoreRecordKeyProperty("ProviderId", typeof(Guid)),
                        new VectorStoreRecordDataProperty("ProviderName", typeof(string)) { IsFilterable = true, IsFullTextSearchable=true },
                        new VectorStoreRecordDataProperty("ProviderSource", typeof(string)),
                        new VectorStoreRecordDataProperty("EventIds", typeof(List<int>)),
                        new VectorStoreRecordDataProperty("Channels", typeof(List<string>)),
                        new VectorStoreRecordDataProperty("Tasks", typeof(List<string>)),
                        new VectorStoreRecordDataProperty("Keywords", typeof(List<string>)),
                        new VectorStoreRecordDataProperty("Strings", typeof(List<string>)),
                        new VectorStoreRecordDataProperty("TemplateFields", typeof(List<string>)),
                        new VectorStoreRecordVectorProperty("ChannelsEmbedding", typeof(ReadOnlyMemory<float>)) {
                            Dimensions = 384,
                            IndexKind = IndexKind.Hnsw,
                            DistanceFunction = DistanceFunction.CosineSimilarity
                        },
                        new VectorStoreRecordVectorProperty("TasksEmbedding", typeof(ReadOnlyMemory<float>)) {
                            Dimensions = 384,
                            IndexKind = IndexKind.Hnsw,
                            DistanceFunction = DistanceFunction.CosineSimilarity
                        },
                        new VectorStoreRecordVectorProperty("KeywordsEmbedding", typeof(ReadOnlyMemory<float>)) {
                            Dimensions = 384,
                            IndexKind = IndexKind.Hnsw,
                            DistanceFunction = DistanceFunction.CosineSimilarity
                        },
                        new VectorStoreRecordVectorProperty("StringsEmbedding", typeof(ReadOnlyMemory<float>)) {
                            Dimensions = 384,
                            IndexKind = IndexKind.Hnsw,
                            DistanceFunction = DistanceFunction.CosineSimilarity
                        },
                        new VectorStoreRecordVectorProperty("TemplateFieldsEmbedding", typeof(ReadOnlyMemory<float>)) {
                            Dimensions = 384,
                            IndexKind = IndexKind.Hnsw,
                            DistanceFunction = DistanceFunction.CosineSimilarity
                        },
                    }
            };
        #endregion

        //
        // BGE (BERT) micro v2 (https://huggingface.co/TaylorAI/bge-micro-v2)
        //
        private static readonly int s_EmbeddingVectorDimensions = 384;
        public static readonly string s_CollectionName = "manifests";
        private readonly Kernel m_Kernel;
        private StateManager m_StateManager;

        public EtwProviderManifestVectorDb(Kernel kernel, StateManager stateManager)
        {
            m_Kernel = kernel;
            m_StateManager = stateManager;
        }

        public async Task Create(int Limit)
        {
            var collection = new QdrantVectorStoreRecordCollection<EtwProviderManifestRecord>(
                new QdrantClient("localhost"),
                s_CollectionName,
                new()
                {
                    HasNamedVectors = true,
                    PointStructCustomMapper = new Mapper(),
                    VectorStoreRecordDefinition = s_EtwProviderManifestRecordDefinition
                });
            await collection.CreateCollectionAsync();
            m_StateManager.ProgressState.InitializeProgress(Limit + 1);
            m_StateManager.ProgressState.UpdateProgressMessage($"Loading provider manifests {Limit}");
            var all = ProviderParser.GetManifests(Limit);
            m_StateManager.ProgressState.UpdateProgressValue();
            int i = 0;
            var embeddingService = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            foreach (var kvp in all)
            {
                var provider = kvp.Value;
                var record = new EtwProviderManifestRecord()
                {
                    ProviderId = provider.Provider.Id,
                    ProviderName = provider.Provider.Name!,
                    ProviderSource = provider.Provider.Source!,
                    EventIds = provider.Events.Select(e => e.Id).ToList(),
                    Channels = provider.Channels.Select(c => $"{c}").ToList(),
                    Tasks = provider.Tasks.Select(t => $"{t}").ToList(),
                    Keywords = provider.Keywords.Select(k => $"{k}").ToList(),
                    Strings = provider.StringTable.Select(s => $"{s}").ToList(),
                    TemplateFields = provider.Templates.Select(t => $"{t}").ToList(),
                };

                m_StateManager.ProgressState.UpdateProgressMessage($"Generating embeddings {++i} of {Limit}...");
                //
                // TODO: joining as list of strings vs multivector embeddings?
                //
                var result = await embeddingService.GenerateEmbeddingAsync(string.Join(",", record.Channels));
                record.ChannelsEmbedding = result.ToArray();
                result = await embeddingService.GenerateEmbeddingAsync(string.Join(",", record.Tasks));
                record.TasksEmbedding = result.ToArray();
                result = await embeddingService.GenerateEmbeddingAsync(string.Join(",", record.Keywords));
                record.KeywordsEmbedding = result.ToArray();
                result = await embeddingService.GenerateEmbeddingAsync(string.Join(",", record.Strings));
                record.StringsEmbedding = result.ToArray();
                result = await embeddingService.GenerateEmbeddingAsync(string.Join(",", record.TemplateFields));
                record.TemplateFieldEmbedding = result.ToArray();

                await collection.UpsertAsync(record);
                m_StateManager.ProgressState.UpdateProgressValue();
            }
            m_StateManager.ProgressState.FinalizeProgress("Vector database created.");
        }
    }

    internal class QdrantVecDbEtwSearchPlugin
    {
        private readonly Kernel m_Kernel;

        public QdrantVecDbEtwSearchPlugin(Kernel _Kernel)
        {
            m_Kernel = _Kernel;
        }

        [KernelFunction("SearchEtwProvider")]
        [Description("Search for an ETW provider similar to the given query.")]
        public async Task<string> SearchEtwProviderAsync(string query)
        {
            var store = m_Kernel.GetRequiredService<IVectorStore>();
            var embedding = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var searchVector = await embedding.GenerateEmbeddingAsync(query);
            var searchOptions = new VectorSearchOptions
            {
                VectorPropertyName = nameof(EtwProviderManifestRecord.ChannelsEmbedding)
            };
            var collection = store.GetCollection<ulong, EtwProviderManifestRecord>(
                s_CollectionName, s_EtwProviderManifestRecordDefinition);
            var searchResult = await collection.VectorizedSearchAsync(searchVector, searchOptions);
            await foreach (var item in searchResult.Results)
            {
                return item.Record.ToString();
            }
            return string.Empty;
        }
    }
}
