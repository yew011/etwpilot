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
using EtwPilot.ViewModel;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

//
// Remove supression after SK vector store is out of alpha
//
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0020

namespace EtwPilot.Vector.EtwEvent
{
    internal class EventCollection : QdrantCollection<EtwEventRecord>
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
                        new VectorStoreRecordDataProperty("EventJson", typeof(string)),
                        new VectorStoreRecordDataProperty("Description", typeof(string)) { IsFilterable = true, IsFullTextSearchable = true },
                        new VectorStoreRecordVectorProperty("DescriptionEmbedding", typeof(ReadOnlyMemory<float>)) {
                            Dimensions = s_Dimensions,
                            IndexKind = IndexKind.Hnsw,
                            DistanceFunction = DistanceFunction.CosineSimilarity
                        },
                    }
            };
        public static readonly string s_Name = "events";

        public EventCollection(Kernel kernel, ProgressState progress, string hostUri) :
            base(s_RecordDefinition, kernel, progress, hostUri, s_Name)
        {

        }

        public override async Task<EtwEventRecord> CreateRecord(
            dynamic Object,
            ITextEmbeddingGenerationService EmbeddingService
            )
        {
            return await EtwEventRecord.CreateFromParsedEtwEvent(Object, EmbeddingService);
        }
    }
}
