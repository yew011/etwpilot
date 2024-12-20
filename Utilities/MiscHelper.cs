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

using System.Collections;
using System.Reflection;

namespace EtwPilot.Utilities
{
    internal static class MiscHelper
    {
        public static bool IsListType(Type type)
        {
            return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        }

        public static string FormatByteSizeString(double Value, int DecimalPlaces = 1)
        {
            string[] sizes =
                   { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            if (Value < 0)
            {
                return "-" + FormatByteSizeString(-Value);
            }
            if (Value == 0)
            {
                return string.Format("{0:n" + DecimalPlaces + "} bytes", 0);
            }

            int mag = (int)Math.Log(Value, 1024);
            decimal adjustedSize = (decimal)Value / (1L << (mag * 10));
            if (Math.Round(adjustedSize, DecimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + DecimalPlaces + "} {1}", adjustedSize, sizes[mag]);
        }

        public static void ListResources()
        {
            var asm = Assembly.GetEntryAssembly();
            if (asm == null)
            {
                return;
            }
            string resName = asm.GetName().Name + ".g.resources";
            using (var stream = asm.GetManifestResourceStream(resName))
            {
                if (stream == null)
                {
                    return;
                }
                using (var reader = new System.Resources.ResourceReader(stream))
                {
                    var stuff = reader.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).ToArray();
                }
                foreach (var r in System.Windows.Application.Current.Resources)
                {
                    var thisthing = r;
                }
            }
        }
    }
}
