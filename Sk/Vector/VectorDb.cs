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
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using Microsoft.Extensions.VectorData;
using System.IO;
using System.Net.Http;
using System.Net;
using Microsoft.SemanticKernel.Embeddings;
using etwlib;
using EtwPilot.ViewModel;

//
// Remove supression after SK vector store is out of alpha
//
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0020

namespace EtwPilot.Sk.Vector
{
    internal class EtwVectorDb : QdrantVectorStore
    {
        private int m_Dimensions;
        public static readonly string s_EtwProviderManifestCollectionName = "manifests";
        public static readonly string s_EtwEventCollectionName = "events";

        private readonly string m_ClientUri; // needed for HttpClient requests
        private readonly QdrantClient m_Client;
        public bool m_Initialized { get; set; }

        public EtwVectorDb(QdrantClient Client,
            string clientUri,
            int dimensions) : base(Client, new() { HasNamedVectors = true })
        {
            m_Client = Client;
            m_ClientUri = clientUri;
            m_Dimensions = dimensions;
        }

        public async Task Initialize()
        {
            //
            // Create collections if needed
            //
            var collections = ListCollectionNamesAsync().ToBlockingEnumerable().ToList();
            if (collections == null || !collections.Contains(s_EtwProviderManifestCollectionName))
            {
                var collection = GetCollection<ulong, EtwProviderManifestRecord>(s_EtwProviderManifestCollectionName);
                await collection.CreateCollectionIfNotExistsAsync();
            }
            if (collections == null || !collections.Contains(s_EtwEventCollectionName))
            {
                var collection = GetCollection<ulong, EtwEventRecord>(s_EtwEventCollectionName);
                await collection.CreateCollectionIfNotExistsAsync();
            }
            m_Initialized = true;
        }

