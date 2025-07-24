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
using Microsoft.Extensions.AI;

namespace EtwPilot.Sk.Vector
{
    internal class EtwProviderManifestRecord : IEtwRecord<ParsedEtwManifest>
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string ProviderId { get; set; }
        public string ManifestJson { get; set; }
        public string Embedding { get; set; }
        public List<string> Keywords { get; set; }

        public EtwProviderManifestRecord()
        {
            Id = Guid.NewGuid();
        }

        public static TRecord Create<TRecord>() where TRecord : class, IEtwRecord<ParsedEtwManifest>
        {
            return new EtwProviderManifestRecord() as TRecord ?? throw new InvalidOperationException("Failed to create EtwProviderManifestRecord");
        }

        public static VectorStoreCollectionDefinition GetRecordDefinition(int Dimensions, IEmbeddingGenerator<string, Embedding<float>> Generator)
        {
            return new VectorStoreCollectionDefinition()
            {
                Properties = new List<VectorStoreProperty>
                {
                    new VectorStoreKeyProperty("Id", typeof(Guid)),
                    new VectorStoreDataProperty("Name", typeof(string)) { IsIndexed = true },
                    new VectorStoreDataProperty("ProviderId", typeof(string)) { IsIndexed = true },
                    new VectorStoreDataProperty("ManifestJson", typeof(string)) { IsIndexed = true, IsFullTextIndexed = true },
                    new VectorStoreDataProperty("Keywords", typeof(List<string>)) { IsIndexed = true },
                    new VectorStoreVectorProperty("Embedding", typeof(string), Dimensions) {
                        IndexKind = IndexKind.Hnsw,
                        DistanceFunction = DistanceFunction.CosineSimilarity,
                        EmbeddingGenerator = Generator
                    },
                }
            };
        }

        public void Build(ParsedEtwManifest Manifest)
        {
            ProviderId = Manifest.Provider.Id.ToString();
            Name = string.IsNullOrEmpty(Manifest.Provider.Name) ? "(unnamed)" : Manifest.Provider.Name;
            ManifestJson = JsonConvert.SerializeObject(Manifest);
            Embedding = ManifestJson;
            Keywords = new List<string>();
            Keywords.Add(Manifest.Provider.Id.ToString());
            Keywords.Add(Name);
            Keywords.AddRange(Manifest.Channels.Select(e => e.Name).Where(k => !string.IsNullOrEmpty(k)));
            Keywords.AddRange(Manifest.Channels.Select(e => e.Description).Where(k => !string.IsNullOrEmpty(k)));
            Keywords.AddRange(Manifest.Keywords.Select(e => e.Name).Where(k => !string.IsNullOrEmpty(k)));
            Keywords.AddRange(Manifest.Keywords.Select(e => e.Description).Where(k => !string.IsNullOrEmpty(k)));
            Keywords.AddRange(Manifest.Tasks.Select(kvp => kvp.Key.Name).ToList());
            Keywords.AddRange(Manifest.Tasks.Select(kvp => kvp.Value.Select(v => v.Name)).SelectMany(
                v => v).Where(k => !string.IsNullOrEmpty(k)));
            Keywords.AddRange(Manifest.GlobalOpcodes.Select(e => e.Name).Where(k => !string.IsNullOrEmpty(k)));
            Keywords.AddRange(Manifest.Templates.Select(kvp => kvp.Key).ToList());
            Keywords.AddRange(Manifest.Templates.Select(kvp => kvp.Value.Select(v => v.Name)).SelectMany(
                v => v).Where(k => !string.IsNullOrEmpty(k)));
            Keywords.AddRange(Manifest.StringTable);
            Keywords = Keywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        }
    }
}
