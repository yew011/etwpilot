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
using System.Runtime.CompilerServices;
using static EtwPilot.Utilities.EtwVectorDb;
using Google.Protobuf.Collections;
using System.Net.Http;
using System.Net;
using System.IO;

//
// Remove supression after SK vector store is out of alpha
//
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0020

namespace EtwPilot.Utilities
{
    internal class EtwCollectionFactory() : IQdrantVectorStoreRecordCollectionFactory
    {
        public IVectorStoreRecordCollection<TKey, TRecord> CreateVectorStoreRecordCollection<TKey, TRecord>(
            QdrantClient qdrantClient,
            string name,
            VectorStoreRecordDefinition? vectorStoreRecordDefinition)
        where TKey : notnull
        {
            foreach (var supportedCollection in EtwVectorDb.s_SupportedCollections)
            {
                var collectionName = supportedCollection.Key;
                var meta = supportedCollection.Value;

                if (collectionName == name &&
                    typeof(TRecord) == typeof(EtwProviderManifestRecord))
                {
                    var customCollection = new QdrantVectorStoreRecordCollection<EtwProviderManifestRecord>(
                        qdrantClient,
                        name,
                        new()
                        {
                            HasNamedVectors = true,
                            PointStructCustomMapper = new EtwProviderManifestRecordMapper(),
                            VectorStoreRecordDefinition = vectorStoreRecordDefinition
                        }) as IVectorStoreRecordCollection<TKey, TRecord>;
                    return customCollection!;
                }
            }

            throw new NotImplementedException();
        }
    }

    internal class EtwVectorDb : INotifyPropertyChanged
    {
        #region record defs

        public class EtwProviderManifestRecord
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Source { get; set; }
            public List<string> EventIds { get; set; }
            public List<string> Channels { get; set; }
            public List<string> Tasks { get; set; }
            public List<string> Keywords { get; set; }
            public List<string> Strings { get; set; }
            public List<string> TemplateFields { get; set; }
            public string Description { get; set; }
            public ReadOnlyMemory<float> DescriptionEmbedding { get; set; }

            public EtwProviderManifestRecord()
            {
                Id = Guid.NewGuid();
                Name = string.Empty;
                Source = string.Empty;
                EventIds = new List<string>();
                Channels = new List<string>();
                Tasks = new List<string>();
                Keywords = new List<string>();
                Strings = new List<string>();
                TemplateFields = new List<string>();
                DescriptionEmbedding = new ReadOnlyMemory<float>();
                Description = string.Empty;
            }

            public static async Task<EtwProviderManifestRecord> CreateFromParsedEtwManifest(
                ParsedEtwManifest Manifest,
                ITextEmbeddingGenerationService EmbeddingService
                )
            {
                var record = new EtwProviderManifestRecord()
                {
                    Id = Manifest.Provider.Id,
                    Name = (string.IsNullOrEmpty(Manifest.Provider.Name) ? "(unnamed)" : Manifest.Provider.Name),
                    Source = (string.IsNullOrEmpty(Manifest.Provider.Source) ? "(unknown)" : Manifest.Provider.Source),
                    EventIds = Manifest.Events.Select(e => e.Id).ToList(),
                    Channels = Manifest.Channels.Select(c => $"{c}").ToList(),
                    Keywords = Manifest.Keywords.Select(k => $"{k}").ToList(),
                    //
                    // Note: ignoring strings for now; they're mostly repetitive of what's
                    // already captured in other aspects
                    //
                };

                foreach (var kvp2 in Manifest.Tasks)
                {
                    var taskName = kvp2.Key.Name;
                    var taskHexvalue = kvp2.Key.Value;
                    var opcodes = kvp2.Value;
                    var opcodeNames = opcodes.Select(o => $"{o.Name}").ToList();
                    record.Tasks.Add($"task {taskName}(value={taskHexvalue:X}) " +
                        $"has opcodes {string.Join(',', opcodeNames)}");
                }

                foreach (var kvp2 in Manifest.Templates)
                {
                    var structName = kvp2.Key;
                    var fields = kvp2.Value;
                    record.TemplateFields.AddRange(fields.Select(f => $"{f.Name}").Where(
                        f => !record.TemplateFields.Contains(f)));
                }

                record.Description = $@"There is an ETW provider with Id={record.Id} named " +
                    $"{record.Name} whose events are formatted from source {record.Source}. " +
                    $"The supported events have IDs {string.Join(",", record.EventIds)} and " +
                    $"among these events there are unique template fields named " +
                    $"{string.Join(",", record.TemplateFields)}. The provider further defines " +
                    $"channels: {string.Join(",", record.Channels)}; tasks/opcodes: " +
                    $"{string.Join(",", record.Tasks)}";

                var embeddingResult = await EmbeddingService.GenerateEmbeddingAsync(record.Description);
                record.DescriptionEmbedding = embeddingResult.ToArray();
                return record;
            }