        #region IVectorStore
        public override IVectorStoreRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
            string name,
            VectorStoreRecordDefinition? vectorStoreRecordDefinition = null
            )
        {
            //
            // This routine is invoked whenever we need to retrieve an IVectorStoreRecordCollection
            // interface in order to invoke collection-related operations like UpsertAsync. Note that
            // the QdrantVectorStoreRecordCollection class is a wrapper class for Qdrant related
            // functionality, it does not actually map to an existing collection on the qdrant node,
            // necessarily (unless CreateAsync is invoked).
            //
            if (s_EtwProviderManifestCollectionName == name && typeof(TRecord) == typeof(EtwProviderManifestRecord))
            {
                var customCollection = new QdrantVectorStoreRecordCollection<EtwProviderManifestRecord>(
                    m_Client,
                    name,
                    new()
                    {
                        HasNamedVectors = true,
                        PointStructCustomMapper = new EtwProviderManifestRecordMapper(),
                        VectorStoreRecordDefinition = new VectorStoreRecordDefinition
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
                                    Dimensions = m_Dimensions,
                                    IndexKind = IndexKind.Hnsw,
                                    DistanceFunction = DistanceFunction.CosineSimilarity
                                },
                            }
                        }
                    }) as IVectorStoreRecordCollection<TKey, TRecord>;
                return customCollection!;
            }
            else if (s_EtwEventCollectionName == name && typeof(TRecord) == typeof(EtwEventRecord))
            {
                var customCollection = new QdrantVectorStoreRecordCollection<EtwEventRecord>(
                    m_Client,
                    name,
                    new()
                    {
                        HasNamedVectors = true,
                        PointStructCustomMapper = new EtwEventRecordMapper(),
                        VectorStoreRecordDefinition = new VectorStoreRecordDefinition
                        {
                            Properties = new List<VectorStoreRecordProperty>
                            {
                                new VectorStoreRecordKeyProperty("Id", typeof(Guid)),
                                new VectorStoreRecordDataProperty("EventJson", typeof(string)),
                                new VectorStoreRecordDataProperty("Description", typeof(string)) { IsFilterable = true, IsFullTextSearchable = true },
                                new VectorStoreRecordVectorProperty("DescriptionEmbedding", typeof(ReadOnlyMemory<float>)) {
                                    Dimensions = m_Dimensions,
                                    IndexKind = IndexKind.Hnsw,
                                    DistanceFunction = DistanceFunction.CosineSimilarity
                                },
                            }
                        }
                    }) as IVectorStoreRecordCollection<TKey, TRecord>;
                return customCollection!;
            }
            throw new NotImplementedException();
        }
        #endregion

        public async Task SaveCollection(string CollectionName, string OutputPath, CancellationToken Token)
        {
            var collection = GetCollection<ulong, EtwEventRecord>(CollectionName);
            if (!await collection.CollectionExistsAsync())
            {
                throw new InvalidOperationException($"Collection {CollectionName} does not exist");
            }

            //
            // Generate the snapshot on the qdrant node's local storage
            //
            var snapshotName = "";
            var result = await m_Client.CreateSnapshotAsync(CollectionName);
            if (string.IsNullOrEmpty(result.Name))
            {
                throw new Exception("The returned snapshot name is empty");
            }
            snapshotName = result.Name;

            //
            // Download the snapshot locally.
            //
            // TODO: Hopefully either SK (via http api) or qdrant-dotnet (via grpc) will
            // add the ability to actually download the generated snapshots.. ?!
            //
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(@$"http://{m_ClientUri}:6333");
                var uri = $"collections/{CollectionName}/snapshots/" +
                        $"{snapshotName}";
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"HTTP status code is {response.StatusCode}");
                }
                var target = Path.Combine(OutputPath, snapshotName);
                using var downloadStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var fileStream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
                await downloadStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }
        }

        public async Task RestoreCollection(string CollectionName, string InputPath, CancellationToken Token)
        {
            //
            // TODO: Hopefully either SK (via http api) or qdrant-dotnet (via grpc) will
            // add the ability to actually upload the snapshots for restore.. ?!
            //
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri($@"http://{m_ClientUri}:6333");
                var uri = $"collections/{CollectionName}/snapshots/" +
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
        }

        public async Task<ulong> GetRecordCount(string CollectionName, CancellationToken Token)
        {
            if (CollectionName == s_EtwEventCollectionName)
            {
                var collection = GetCollection<ulong, EtwEventRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    return 0;
                }
            }
            else if (CollectionName == s_EtwProviderManifestCollectionName)
            {
                var collection = GetCollection<ulong, EtwProviderManifestRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    return 0;
                }
            }
            else
            {
                throw new InvalidOperationException($"Unknown collection {CollectionName}");
            }
            return await m_Client.CountAsync(CollectionName);
        }

        public async Task<VectorSearchResults<T>?> Search<T>(
            string CollectionName,
            string Query,
            ITextEmbeddingGenerationService EmbeddingService,
            VectorSearchOptions<T> Options,
            CancellationToken Token
            )
        {
            if (CollectionName == s_EtwEventCollectionName)
            {
                var collection = GetCollection<ulong, EtwEventRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    throw new InvalidOperationException($"Collection {CollectionName} does not exist");
                }
                var options = Options as VectorSearchOptions<EtwEventRecord>;
                var searchVector = await EmbeddingService.GenerateEmbeddingAsync(Query);
                return await collection.VectorizedSearchAsync(searchVector, options, Token) as VectorSearchResults<T>;
            }
            else if (CollectionName == s_EtwProviderManifestCollectionName)
            {
                var collection = GetCollection<ulong, EtwProviderManifestRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    throw new InvalidOperationException($"Collection {CollectionName} does not exist");
                }
                var options = Options as VectorSearchOptions<EtwProviderManifestRecord>;
                var searchVector = await EmbeddingService.GenerateEmbeddingAsync(Query);
                return await collection.VectorizedSearchAsync(searchVector, options, Token) as VectorSearchResults<T>;
            }
            throw new InvalidOperationException($"Unknown collection {CollectionName}");
        }

        public async Task Erase(string CollectionName, CancellationToken Token)
        {
            if (CollectionName == s_EtwEventCollectionName)
            {
                var collection = GetCollection<ulong, EtwEventRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    return;
                }
                await collection.DeleteCollectionAsync();
            }
            else if (CollectionName == s_EtwProviderManifestCollectionName)
            {
                var collection = GetCollection<ulong, EtwProviderManifestRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    return;
                }
                await collection.DeleteCollectionAsync();
            }
            throw new InvalidOperationException($"Unknown collection {CollectionName}");
        }

        public async Task ImportData(
            string CollectionName,
            dynamic Data,
            ITextEmbeddingGenerationService EmbeddingService,
            CancellationToken Token,
            ProgressState? Progress = null
            )
        {
            var oldProgressValue = Progress?.ProgressValue;
            var oldProgressMax = Progress?.ProgressMax;
            if (Progress != null)
            {
                Progress.ProgressMax = Data.Count;
                Progress.ProgressValue = 0;
            }
            var recordNumber = 1;

            try
            {
                if (CollectionName == s_EtwEventCollectionName)
                {
                    var collection = GetCollection<ulong, EtwEventRecord>(CollectionName);
                    if (!await collection.CollectionExistsAsync())
                    {
                        throw new InvalidOperationException($"Collection {CollectionName} does not exist");
                    }
                    foreach (var item in Data)
                    {
                        if (Token.IsCancellationRequested)
                        {
                            throw new OperationCanceledException();
                        }
                        Progress?.UpdateProgressMessage($"Importing vector data (record {recordNumber++} of {Data.Count})...");
                        var evt = item as ParsedEtwEvent;
                        var record = await EtwEventRecord.CreateFromParsedEtwEvent(evt!, EmbeddingService);
                        await collection.UpsertAsync(record, cancellationToken: Token);
                        Progress?.UpdateProgressValue();
                    }
                }
                else if (CollectionName == s_EtwProviderManifestCollectionName)
                {
                    var collection = GetCollection<ulong, EtwProviderManifestRecord>(CollectionName);
                    if (!await collection.CollectionExistsAsync())
                    {
                        throw new InvalidOperationException($"Collection {CollectionName} does not exist");
                    }
                    foreach (var item in Data)
                    {
                        if (Token.IsCancellationRequested)
                        {
                            throw new OperationCanceledException();
                        }
                        Progress?.UpdateProgressMessage($"Importing vector data (record {recordNumber++} of {Data.Count})...");
                        var manifest = item as ParsedEtwManifest;
                        var record = await EtwProviderManifestRecord.CreateFromParsedEtwManifest(
                            manifest!, EmbeddingService);
                        await collection.UpsertAsync(record, cancellationToken: Token);
                        Progress?.UpdateProgressValue();
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unknown collection {CollectionName}");
                }
            }
            finally
            {
                if (Progress != null)
                {
                    Progress.ProgressMax = oldProgressMax!.Value;
                    Progress.ProgressValue = oldProgressValue!.Value;
                }
            }
        }
    }
}
