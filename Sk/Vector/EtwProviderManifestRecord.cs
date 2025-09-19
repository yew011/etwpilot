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
        public string ManifestCompressed { get; set; }
        public string Embedding { get; set; }
        public string Keywords { get; set; } // for hybrid search

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
                    new VectorStoreDataProperty("ManifestJson", typeof(string)) { IsIndexed = true },
                    new VectorStoreDataProperty("ManifestCompressed", typeof(string)) { IsIndexed = true, IsFullTextIndexed = true },
                    new VectorStoreDataProperty("Keywords", typeof(string)) { IsIndexed = true, IsFullTextIndexed = true },
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
            //
            // Vector searches are against the compressed manifest
            //
            Embedding = ManifestCompressed = EtwManifestCompressor.Compress(Manifest);
            //
            // Build keywords for hybrid search
            //
            var kw = new List<string>();
            kw.Add(Manifest.Provider.Id.ToString());
            kw.Add(Name);
            kw.AddRange(Manifest.Channels.Select(e => e.Name).Where(k => !string.IsNullOrEmpty(k)));
            kw.AddRange(Manifest.Channels.Select(e => e.Description).Where(k => !string.IsNullOrEmpty(k)));
            kw.AddRange(Manifest.Keywords.Select(e => e.Name).Where(k => !string.IsNullOrEmpty(k)));
            kw.AddRange(Manifest.Keywords.Select(e => e.Description).Where(k => !string.IsNullOrEmpty(k)));
            kw.AddRange(Manifest.Tasks.Select(kvp => kvp.Key.Name).ToList());
            kw.AddRange(Manifest.Tasks.Select(kvp => kvp.Value.Select(v => v.Name)).SelectMany(
                v => v).Where(k => !string.IsNullOrEmpty(k)));
            kw.AddRange(Manifest.GlobalOpcodes.Select(e => e.Name).Where(k => !string.IsNullOrEmpty(k)));
            kw.AddRange(Manifest.Templates.Select(kvp => kvp.Key).ToList());
            kw.AddRange(Manifest.Templates.Select(kvp => kvp.Value.Select(v => v.Name)).SelectMany(
                v => v).Where(k => !string.IsNullOrEmpty(k)));
            kw.AddRange(Manifest.StringTable);
            kw = kw.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Keywords = string.Join(' ', kw);
        }
    }
}