            public static EtwProviderManifestRecord CreateFromQdrantPointStruct(PointStruct Ps)
            {
                var record = new EtwProviderManifestRecord()
                {
                    Id = new Guid(Ps.Id.Uuid),
                    Name = Ps.Payload["ProviderName"].StringValue,
                    Source = Ps.Payload["ProviderSource"].StringValue,
                    Description = Ps.Payload["Description"].StringValue
                };

                var eventIds = ExtractFieldFromPayload(Ps.Payload, "EventIds");
                var channels = ExtractFieldFromPayload(Ps.Payload, "Channels");
                var tasks = ExtractFieldFromPayload(Ps.Payload, "Tasks");
                var keywords = ExtractFieldFromPayload(Ps.Payload, "Keywords");
                var strings = ExtractFieldFromPayload(Ps.Payload, "Strings");
                var templateFields = ExtractFieldFromPayload(Ps.Payload, "TemplateFields");

                if (eventIds.Count > 0)
                {
                    record.EventIds.AddRange(eventIds);
                }
                if (channels.Count > 0)
                {
                    record.Channels.AddRange(channels);
                }
                if (tasks.Count > 0)
                {
                    record.Tasks.AddRange(tasks);
                }
                if (keywords.Count > 0)
                {
                    record.Keywords.AddRange(keywords);
                }
                if (strings.Count > 0)
                {
                    record.Strings.AddRange(strings);
                }
                if (templateFields.Count > 0)
                {
                    record.TemplateFields.AddRange(templateFields);
                }
                return record;
            }

            private static List<string> ExtractFieldFromPayload(MapField<string, Value> Payload, string FieldName)
            {
                var values = new List<string>();
                if (Payload.ContainsKey(FieldName))
                {
                    foreach (var value in Payload[FieldName].ListValue.Values)
                    {
                        values.Add(value.StringValue);
                    }
                }
                return values;
            }
        }

        internal class EtwProviderManifestRecordMapper : IVectorStoreRecordMapper<EtwProviderManifestRecord, PointStruct>
        {
            public PointStruct MapFromDataToStorageModel(EtwProviderManifestRecord Record)
            {
                var ps = new PointStruct
                {
                    Id = new PointId { Uuid = Record.Id.ToString() },
                    Vectors = new Vectors() { },
                    Payload = {
                        { "ProviderName", Record.Name },
                        { "ProviderSource", Record.Source },
                        { "EventIds", IntListAsProtoValue(Record.EventIds) },
                        { "Channels", StringListAsProtoValue(Record.Channels) },
                        { "Tasks", StringListAsProtoValue(Record.Tasks) },
                        { "Keywords", StringListAsProtoValue(Record.Keywords) },
                        { "Strings", StringListAsProtoValue(Record.Strings) },
                        { "TemplateFields", StringListAsProtoValue(Record.TemplateFields) },
                        { "Description", Record.Description },
                    },
                };

                var namedVectors = new NamedVectors();
                namedVectors.Vectors.Add("DescriptionEmbedding", Record.DescriptionEmbedding.ToArray());
                ps.Vectors.Vectors_ = namedVectors;
                return ps;
            }

