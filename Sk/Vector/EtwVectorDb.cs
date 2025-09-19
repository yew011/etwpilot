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
using ControlzEx.Behaviors;
using EtwPilot.Utilities;
using EtwPilot.ViewModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using System.IO;
using System.Net;
using System.Net.Http;

namespace EtwPilot.Sk.Vector
{
    internal class EtwVectorDbService
    {
        private int m_Dimensions;
        private string s_EtwProviderManifestCollectionName;
        private string s_EtwEventCollectionName;
        private QdrantVectorStore m_QdrantStore;        
        private readonly string m_ClientUri; // needed for HttpClient requests
        private QdrantClient m_Client;
        public bool m_Initialized { get; set; }
        private IEmbeddingGenerator<string, Embedding<float>> m_EmbeddingGenerator;

        private readonly static int s_GrpcTimeoutSec = 60 * 5; // 5 min

        public EtwVectorDbService(string clientUri)
        {
            m_ClientUri = clientUri;
        }

        public async Task InitializeAsync(IKernelBuilder Builder, CancellationToken Token)
        {
            var progress = GlobalStateViewModel.Instance.g_InsightsViewModel.ProgressState;
            using var progressContext = progress.CreateProgressContext(4, $"Initializing vector store service...");
            //
            // Create client and test connection to the Qdrant server.
            //
            progress.UpdateProgressMessage($"Initializing qdrant host {m_ClientUri} vector db...");
            m_Client = new QdrantClient(m_ClientUri, grpcTimeout: TimeSpan.FromSeconds(s_GrpcTimeoutSec));
            var health = await m_Client.HealthAsync(Token);
            progress.UpdateProgressMessage($"{health.Title} version {health.Version} commit {health.Commit}");
            m_QdrantStore = new QdrantVectorStore(m_Client, false, new() { HasNamedVectors = true });
            //
            // Determine the vector dimension size of the selected embeddings model.
            // This is required to setup our vector record representations.
            //
            var tmpKernel = Builder.Build();
            m_EmbeddingGenerator = tmpKernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            var text = "This is a test sentence.";
            var embeddings = await m_EmbeddingGenerator.GenerateAsync(new[] { text }, cancellationToken:Token);
            var embedding = embeddings.First();  // Get the first embedding
            m_Dimensions = embedding.Dimensions;  // Number of elements in the vector
            //
            // Collections are named by their vector dimensions, so that the embeddings model
            // chosen by the user will match a collection with those dimensions.
            //
            s_EtwProviderManifestCollectionName = $"manifests_{m_Dimensions}";
            s_EtwEventCollectionName = $"events_{m_Dimensions}";
            //
            // Create collections if needed.
            //
            var collections = m_QdrantStore.ListCollectionNamesAsync(Token).ToBlockingEnumerable().ToList();
            if (collections == null || !collections.Contains(s_EtwProviderManifestCollectionName))
            {
                var collection = GetCollection<Guid, EtwProviderManifestRecord>(s_EtwProviderManifestCollectionName);
                await collection.EnsureCollectionExistsAsync(Token);
            }
            if (collections == null || !collections.Contains(s_EtwEventCollectionName))
            {
                var collection = GetCollection<Guid, EtwEventRecord>(s_EtwEventCollectionName);
                await collection.EnsureCollectionExistsAsync(Token);
            }
            m_Initialized = true;
            Builder.Services.AddSingleton(this);
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
                        Definition = EtwProviderManifestRecord.GetRecordDefinition(m_Dimensions, m_EmbeddingGenerator),
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
                        Definition = EtwEventRecord.GetRecordDefinition(m_Dimensions, m_EmbeddingGenerator),
                    }) as QdrantCollection<TKey, TRecord>;
                return collection!;
            }
            throw new NotImplementedException();
        }

        public string GetCollectionName<T>() where T : class
        {
            return typeof(T) switch
            {
                Type t when t == typeof(EtwEventRecord) => s_EtwEventCollectionName,
                Type t when t == typeof(EtwProviderManifestRecord) => s_EtwProviderManifestCollectionName,
                _ => throw new InvalidOperationException($"No collection name defined for type {typeof(T).Name}")
            };
        }

        public async Task SaveCollectionAsync<T>(string OutputPath, CancellationToken Token) where T: class
        {
            var collectionName = GetCollectionName<T>();
            var collection = GetCollection<Guid, T>(collectionName);
            if (!await collection.CollectionExistsAsync(Token))
            {
                throw new InvalidOperationException($"Collection {collectionName} does not exist");
            }

            //
            // Generate the snapshot on the qdrant node's local storage
            //
            var snapshotName = "";
            var result = await m_Client.CreateSnapshotAsync(collectionName, cancellationToken: Token);
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
                var uri = $"collections/{collectionName}/snapshots/" +
                        $"{snapshotName}";
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                var response = await httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken: Token).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"HTTP status code is {response.StatusCode}");
                }
                var target = Path.Combine(OutputPath, snapshotName);
                using var downloadStream = await response.Content.ReadAsStreamAsync(Token).ConfigureAwait(false);
                using var fileStream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
                await downloadStream.CopyToAsync(fileStream, cancellationToken: Token).ConfigureAwait(false);
            }
        }

        public async Task RestoreCollectionAsync<T>(string InputPath, CancellationToken Token) where T: class
        {
            var collectionName = GetCollectionName<T>();
            var collection = GetCollection<Guid, T>(collectionName);            
            //
            // TODO: Hopefully either SK (via http api) or qdrant-dotnet (via grpc) will
            // add the ability to actually upload the snapshots for restore.. ?!
            //
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri($@"http://{m_ClientUri}:6333");
                var uri = $"collections/{collectionName}/snapshots/" +
                    $"upload?priority=snapshot"; // priority overrides local data
                var snapshotData = File.ReadAllBytes(InputPath);
                var content = new MultipartFormDataContent()
                    {
                        {new ByteArrayContent(snapshotData), "snapshot"}
                    };
                var response = await httpClient.PostAsync(uri, content, cancellationToken: Token).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"HTTP status code is {response.StatusCode}");
                }
            }
        }

        public async Task<ulong> GetRecordCountAsync<T>(
            CancellationToken Token
            ) where T: class
        {
            var collectionName = GetCollectionName<T>();
            var collection = GetCollection<Guid, T>(collectionName);
            if (!await collection.CollectionExistsAsync(Token))
            {
                throw new InvalidOperationException($"Collection {collectionName} does not exist");
            }

            //
            // Oddly, SK's QdrantCollection does not have a CountAsync method, so we'll use the
            // qdrant client directly.
            //
            return await m_Client.CountAsync(collectionName, cancellationToken:Token);
        }

        public async Task<List<T>?> GetAsync<T>(
            CancellationToken Token,
            int Top = -1
            ) where T: class
        {
            var collectionName = GetCollectionName<T>();
            var collection = GetCollection<Guid, T>(collectionName);
            if (!await collection.CollectionExistsAsync(Token))
            {
                throw new InvalidOperationException($"Collection {collectionName} does not exist");
            }

            int top = Top;
            if (top <= 0)
            {
                var max = await GetRecordCountAsync<T>(Token);
                if (max == 0)
                {
                    return null;
                }
                top = (int)max;
            }
            int batchSize = 1000;
            if (top < batchSize)
            {
                batchSize = top;
            }

            //
            // Qdrant is not well suited for keyword searches, it's built for vector searches.
            // SK's qdrant connector has a Search and HybridSearch, both of which require a vector
            // or a search value that is translated to a vector via embeddings.
            // So if we just want to "Get" some records we can:
            //   1. Call Collection.GetAsync with a filter that always returns true.
            //      SK has extremely poor support for converting a linq expression to a qdrant
            //      filter - in fact, I can't even get it to work. GetAsync requires a filter.
            //   2. Call ScrollAsync using Qdrant client directly and page through the results.
            //      We have no convenient way to map returned qdrant point data to our internal
            //      record types - SK does this for us with record mappers, but they're all private.
            //   3. Call Collection.SearchAsync with a "neutral vector" that essentially is a no-op.
            // #3 is the only viable option at this time.
            //
            var searchOptions = new VectorSearchOptions<T>();
            return await Paginate(
                Collection: collection,
                SearchOptions: searchOptions,
                Top: top,
                ScoreThreshold: 0,
                SearchFunction: () =>
                    collection.SearchAsync(
                            searchValue: GetNeutralVector(),
                            top: top,
                            options: searchOptions,
                            cancellationToken: Token
                        ),
                Token
            );
        }

        public async Task<List<T>?> SearchAsync<T>(
            string Query,
            int Top,
            int ScoreThreshold,  // 0 - 100
            VectorSearchOptions<T> Options,
            CancellationToken Token
            ) where T : class
        {
            var collectionName = GetCollectionName<T>();
            var collection = GetCollection<Guid, T>(collectionName);
            if (!await collection.CollectionExistsAsync(Token))
            {
                throw new InvalidOperationException($"Collection {collectionName} does not exist");
            }
            return await Paginate(
                Collection: collection,
                SearchOptions: Options,
                Top: Top,
                ScoreThreshold: ScoreThreshold,
                SearchFunction: () =>
                    collection.SearchAsync(
                            searchValue: Query,
                            top: Top,
                            options: Options,
                            cancellationToken: Token
                        ),
                Token);
        }

        public async Task<List<T>?> SearchAsync<T, TInput>(
            TInput VectorSearchValue,
            int Top,
            int ScoreThreshold,  // 0 - 100
            VectorSearchOptions<T> Options,
            CancellationToken Token
            ) where T : class where TInput : notnull
        {
            var collectionName = GetCollectionName<T>();
            var collection = GetCollection<Guid, T>(collectionName);
            if (!await collection.CollectionExistsAsync(Token))
            {
                throw new InvalidOperationException($"Collection {collectionName} does not exist");
            }
            return await Paginate(
                Collection: collection,
                SearchOptions: Options,
                Top: Top,
                ScoreThreshold: ScoreThreshold,
                SearchFunction: () =>
                    collection.SearchAsync(
                            searchValue: VectorSearchValue,
                            top: Top,
                            options: Options,
                            cancellationToken: Token
                        ),
                Token);
        }

        public async Task<List<T>?> HybridSearchAsync<T, TInput>(
            TInput VectorSearchValue,
            List<string> Keywords,
            int Top,
            int ScoreThreshold,  // 0 - 100
            HybridSearchOptions<T> Options,
            CancellationToken Token
            ) where T : class where TInput : notnull
        {
            var collectionName = GetCollectionName<T>();
            var collection = GetCollection<Guid, T>(collectionName);
            if (!await collection.CollectionExistsAsync(Token))
            {
                throw new InvalidOperationException($"Collection {collectionName} does not exist");
            }
            return await Paginate(
                Collection: collection,
                SearchOptions: Options,
                Top: Top,
                ScoreThreshold: ScoreThreshold,
                SearchFunction: () =>
                    collection.HybridSearchAsync(
                            searchValue: VectorSearchValue,
                            keywords: Keywords,
                            top: Top,
                            options: Options,
                            cancellationToken: Token
                        ),
                Token);
        }

        public ReadOnlyMemory<float> GetNeutralVector()
        {
            return new ReadOnlyMemory<float>(new float[m_Dimensions]);
        }

        public async Task EraseAsync<T>(CancellationToken Token) where T: class
        {
            var collectionName = GetCollectionName<T>();
            var collection = GetCollection<Guid, T>(collectionName);
            if (!await collection.CollectionExistsAsync(Token))
            {
                return;
            }
            //
            // Qdrant does not have a "delete all records" operation, so we drop and recreate the collection.
            //
            await collection.EnsureCollectionDeletedAsync(Token);
            await collection.EnsureCollectionExistsAsync(Token);
        }

        public async Task ImportDataAsync<T, U>(
            List<T> Data,   // eg, T = ParsedEtwEvent, U = EtwEventRecord
            CancellationToken Token,
            ProgressState Progress
            ) where T : class where U : class, IEtwRecord<T>
        {
            using var progressContext = Progress.CreateProgressContext(Data.Count, $"Importing data...");
            var recordNumber = 1;

            var collectionName = GetCollectionName<U>();
            var collection = GetCollection<Guid, U>(collectionName);
            if (!await collection.CollectionExistsAsync(Token))
            {
                throw new InvalidOperationException($"Collection {collectionName} does not exist");
            }

            foreach (var item in Data)
            {
                if (Token.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }
                Progress.UpdateProgressMessage($"Importing vector data (record {recordNumber++} of {Data.Count})...");
                var record = U.Create<U>();
                record.Build(item);
                await collection.UpsertAsync(record, cancellationToken: Token);
                Progress.UpdateProgressValue();
            }
        }

        private async Task<List<T>> Paginate<T>(
            QdrantCollection<Guid, T> Collection,
            dynamic SearchOptions, // can be VectorSearchOptions<T> or HybridSearchOptions<T>
            int Top,
            int ScoreThreshold,  // 0 - 100 (0 = all)
            Func<IAsyncEnumerable<VectorSearchResult<T>>> SearchFunction,
            CancellationToken Token
            ) where T : class
        {
            bool keepGoing = true;
            var records = new List<T>();
            SearchOptions.Skip = 0;
            while (keepGoing)
            {
                var results = SearchFunction();
                var retrieved = 0;
                if (Token.IsCancellationRequested)
                {
                    break;
                }
                await foreach (var result in results)
                {
                    retrieved++;
                    if (ScoreThreshold > 0)
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
                        continue;
                    }
                    records.Add(result.Record);
                }
                SearchOptions.Skip += retrieved;
                keepGoing = (retrieved == Top && records.Count < Top);
            }
            return records;
        }
    }
}
