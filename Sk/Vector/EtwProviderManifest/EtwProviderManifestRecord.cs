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
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client.Grpc;

//
// Remove supression after SK vector store is out of alpha
//
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0020

namespace EtwPilot.Sk.Vector.EtwProviderManifest
{
    using static VectorHelper;

    internal class EtwProviderManifestRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Source { get; set; }
        public List<string> EventIds { get; set; }
        public List<string> Channels { get; set; }
        public List<string> Tasks { get; set; }
        public List<string> Keywords { get; set; }
        public List<string> Strings { get; set; }
        public List<string> TemplateFields { get; set; }
        public string Description { get; set; }
        public ReadOnlyMemory<float> DescriptionEmbedding { get; set; }

        public EtwProviderManifestRecord()
        {
            Id = Guid.NewGuid();
            Name = string.Empty;
            Source = string.Empty;
            EventIds = new List<string>();
            Channels = new List<string>();
            Tasks = new List<string>();
            Keywords = new List<string>();
            Strings = new List<string>();
            TemplateFields = new List<string>();
            DescriptionEmbedding = new ReadOnlyMemory<float>();
            Description = string.Empty;
        }

        public static async Task<EtwProviderManifestRecord> CreateFromParsedEtwManifest(
            ParsedEtwManifest Manifest,
            ITextEmbeddingGenerationService EmbeddingService
            )
        {
            var record = new EtwProviderManifestRecord()
            {
                Id = Manifest.Provider.Id,
                Name = string.IsNullOrEmpty(Manifest.Provider.Name) ? "(unnamed)" : Manifest.Provider.Name,
                Source = string.IsNullOrEmpty(Manifest.Provider.Source) ? "(unknown)" : Manifest.Provider.Source,
                EventIds = Manifest.Events.Select(e => e.Id).ToList(),
                Channels = Manifest.Channels.Select(c => $"{c}").ToList(),
                Keywords = Manifest.Keywords.Select(k => $"{k}").ToList(),
                //
                // Note: ignoring strings for now; they're mostly repetitive of what's
                // already captured in other aspects
                //
            };

            foreach (var kvp2 in Manifest.Tasks)
            {
                var taskName = kvp2.Key.Name;
                var taskHexvalue = kvp2.Key.Value;
                var opcodes = kvp2.Value;
                var opcodeNames = opcodes.Select(o => $"{o.Name}").ToList();
                record.Tasks.Add($"task {taskName}(value={taskHexvalue:X}) " +
                    $"has opcodes {string.Join(',', opcodeNames)}");
            }

            foreach (var kvp2 in Manifest.Templates)
            {
                var structName = kvp2.Key;
                var fields = kvp2.Value;
                record.TemplateFields.AddRange(fields.Select(f => $"{f.Name}").Where(
                    f => !record.TemplateFields.Contains(f)));
            }

            record.Description = $@"There is an ETW provider with Id={record.Id} named " +
                $"{record.Name} whose events are formatted from source {record.Source}. " +
                $"The supported events have IDs {string.Join(",", record.EventIds)} and " +
                $"among these events there are unique template fields named " +
                $"{string.Join(",", record.TemplateFields)}. The provider further defines " +
                $"channels: {string.Join(",", record.Channels)}; tasks/opcodes: " +
                $"{string.Join(",", record.Tasks)}";

            var embeddingResult = await EmbeddingService.GenerateEmbeddingAsync(record.Description);
            record.DescriptionEmbedding = embeddingResult.ToArray();
            return record;
        }

        public static EtwProviderManifestRecord CreateFromQdrantPointStruct(PointStruct Ps)
        {
            var record = new EtwProviderManifestRecord()
            {
                Id = new Guid(Ps.Id.Uuid),
                Name = Ps.Payload["ProviderName"].StringValue,
                Source = Ps.Payload["ProviderSource"].StringValue,
                Description = Ps.Payload["Description"].StringValue
            };

            var eventIds = ExtractFieldFromPayload(Ps.Payload, "EventIds");
            var channels = ExtractFieldFromPayload(Ps.Payload, "Channels");
            var tasks = ExtractFieldFromPayload(Ps.Payload, "Tasks");
            var keywords = ExtractFieldFromPayload(Ps.Payload, "Keywords");
            var strings = ExtractFieldFromPayload(Ps.Payload, "Strings");
            var templateFields = ExtractFieldFromPayload(Ps.Payload, "TemplateFields");

            if (eventIds.Count > 0)
            {
                record.EventIds.AddRange(eventIds);
            }
            if (channels.Count > 0)
            {
                record.Channels.AddRange(channels);
            }
            if (tasks.Count > 0)
            {
                record.Tasks.AddRange(tasks);
            }
            if (keywords.Count > 0)
            {
                record.Keywords.AddRange(keywords);
            }
            if (strings.Count > 0)
            {
                record.Strings.AddRange(strings);
            }
            if (templateFields.Count > 0)
            {
                record.TemplateFields.AddRange(templateFields);
            }
            return record;
        }
    }

    internal class EtwProviderManifestRecordMapper : IVectorStoreRecordMapper<EtwProviderManifestRecord, PointStruct>
    {
        public PointStruct MapFromDataToStorageModel(EtwProviderManifestRecord Record)
        {
            var ps = new PointStruct
            {
                Id = new PointId { Uuid = Record.Id.ToString() },
                Vectors = new Vectors() { },
                Payload = {
                        { "ProviderName", Record.Name },
                        { "ProviderSource", Record.Source },
                        { "EventIds", IntListAsProtoValue(Record.EventIds) },
                        { "Channels", StringListAsProtoValue(Record.Channels) },
                        { "Tasks", StringListAsProtoValue(Record.Tasks) },
                        { "Keywords", StringListAsProtoValue(Record.Keywords) },
                        { "Strings", StringListAsProtoValue(Record.Strings) },
                        { "TemplateFields", StringListAsProtoValue(Record.TemplateFields) },
                        { "Description", Record.Description },
                    },
            };
            var namedVectors = new NamedVectors();
            namedVectors.Vectors.Add("DescriptionEmbedding", Record.DescriptionEmbedding.ToArray());
            ps.Vectors.Vectors_ = namedVectors;
            return ps;
        }

        public EtwProviderManifestRecord MapFromStorageToDataModel(PointStruct Ps, StorageToDataModelMapperOptions Options)
        {
            return EtwProviderManifestRecord.CreateFromQdrantPointStruct(Ps);
        }
    }
}