            public EtwProviderManifestRecord MapFromStorageToDataModel(PointStruct Ps, StorageToDataModelMapperOptions Options)
            {
                return EtwProviderManifestRecord.CreateFromQdrantPointStruct(Ps);
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

        //
        // BGE (BERT) micro v2 (https://huggingface.co/TaylorAI/bge-micro-v2)
        //
        private static readonly int s_EmbeddingVectorDimensions = 384;
        public static readonly VectorStoreRecordDefinition s_EtwProviderManifestRecordDefinition =
            new VectorStoreRecordDefinition
            {
                Properties = new List<VectorStoreRecordProperty>
                    {
                        new VectorStoreRecordKeyProperty("Id", typeof(Guid)),
                        new VectorStoreRecordDataProperty("Name", typeof(string)) { IsFilterable = true, IsFullTextSearchable=true },
                        new VectorStoreRecordDataProperty("Source", typeof(string)),
                        new VectorStoreRecordDataProperty("EventIds", typeof(List<int>)),
                        new VectorStoreRecordDataProperty("Channels", typeof(List<string>)),
                        new VectorStoreRecordDataProperty("Tasks", typeof(List<string>)),
                        new VectorStoreRecordDataProperty("Keywords", typeof(List<string>)),
                        new VectorStoreRecordDataProperty("Strings", typeof(List<string>)),
                        new VectorStoreRecordDataProperty("TemplateFields", typeof(List<string>)),
                        new VectorStoreRecordDataProperty("Description", typeof(string)) { IsFilterable = true, IsFullTextSearchable = true },
                        new VectorStoreRecordVectorProperty("DescriptionEmbedding", typeof(ReadOnlyMemory<float>)) {
                            Dimensions = s_EmbeddingVectorDimensions,
                            IndexKind = IndexKind.Hnsw,
                            DistanceFunction = DistanceFunction.CosineSimilarity
                        },
                    }
            };
        #endregion

        public static readonly string s_ManifestCollectionName = "manifests";
        public static readonly string s_DataCollectionName = "data";
        private Kernel m_Kernel;
        private StateManager m_StateManager;
        private string m_HostUri;
        public bool m_Initialized;
        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly Dictionary<string, VectorStoreRecordDefinition> s_SupportedCollections =
            new Dictionary<string, VectorStoreRecordDefinition>()
            {
                { s_ManifestCollectionName, s_EtwProviderManifestRecordDefinition },
            };

        public EtwVectorDb()
        {
        }

        public async Task Initialize(Kernel kernel, StateManager stateManager, string HostUri)
        {
            m_Kernel = kernel;
            m_StateManager = stateManager;
            m_Initialized = true;
            m_HostUri = HostUri;

            var store = m_Kernel.GetRequiredService<IVectorStore>();
            //
            // Create collections if needed
            //
            var collections = store.ListCollectionNamesAsync().ToBlockingEnumerable().ToList();
            foreach (var kvp in s_SupportedCollections)
            {
                if (collections == null || !collections.Contains(kvp.Key))
                {
                    if (kvp.Key == s_ManifestCollectionName)
                    {
                        _ = await CreateManifestCollectionInternal(true);
                    }
                    else if (kvp.Key == s_DataCollectionName)
                    {

                    }
                    else
                    {
                        return;
                    }
                }
            }
            m_Initialized = true;
        }

        public async Task<ulong> GetManifestRecordCount()
        {
            IVectorStoreRecordCollection<ulong, EtwProviderManifestRecord> collection;
            try
            {
                collection = await CreateManifestCollectionInternal(false);
            }
            catch (Exception)
            {
                return 0;
            }

            using (var client = new QdrantClient(m_HostUri))
            {
                try
                {
                    return await client.CountAsync(s_ManifestCollectionName);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        public async Task ImportManifestData(List<ParsedEtwManifest> Manifests)
        {
            int total = Manifests.Count;
            if (total == 0)
            {
                throw new Exception("No data provided.");
            }

            var collection = await CreateManifestCollectionInternal(false);
            var embeddingService = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            m_StateManager.ProgressState.UpdateProgressMessage($"Populating collection with {total} records...");

            foreach (var manifest in Manifests)
            {
                var record = await EtwProviderManifestRecord.CreateFromParsedEtwManifest(manifest, embeddingService);
                await collection.UpsertAsync(record);
            }

            m_StateManager.ProgressState.UpdateProgressValue();
        }

        private async Task<IVectorStoreRecordCollection<ulong, EtwProviderManifestRecord>> CreateManifestCollectionInternal(bool CreateIfNotExists)
        {
            var store = m_Kernel.GetRequiredService<IVectorStore>();
            var collection = store.GetCollection<ulong, EtwProviderManifestRecord>(s_ManifestCollectionName,
                s_EtwProviderManifestRecordDefinition);
            if (!await collection.CollectionExistsAsync())
            {
                if (CreateIfNotExists)
                {
                    await collection.CreateCollectionIfNotExistsAsync();
                    return collection;
                }
                throw new InvalidOperationException($"Collection {s_ManifestCollectionName} does not exist");
            }
            return collection;
        }

        public async Task SaveManifestCollection(string Path)
        {
            await SaveCollectionInternal(s_ManifestCollectionName, Path);
        }

        public async Task RestoreManifestCollection(string Path)
        {
            await RestoreCollectionInternal(s_ManifestCollectionName, Path);
        }

        public async Task CreateDataCollection(int Limit = 0)
        {

        }

        public async Task SaveDataCollection(string Path)
        {
            await SaveCollectionInternal(s_DataCollectionName, Path);
        }

        public async Task<ulong> GetDataRecordCount()
        {
            IVectorStoreRecordCollection<ulong, EtwProviderManifestRecord> collection;
            try
            {
                collection = await CreateManifestCollectionInternal(false);
            }
            catch (Exception)
            {
                return 0;
            }

            using (var client = new QdrantClient(m_HostUri))
            {
                try
                {
                    return await client.CountAsync(s_ManifestCollectionName);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        public async Task RestoreDataCollection(string Path)
        {
            await RestoreCollectionInternal(s_DataCollectionName, Path);
        }


        private async Task SaveCollectionInternal(string Name, string OutputPath)
        {
            var store = m_Kernel.GetRequiredService<IVectorStore>();
            var collections = store.ListCollectionNamesAsync().ToBlockingEnumerable().ToList();
            if (collections == null || !collections.Contains(Name))
            {
                throw new Exception("Collection doesn't exist.");
            }

            //
            // Generate the snapshot on the qdrant node's local storage
            //
            m_StateManager.ProgressState.UpdateProgressMessage($"Generating snapshot....");
            var snapshotName = "";
            using (var client = new QdrantClient(m_HostUri))
            {
                var result = await client.CreateSnapshotAsync(Name);
                if (string.IsNullOrEmpty(result.Name))
                {
                    throw new Exception("The returned snapshot name is empty");
                }
                snapshotName = result.Name;
            }
            m_StateManager.ProgressState.UpdateProgressValue();
            m_StateManager.ProgressState.UpdateProgressMessage($"Downloading snapshot...");

            //
            // Download the snapshot locally.
            //
            // TODO: Hopefully either SK (via http api) or qdrant-dotnet (via grpc) will
            // add the ability to actually download the generated snapshots.. ?!
            //
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(@$"http://{m_HostUri}:6333");
                var uri = $"collections/{Name}/snapshots/" +
                        $"{snapshotName}";
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"HTTP status code is {response.StatusCode}");
                }
                var content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var target = Path.Combine(OutputPath, snapshotName);
                File.WriteAllBytes(target, content);
            }

            m_StateManager.ProgressState.UpdateProgressValue();
        }

        private async Task RestoreCollectionInternal(string Name, string InputPath)
        {
            m_StateManager.ProgressState.UpdateProgressMessage($"Uploading snapshot....");

            //
            // TODO: Hopefully either SK (via http api) or qdrant-dotnet (via grpc) will
            // add the ability to actually upload the snapshots for restore.. ?!
            //
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri($@"http://{m_HostUri}:6333");
                var uri = $"collections/{Name}/snapshots/" +
                    $"upload?priority=snapshot"; // priority overrides local data
                var snapshotData = File.ReadAllBytes(InputPath);
                var content = new MultipartFormDataContent()
                    {
                        {new ByteArrayContent(snapshotData), "snapshot"}
                    };
                var response = await httpClient.PostAsync(uri, content).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"HTTP status code is {response.StatusCode}");
                }
            }

            m_StateManager.ProgressState.UpdateProgressValue();
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal class QdrantVecDbEtwSearchPlugin
    {
        private readonly Kernel m_Kernel;

        public QdrantVecDbEtwSearchPlugin(Kernel _Kernel)
        {
            m_Kernel = _Kernel;
        }

        [KernelFunction("SearchEtwProviderManifests")]
        [Description("Search ETW provider manifests for information similar to the given query. A typical use of this search routine is to locate" +
            "a particular ETW provider given parameters that relate to information unique to that provider such as event names, tasks, opcodes, "+
            "keywords, channels or template fields.")]
        public async Task<string> SearchEtwProviderManifestsAsync(string query)
        {
            var store = m_Kernel.GetRequiredService<IVectorStore>();

            //
            // Retrieve the collection, if it exists
            //
            var collection = store.GetCollection<ulong, EtwProviderManifestRecord>(s_ManifestCollectionName,
                s_EtwProviderManifestRecordDefinition);
            if (!await collection.CollectionExistsAsync())
            {
                throw new InvalidOperationException($"Collection {s_ManifestCollectionName} does not exist");
            }

            //
            // Perform vector similarity search
            //
            var embedding = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var searchVector = await embedding.GenerateEmbeddingAsync(query);
            var searchOptions = new VectorSearchOptions
            {
                VectorPropertyName = nameof(EtwProviderManifestRecord.DescriptionEmbedding)
            };
            var searchResult = await collection.VectorizedSearchAsync(searchVector, searchOptions);
            await foreach (var item in searchResult.Results)
            {
                return item.Record.Description;
            }
            return string.Empty;
        }
    }
}
