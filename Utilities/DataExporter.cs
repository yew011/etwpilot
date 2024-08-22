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
using EtwPilot.ViewModel;
using Newtonsoft.Json;
using static EtwPilot.ViewModel.MainWindowViewModel;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Xml;
using System.Text;
using System.Collections;
using System.Diagnostics;

namespace EtwPilot.Utilities
{
    internal static class DataExporter
    {
        public static async Task Export<T>(List<T> Data, ExportFormat Format, StateManager State, string DataTypeName)
        {
            var browser = new FolderBrowserDialog();
            browser.Description = "Select a location to export the data";
            browser.RootFolder = Environment.SpecialFolder.MyComputer;
            var result = browser.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            var location = Path.Combine(browser.SelectedPath,
                $"{DataTypeName}-{DateTime.Now:yyyy-MM-dd-HHmmss}.{Format}");

            State.ProgressState.InitializeProgress(1);
            switch (Format)
            {
                case ExportFormat.Json:
                    {
                        var json = JsonConvert.SerializeObject(Data,
                            Newtonsoft.Json.Formatting.Indented);
                        File.WriteAllText(location, json);
                        break;
                    }
                case ExportFormat.Xml:
                    {
                        var serializer = new XmlSerializer(typeof(List<T>));
                        using (var sw = new StringWriter())
                        {
                            using (var writer = new XmlTextWriter(sw)
                            {
                                Formatting = System.Xml.Formatting.Indented
                            })
                            {
                                serializer.Serialize(writer, Data);
                                File.WriteAllText(location, sw.ToString());
                            }
                        }
                        break;
                    }
                case ExportFormat.Csv:
                    {
                        File.WriteAllText(location, GetDataAsCsv(Data));
                        break;
                    }
                case ExportFormat.Custom:
                    {
                        var lines = Data as List<string>;
                        if (lines == null)
                        {
                            State.ProgressState.FinalizeProgress("Invalid data format");
                            return;
                        }
                        File.WriteAllLines(location, lines);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
            State.ProgressState.FinalizeProgress($"Exported {Data.Count} rows to {location}");
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
