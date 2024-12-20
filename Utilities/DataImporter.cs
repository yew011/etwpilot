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
using Newtonsoft.Json;
using System.IO;
using Microsoft.Win32;
using System.Xml.Serialization;
using System.Xml;
using Newtonsoft.Json.Linq;

namespace EtwPilot.Utilities
{
    public enum ImportFormat
    {
        Invalid,
        Xml,
        Json,
        Max
    }
    public static class DataImporter
    {
        public static T? Import<T>(ImportFormat Format)
        {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Multiselect = false;
            dialog.Title = $"Select a file to import";
            switch (Format)
            {
                case ImportFormat.Xml:
                    {
                        dialog.Filter = "XML files (*.xml)|*.xml";
                        break;
                    }
                case ImportFormat.Json:
                    {
                        dialog.Filter = "JSON files (*.json)|*.json";
                        break;
                    }
                default:
                    {
                        throw new Exception($"Invalid input format {Format}");
                    }
            }
            var result = dialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return default;
            }
            var location = dialog.FileName;

            switch (Format)
            {
                case ImportFormat.Json:
                    {
                        var obj = JsonConvert.DeserializeObject(File.ReadAllText(location));
                        if (obj == null)
                        {
                            return default;
                        }
                        if (obj is JArray)
                        {
                            return ((JArray)obj).ToObject<T>();
                        }
                        return (T)obj;
                    }
                case ImportFormat.Xml:
                    {
                        var serializer = new XmlSerializer(typeof(T));
                        using (var sr = new StringReader(File.ReadAllText(location)))
                        {
                            using (var reader = new XmlTextReader(sr))
                            {
                                var obj = serializer.Deserialize(reader);
                                if (obj == null)
                                {
                                    return default;
                                }
                                return (T)obj;
                            }
                        }
                    }
                default:
                    {
                        throw new Exception($"Invalid input format {Format}");
                    }
            }
        }
    }
}
