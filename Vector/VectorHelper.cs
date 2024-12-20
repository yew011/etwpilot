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
using Google.Protobuf.Collections;
using Qdrant.Client.Grpc;

namespace EtwPilot.Vector
{
    internal static class VectorHelper
    {
        public static List<string> ExtractFieldFromPayload(MapField<string, Value> Payload, string FieldName)
        {
            var values = new List<string>();
            if (Payload.ContainsKey(FieldName))
            {
                foreach (var value in Payload[FieldName].ListValue.Values)
                {
                    values.Add(value.StringValue);
                }
            }
            return values;
        }
        public static Value IntListAsProtoValue(List<string> Values)
        {
            var integers = Values.Select(
                e => int.TryParse(e, out int n) ? n : (int?)null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();
            var values = new Value();
            values.ListValue = new ListValue();
            integers.ForEach(e => values.ListValue.Values.Add(e));
            return values;
        }

        public static Value StringListAsProtoValue(List<string> Values)
        {
            var ret = new Value();
            ret.ListValue = new ListValue();
            Values.ForEach(c => ret.ListValue.Values.Add(c.ToString()));
            return ret;
        }
    }
}
