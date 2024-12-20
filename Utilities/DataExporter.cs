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
using System.Xml.Serialization;
using System.Xml;
using System.Text;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

namespace EtwPilot.Utilities
{
    public static class DataExporter
    {
        public enum ExportFormat
        {
            Csv = 1,
            Xml,
            Json,
            Clip,
            Custom
        }

        public static async Task<(int, string?)> Export<T>(dynamic Data, ExportFormat Format, string DataTypeName, CancellationToken Token)
        {
            bool isListType = MiscHelper.IsListType(typeof(T));
            string location;
            if (Format != ExportFormat.Clip)
            {
                var browser = new OpenFolderDialog();
                browser.Title = "Select a location to export the data";
                browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
                var result = browser.ShowDialog();
                if (!result.HasValue || !result.Value)
                {
                    return (0, null);
                }
                location = Path.Combine(browser.FolderName,
                    $"{DataTypeName}-{DateTime.Now:yyyy-MM-dd-HHmmss}.{Format}");
            }
            else
            {
                location = "Clipboard";
            }
            switch (Format)
            {
                case ExportFormat.Json:
                    {
                        var json = JsonConvert.SerializeObject(Data,
                            Newtonsoft.Json.Formatting.Indented);
                        await File.WriteAllTextAsync(location, json, Token);
                        break;
                    }
                case ExportFormat.Xml:
                    {
                        var serializer = new XmlSerializer(typeof(T));
                        using (var sw = new StringWriter())
                        {
                            using (var writer = new XmlTextWriter(sw)
                            {
                                Formatting = System.Xml.Formatting.Indented,
                            })
                            {
                                serializer.Serialize(writer, Data);
                                await File.WriteAllTextAsync(location, sw.ToString(), Token);
                            }
                        }
                        break;
                    }
                case ExportFormat.Csv:
                    {
                        if (isListType)
                        {
                            await File.WriteAllTextAsync(location, GetDataAsCsv(Data), Token);
                        }
                        else
                        {
                            await File.WriteAllTextAsync(location, $"{Data}", Token);
                        }
                        break;
                    }
                case ExportFormat.Clip:
                    {
                        if (isListType)
                        {
                            System.Windows.Clipboard.SetText(GetDataAsCsv(Data));
                        }
                        else
                        {
                            System.Windows.Clipboard.SetText($"{Data}");
                        }
                        break;
                    }
                case ExportFormat.Custom:
                    {
                        var lines = Data as List<string>;
                        if (lines == null)
                        {
                            throw new Exception("Invalid custom data format");
                        }
                        File.WriteAllLines(location, lines);
                        break;
                    }
                default:
                    {
                        throw new Exception($"Invalid export format {Format}");
                    }
            }
            return (isListType? Data.Count : 1, location);
        }

        public static string GetDataAsCsv<T>(List<T> Data)
        {
            //
            // NB: If T is not a flat type (ie, nested lists), we will attempt to construct
            // a single comma-separated, escaped string for any nested list.
            //
            var sb = new StringBuilder();
            var properties = typeof(T).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).ToList();
            var columns = properties.Select(p => p.Name).ToList();
            sb.AppendLine(string.Join(",", columns));
            foreach (var data in Data)
            {
                var values = properties.Select(p =>
                {
                    var obj = p.GetValue(data, null);
                    if (obj != null && p.PropertyType != typeof(string) &&
                        obj.GetType().GetInterfaces().Any(k => k.IsGenericType
                        && k.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                    {
                        var values = p.GetValue(data, null) as IEnumerable;
                        Debug.Assert(values != null);
                        List<string> strvalues = new List<string>();
                        foreach (var v in values)
                        {
                            strvalues.Add(v.ToString()!);
                        }
                        Debug.Assert(strvalues.Count > 0);
                        return $"\"{string.Join(",", strvalues)}\"";
                    }
                    else
                    {
                        return obj;
                    }
                }).ToList();
                sb.AppendLine(string.Join(",", values));
            }
            return sb.ToString();
        }
    }
}
