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
using EtwPilot.ViewModel;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.VectorData;
using EtwPilot.Sk.Vector.EtwProviderManifest;
using EtwPilot.Sk.Vector.EtwEvent;

//
// Remove supression after SK vector store is out of alpha
//
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0020

namespace EtwPilot.Sk.Vector
{
    using static InsightsViewModel;

    internal class EtwVectorDb : IQdrantVectorStoreRecordCollectionFactory
    {
        private Kernel m_Kernel;
        public bool m_Initialized;
        QdrantCollection<EtwProviderManifestRecord> m_ManifestCollection;
        QdrantCollection<EtwEventRecord> m_EventCollection;

        public EtwVectorDb()
        {
        }

        public async Task Initialize(Kernel kernel, string QdrantHostUri, ProgressState progress)
        {
            m_Kernel = kernel;
            m_Initialized = true;

            m_ManifestCollection = new ManifestCollection(kernel, progress, QdrantHostUri);
            m_EventCollection = new EventCollection(kernel, progress, QdrantHostUri);

            var store = m_Kernel.GetRequiredService<IVectorStore>();
            //
            // Create collections if needed
            //
            var collections = store.ListCollectionNamesAsync().ToBlockingEnumerable().ToList();
            if (collections == null || !collections.Contains(m_ManifestCollection.GetName()))
            {
                _ = await m_ManifestCollection.Create(true);
            }
            if (collections == null || !collections.Contains(m_EventCollection.GetName()))
            {
                _ = await m_EventCollection.Create(true);
            }
            m_Initialized = true;
        }

        public IVectorStoreRecordCollection<TKey, TRecord> CreateVectorStoreRecordCollection<TKey, TRecord>(
            QdrantClient qdrantClient,
            string name,
            VectorStoreRecordDefinition? vectorStoreRecordDefinition) where TKey : notnull
        {
            if (m_ManifestCollection.GetName() == name && typeof(TRecord) == typeof(EtwProviderManifestRecord))
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
            else if (m_EventCollection.GetName() == name && typeof(TRecord) == typeof(EtwEventRecord))
            {
                var customCollection = new QdrantVectorStoreRecordCollection<EtwEventRecord>(
                    qdrantClient,
                    name,
                    new()
                    {
                        HasNamedVectors = true,
                        PointStructCustomMapper = new EtwEventRecordMapper(),
                        VectorStoreRecordDefinition = vectorStoreRecordDefinition
                    }) as IVectorStoreRecordCollection<TKey, TRecord>;
                return customCollection!;
            }
            throw new NotImplementedException();
        }

        public async Task<ulong> GetRecordCount(ChatTopic Topic)
        {
            switch (Topic)
            {
                case ChatTopic.Manifests:
                    {
                        return await m_ManifestCollection.GetRecordCount();
                    }
                case ChatTopic.EventData:
                    {
                        return await m_EventCollection.GetRecordCount();
                    }
                default:
                    {
                        return 0;
                    }
            }
        }

        public async Task<string> Search(ChatTopic Topic, string Query, VectorSearchOptions Options)
        {
            switch (Topic)
            {
                case ChatTopic.Manifests:
                    {
                        var result = await m_ManifestCollection.VectorSearch(Query, Options);
                        await foreach (var item in result.Results)
                        {
                            return item.Record.Description;
                        }
                        break;
                    }
                case ChatTopic.EventData:
                    {
                        var result = await m_EventCollection.VectorSearch(Query, Options);
                        await foreach (var item in result.Results)
                        {
                            return item.Record.Description;
                        }
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
            return string.Empty;
        }

        public async Task<string> Erase(ChatTopic Topic)
        {
            switch (Topic)
            {
                case ChatTopic.Manifests:
                    {
                        await m_ManifestCollection.Erase();
                        break;
                    }
                case ChatTopic.EventData:
                    {
                        await m_EventCollection.Erase();
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
            return string.Empty;
        }

        public async Task ImportData<T>(ChatTopic Topic, List<T> Data, CancellationToken Token)
        {
            switch (Topic)
            {
                case ChatTopic.Manifests:
                    {
                        await m_ManifestCollection.Import(Data, Token);
                        break;
                    }
                case ChatTopic.EventData:
                    {
                        await m_EventCollection.Import(Data, Token);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }        

        public async Task SaveCollection(ChatTopic Topic, string Path)
        {
            switch (Topic)
            {
                case ChatTopic.Manifests:
                    {
                        await m_ManifestCollection.Save(Path);
                        break;
                    }
                case ChatTopic.EventData:
                    {
                        await m_EventCollection.Save(Path);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

        public async Task RestoreCollection(ChatTopic Topic, string Path)
        {
            switch (Topic)
            {
                case ChatTopic.Manifests:
                    {
                        await m_ManifestCollection.Restore(Path);
                        break;
                    }
                case ChatTopic.EventData:
                    {
                        await m_EventCollection.Restore(Path);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
    }
}
