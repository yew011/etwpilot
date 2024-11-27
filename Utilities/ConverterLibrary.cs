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
using EtwPilot.Model;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Data;

namespace EtwPilot.Utilities
{
    public class ConverterLibrary
    {
        private ConcurrentDictionary<string, DynamicRuntimeLibrary> ConverterLibraryCache;

        public ConverterLibrary()
        {
            ConverterLibraryCache = new ConcurrentDictionary<string, DynamicRuntimeLibrary>();
        }

        public IValueConverter? GetIConverter(string ColumnName)
        {
            if (!ConverterLibraryCache.ContainsKey(ColumnName))
            {
                Debug.Assert(false);
                return null;
            }
            return ConverterLibraryCache[ColumnName].GetInstance() as IValueConverter;
        }

        public void Build(List<ConfiguredProvider> ConfiguredProviders)
        {
            //
            // Compile a small dynamic library for each IConverter supplied in
            // column definitions. This allows users to format display columns
            // to their liking. This is done once per trace session (viewmodel)
            // and reused across tab load/unload operations.
            //
            foreach (var prov in ConfiguredProviders)
            {
                foreach (var col in prov.Columns)
                {
                    if (string.IsNullOrEmpty(col.IConverterCode))
                    {
                        continue;
                    }
                    if (ConverterLibraryCache.ContainsKey(col.Name))
                    {
                        continue;
                    }
                    var library = col.GetIConverterLibrary();
                    var result = library.TryCompile(out string err);
                    if (!result)
                    {
                        Debug.Assert(result);
                        ConverterLibraryCache.Clear();
                        return;
                    }
                    result = ConverterLibraryCache.TryAdd(col.Name, library);
                    Debug.Assert(result);
                }
            }
        }
    }
}
