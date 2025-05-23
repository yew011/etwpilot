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
using Microsoft.Extensions.VectorData;
using Newtonsoft.Json;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;

namespace EtwPilot.Sk.Vector
{
    internal class EtwProviderManifestRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string ProviderId { get; set; }
        public string ManifestJson { get; set; }
        public string Embedding { get; set; }

        public EtwProviderManifestRecord()
        {
            Id = Guid.NewGuid();
        }

        public static VectorStoreCollectionDefinition GetRecordDefinition(int Dimensions, Kernel _Kernel)
        {
            var embeddingGenerator = _Kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

            return new VectorStoreCollectionDefinition()
            {
                Properties = new List<VectorStoreProperty>
                {
                    new VectorStoreKeyProperty("Id", typeof(Guid)),
                    new VectorStoreDataProperty("Name", typeof(string)) { IsIndexed = true },
                    new VectorStoreDataProperty("ProviderId", typeof(string)) { IsIndexed = true },
                    new VectorStoreDataProperty("ManifestJson", typeof(string)) { IsIndexed = true, IsFullTextIndexed = true },
                    new VectorStoreVectorProperty("Embedding", typeof(string), Dimensions) {
                        IndexKind = IndexKind.Hnsw,
                        DistanceFunction = DistanceFunction.CosineSimilarity,
                        EmbeddingGenerator = embeddingGenerator
                    },
                }
            };
        }

        public void LoadFromParsedEtwManifest(ParsedEtwManifest Manifest)
        {
            ProviderId = Manifest.Provider.Id.ToString();
            Name = string.IsNullOrEmpty(Manifest.Provider.Name) ? "(unnamed)" : Manifest.Provider.Name;
            ManifestJson = JsonConvert.SerializeObject(Manifest);
            Embedding = ManifestJson;
        }
    }
}
