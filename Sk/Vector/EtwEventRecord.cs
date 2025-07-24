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
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Newtonsoft.Json;

namespace EtwPilot.Sk.Vector
{
    internal class EtwEventRecord : IEtwRecord<ParsedEtwEvent>
    {
        public Guid Id { get; set; }
        public string ProviderName { get; set; }
        public string ProviderId { get; set; }
        public int EventId { get; set; }
        public int EventVersion { get; set; }
        public int ProcessId { get; set; }
        public long ProcessStartKey { get; set; }
        public int ThreadId { get; set; }
        public string UserSid { get; set; }
        public string ActivityId { get; set; }
        public string Timestamp { get; set; }
        public string EventJson { get; set; }
        public string Embedding { get; set; }

        public EtwEventRecord()
        {
            Id = Guid.NewGuid();
        }

        public static TRecord Create<TRecord>() where TRecord : class, IEtwRecord<ParsedEtwEvent>
        {
            return new EtwEventRecord() as TRecord ?? throw new InvalidOperationException("Failed to create EtwEventRecord");
        }

        public static VectorStoreCollectionDefinition GetRecordDefinition(
            int Dimensions,
            IEmbeddingGenerator<string, Embedding<float>> EmbeddingGenerator
            )
        {
            return new VectorStoreCollectionDefinition()
            {
                Properties = new List<VectorStoreProperty>
                {
                    new VectorStoreKeyProperty("Id", typeof(Guid)),
                    new VectorStoreDataProperty("ProviderName", typeof(string)) { IsIndexed = true },
                    new VectorStoreDataProperty("ProviderId", typeof(string)) { IsIndexed = true },
                    new VectorStoreDataProperty("EventId", typeof(int)) { IsIndexed = true },
                    new VectorStoreDataProperty("EventVersion", typeof(int)) { IsIndexed = true },
                    new VectorStoreDataProperty("ProcessId", typeof(int)) { IsIndexed = true },
                    new VectorStoreDataProperty("ProcessStartKey", typeof(long)) { IsIndexed = true },
                    new VectorStoreDataProperty("ThreadId", typeof(int)) { IsIndexed = true },
                    new VectorStoreDataProperty("UserSid", typeof(string)) { IsIndexed = true },
                    new VectorStoreDataProperty("ActivityId", typeof(string)) { IsIndexed = true },
                    new VectorStoreDataProperty("Timestamp", typeof(string)) { IsIndexed = true },
                    new VectorStoreDataProperty("EventJson", typeof(string)) { IsIndexed = true, IsFullTextIndexed = true },
                    new VectorStoreVectorProperty("Embedding", typeof(string), Dimensions) {
                        IndexKind = IndexKind.Hnsw,
                        DistanceFunction = DistanceFunction.CosineSimilarity,
                        EmbeddingGenerator = EmbeddingGenerator
                    },
                }
            };
        }

        public void Build(ParsedEtwEvent Event)
        {
            ProviderName = Event.Provider.Name!;
            ProviderId = Event.Provider.Id.ToString();
            EventId = Event.EventId;
            EventVersion = Event.Version;
            ProcessId = (int)Event.ProcessId;
            ProcessStartKey = Event.ProcessStartKey;
            ThreadId = (int)Event.ThreadId;
            UserSid = Event.UserSid!;
            ActivityId = Event.ActivityId.ToString();
            Timestamp = Event.Timestamp.ToString();
            EventJson = JsonConvert.SerializeObject(Event);
            Embedding = EventJson;
        }
    }

}
