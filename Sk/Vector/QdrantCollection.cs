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
using Qdrant.Client;
using EtwPilot.ViewModel;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using System.Net.Http;
using System.Net;
using System.IO;
using Microsoft.SemanticKernel.Embeddings;

//
// Remove supression after SK vector store is out of alpha
//
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0020

namespace EtwPilot.Sk.Vector
{
    internal abstract class QdrantCollection<T>
    {
        protected readonly Kernel m_Kernel;
        protected readonly ProgressState m_ProgressState;
        protected readonly string m_HostUri;
        protected readonly string m_Name;
        protected VectorStoreRecordDefinition m_RecordDefinition;

        public QdrantCollection(
            VectorStoreRecordDefinition recordDefinition,
            Kernel kernel,
            ProgressState progress,
            string hostUri,
            string name)
        {
            m_RecordDefinition = recordDefinition;
            m_Kernel = kernel;
            m_ProgressState = progress;
            m_HostUri = hostUri;
            m_Name = name;
        }

        public string GetName() => m_Name;

        public async Task<IVectorStoreRecordCollection<ulong, T>> Create(bool CreateIfNotExists)
        {
            var store = m_Kernel.GetRequiredService<IVectorStore>();
            var collection = store.GetCollection<ulong, T>(m_Name, m_RecordDefinition);
            if (!await collection.CollectionExistsAsync())
            {
                if (CreateIfNotExists)
                {
                    await collection.CreateCollectionIfNotExistsAsync();
                    return collection;
                }
                throw new InvalidOperationException($"Collection {m_Name} does not exist");
            }
            return collection;
        }

        public abstract Task<T> CreateRecord<T2>(T2 Object, ITextEmbeddingGenerationService EmbeddingService);

        public async Task<ulong> GetRecordCount()
        {
            IVectorStoreRecordCollection<ulong, T> collection;
            try
            {
                collection = await Create(false);
            }
            catch (Exception)
            {
                return 0;
            }

            using (var client = new QdrantClient(m_HostUri))
            {
                try
                {
                    return await client.CountAsync(m_Name);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        public async Task<VectorSearchResults<T>> VectorSearch(string Query, VectorSearchOptions Options)
        {
            var collection = await Create(false);
            var embedding = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var searchVector = await embedding.GenerateEmbeddingAsync(Query);
            return await collection.VectorizedSearchAsync(searchVector, Options);
        }

        public async Task Erase()
        {
            var collection = await Create(false);
            await collection.DeleteCollectionAsync();
            _ = await Create(true);
        }

        public async Task Import<T2>(List<T2> Data, CancellationToken Token)
        {
            int total = Data.Count;
            if (total == 0)
            {
                throw new Exception("No data provided.");
            }

            var collection = await Create(false);
            var embeddingService = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            m_ProgressState.UpdateProgressMessage($"Populating collection with {total} records...");

            foreach (var item in Data)
            {
                if (Token.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }
                var record = await CreateRecord(item, embeddingService);
                await collection.UpsertAsync(record, cancellationToken:Token);
            }

            m_ProgressState.UpdateProgressValue();
        }

        public async Task Save(string OutputPath)
        {
            var store = m_Kernel.GetRequiredService<IVectorStore>();
            var collections = store.ListCollectionNamesAsync().ToBlockingEnumerable().ToList();
            if (collections == null || !collections.Contains(m_Name))
            {
                throw new Exception("Collection doesn't exist.");
            }

            //
            // Generate the snapshot on the qdrant node's local storage
            //
            m_ProgressState.UpdateProgressMessage($"Generating snapshot....");
            var snapshotName = "";
            using (var client = new QdrantClient(m_HostUri))
            {
                var result = await client.CreateSnapshotAsync(m_Name);
                if (string.IsNullOrEmpty(result.Name))
                {
                    throw new Exception("The returned snapshot name is empty");
                }
                snapshotName = result.Name;
            }
            m_ProgressState.UpdateProgressValue();
            m_ProgressState.UpdateProgressMessage($"Downloading snapshot...");

            //
            // Download the snapshot locally.
            //
            // TODO: Hopefully either SK (via http api) or qdrant-dotnet (via grpc) will
            // add the ability to actually download the generated snapshots.. ?!
            //
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(@$"http://{m_HostUri}:6333");
                var uri = $"collections/{m_Name}/snapshots/" +
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
            m_ProgressState.UpdateProgressValue();
        }

        public async Task Restore(string InputPath)
        {
            m_ProgressState.UpdateProgressMessage($"Uploading snapshot....");

            //
            // TODO: Hopefully either SK (via http api) or qdrant-dotnet (via grpc) will
            // add the ability to actually upload the snapshots for restore.. ?!
            //
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri($@"http://{m_HostUri}:6333");
                var uri = $"collections/{m_Name}/snapshots/" +
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

            m_ProgressState.UpdateProgressValue();
        }
    }
}
