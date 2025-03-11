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
using EtwPilot.Sk.Vector;
using EtwPilot.ViewModel;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

//
// Remove supression after SK vector store is out of alpha
//
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0020

namespace EtwPilot.Sk.Vector.EtwProviderManifest
{
    internal class ManifestCollection : QdrantCollection<EtwProviderManifestRecord>
    {
        //
        // BGE (BERT) micro v2 (https://huggingface.co/TaylorAI/bge-micro-v2)
        //
        private static readonly int s_Dimensions = 384;
        private static readonly VectorStoreRecordDefinition s_RecordDefinition =
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
                            Dimensions = s_Dimensions,
                            IndexKind = IndexKind.Hnsw,
                            DistanceFunction = DistanceFunction.CosineSimilarity
                        },
                    }
            };
        public static readonly string s_Name = "manifests";

        public ManifestCollection(Kernel kernel, ProgressState progress, string hostUri) :
            base(s_RecordDefinition, kernel, progress, hostUri, s_Name)
        {

        }

        public override async Task<EtwProviderManifestRecord> CreateRecord<T>(
            T Object,
            ITextEmbeddingGenerationService EmbeddingService
            )
        {
            var manifest = Object as ParsedEtwManifest;
            if (manifest == null)
            {
                throw new Exception("Unrecognized input object type for EtwProviderManifestRecord");
            }
            return await EtwProviderManifestRecord.CreateFromParsedEtwManifest(manifest, EmbeddingService);
        }
    }
}
