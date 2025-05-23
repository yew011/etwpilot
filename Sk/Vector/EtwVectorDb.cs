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
using etwlib;
using EtwPilot.ViewModel;
using Microsoft.SemanticKernel;

namespace EtwPilot.Sk.Vector
{
    internal class EtwVectorDb
    {
        private int m_Dimensions;
        public readonly string s_EtwProviderManifestCollectionName;
        public readonly string s_EtwEventCollectionName;

        private QdrantVectorStore m_QdrantStore;
        private readonly Kernel m_Kernel;

        private readonly string m_ClientUri; // needed for HttpClient requests
        private readonly QdrantClient m_Client;
        public bool m_Initialized { get; set; }

        public EtwVectorDb(QdrantClient Client, string clientUri, int dimensions, Kernel _Kernel)
        {
            m_QdrantStore = new QdrantVectorStore(Client, false, new() { HasNamedVectors = true });
            m_Client = Client;
            m_ClientUri = clientUri;
            m_Dimensions = dimensions;
            m_Kernel = _Kernel;

            //
            // Collections are named by their vector dimensions, so that the embeddings model
            // chosen by the user will match a collection with those dimensions.
            //
            s_EtwProviderManifestCollectionName = $"manifests_{dimensions}";
            s_EtwEventCollectionName = $"events_{dimensions}";
        }

        public async Task Initialize()
        {
            //
            // Create collections if needed.
            //
            var collections = m_QdrantStore.ListCollectionNamesAsync().ToBlockingEnumerable().ToList();
            if (collections == null || !collections.Contains(s_EtwProviderManifestCollectionName))
            {
                var collection = GetCollection<Guid, EtwProviderManifestRecord>(s_EtwProviderManifestCollectionName);
                await collection.EnsureCollectionExistsAsync();
            }
            if (collections == null || !collections.Contains(s_EtwEventCollectionName))
            {
                var collection = GetCollection<Guid, EtwEventRecord>(s_EtwEventCollectionName);
                await collection.EnsureCollectionExistsAsync();
            }
            m_Initialized = true;
        }

        public QdrantCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name)
            where TKey : notnull
            where TRecord : class
        {
            if (s_EtwProviderManifestCollectionName == name && typeof(TRecord) == typeof(EtwProviderManifestRecord))
            {
                var collection = new QdrantCollection<Guid, EtwProviderManifestRecord>(
                    m_Client,
                    name,
                    false,
                    new()
                    {
                        HasNamedVectors = true,
                        Definition = EtwProviderManifestRecord.GetRecordDefinition(m_Dimensions, m_Kernel),
                    }) as QdrantCollection<TKey, TRecord>;
                return collection!;
            }
            else if (s_EtwEventCollectionName == name && typeof(TRecord) == typeof(EtwEventRecord))
            {
                var collection = new QdrantCollection<Guid, EtwEventRecord>(
                    m_Client,
                    name,
                    false,
                    new()
                    {
                        HasNamedVectors = true,
                        Definition = EtwEventRecord.GetRecordDefinition(m_Dimensions, m_Kernel),
                    }) as QdrantCollection<TKey, TRecord>;
                return collection!;
            }
            throw new NotImplementedException();
        }

        public async Task SaveCollection(string CollectionName, string OutputPath, CancellationToken Token)
        {
            var collection = GetCollection<Guid, EtwEventRecord>(CollectionName);
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
                var collection = GetCollection<Guid, EtwEventRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    return 0;
                }
            }
            else if (CollectionName == s_EtwProviderManifestCollectionName)
            {
                var collection = GetCollection<Guid, EtwProviderManifestRecord>(CollectionName);
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

        public async Task<List<T>?> Search<T>(
            string CollectionName,
            string Query,
            int Top,
            int ScoreThreshold,  // 0 - 100
            VectorSearchOptions<T> Options,
            CancellationToken Token
            )
        {
            var top = Top;
            if (top < 0)
            {
                top = 100000; // something large...
            }
            if (CollectionName == s_EtwEventCollectionName)
            {
                var collection = GetCollection<Guid, EtwEventRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    throw new InvalidOperationException($"Collection {CollectionName} does not exist");
                }
                var options = Options as VectorSearchOptions<EtwEventRecord>;
                var results = collection.SearchAsync(Query, Top, options, Token);
                var records = new List<EtwEventRecord>();
                await foreach (var result in results)
                {
                    if (!result.Score.HasValue)
                    {
                        continue;
                    }
                    var score = Math.Round(result.Score.Value * 100);
                    if (score >= ScoreThreshold)
                    {
                        records.Add(result.Record);
                    }
                }
                return records as List<T>;
            }
            else if (CollectionName == s_EtwProviderManifestCollectionName)
            {
                var collection = GetCollection<Guid, EtwProviderManifestRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    throw new InvalidOperationException($"Collection {CollectionName} does not exist");
                }
                var options = Options as VectorSearchOptions<EtwProviderManifestRecord>;
                var results = collection.SearchAsync(Query, Top, options, Token);
                var records = new List<EtwProviderManifestRecord>();
                await foreach (var result in results)
                {
                    if (!result.Score.HasValue)
                    {
                        continue;
                    }
                    var score = Math.Round(result.Score.Value * 100);
                    if (score >= ScoreThreshold)
                    {
                        records.Add(result.Record);
                    }
                }
                return records as List<T>;
            }
            throw new InvalidOperationException($"Unknown collection {CollectionName}");
        }

        public async Task Erase(string CollectionName, CancellationToken Token)
        {
            if (CollectionName == s_EtwEventCollectionName)
            {
                var collection = GetCollection<Guid, EtwEventRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    return;
                }
                await collection.EnsureCollectionDeletedAsync();
            }
            else if (CollectionName == s_EtwProviderManifestCollectionName)
            {
                var collection = GetCollection<Guid, EtwProviderManifestRecord>(CollectionName);
                if (!await collection.CollectionExistsAsync())
                {
                    return;
                }
                await collection.EnsureCollectionDeletedAsync();
            }
            throw new InvalidOperationException($"Unknown collection {CollectionName}");
        }

        public async Task ImportData(
            string CollectionName,
            dynamic Data,
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
                    var collection = GetCollection<Guid, EtwEventRecord>(CollectionName);
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
                        var record = new EtwEventRecord();
                        record.LoadFromParsedEtwEvent(evt!);
                        await collection.UpsertAsync(record, cancellationToken: Token);
                        Progress?.UpdateProgressValue();
                    }
                }
                else if (CollectionName == s_EtwProviderManifestCollectionName)
                {
                    var collection = GetCollection<Guid, EtwProviderManifestRecord>(CollectionName);
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
                        var record = new EtwProviderManifestRecord();
                        record.LoadFromParsedEtwManifest(manifest!);
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
