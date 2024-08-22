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
using System.Diagnostics;
using System.IO;

namespace EtwPilot.Model
{
    using static EtwPilot.Utilities.TraceLogger;

    internal class InsightsConfigurationModel
    {
        public OnnxGenAIConfigModel GenAIConfig { get; set; }
        public string ModelPath { get; set; }
        public string EmbeddingsModelFile { get; set; }
        public string EmbeddingsVocabFile { get; set; }

        public InsightsConfigurationModel()
        {
            GenAIConfig = new OnnxGenAIConfigModel();
            ModelPath = string.Empty;
            EmbeddingsModelFile = string.Empty;
            EmbeddingsVocabFile = string.Empty;
        }

        public static InsightsConfigurationModel? Load(string ModelPath, string EmbeddingsModelFile)
        {
            if (!ValidateModelPath(ModelPath) || !ValidateEmbeddingsModelFile(EmbeddingsModelFile))
            {
                return null;
            }

            var config = new InsightsConfigurationModel();

            try
            {
                var configPath = Path.Combine(ModelPath, "genai_config.json");
                var configJson = File.ReadAllText(configPath);
                if (string.IsNullOrEmpty(configJson))
                {
                    Debug.Assert(false);
                    Trace(TraceLoggerType.Inference,
                          TraceEventType.Error,
                          $"Json content for OnnxGenAI config file {configPath} is empty.");
                    return null;
                }
                var settings = new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error,
                    Error = (sender, eventArgs) => {
                        Console.WriteLine(eventArgs.ErrorContext.Error.Message);  // or write to a log
                        eventArgs.ErrorContext.Handled = true;
                    }
                };
                var onnxConfig = JsonConvert.DeserializeObject<OnnxGenAIConfigModel>(configJson, settings);
                if (onnxConfig == null)
                {
                    Trace(TraceLoggerType.Inference,
                          TraceEventType.Error,
                          $"Unable to deserialize json content {configPath}: empty result.");
                    return null;
                }
                config.GenAIConfig = onnxConfig;
                config.ModelPath = ModelPath;
                config.EmbeddingsModelFile = EmbeddingsModelFile;
                config.EmbeddingsVocabFile = GetVocabFile(EmbeddingsModelFile);
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.Inference,
                      TraceEventType.Error,
                      $"Exception occurred when loading model config: {ex.Message}");
                return null;
            }

            return config;
        }

        public static bool ValidateModelPath(string ModelPath)
        {
            if (string.IsNullOrEmpty(ModelPath) || !Directory.Exists(ModelPath))
            {
                return false;
            }

            var configPath = Path.Combine(ModelPath, "genai_config.json");
            if (!File.Exists(configPath))
            {
                return false;
            }
            return true;
        }

        public static bool ValidateEmbeddingsModelFile(string ModelFile)
        {
            if (string.IsNullOrEmpty(ModelFile) || !File.Exists(ModelFile))
            {
                return false;
            }
            var vocabFile = GetVocabFile(ModelFile);
            if (!File.Exists(vocabFile))
            {
                return false;
            }
            return true;
        }

        private static string GetVocabFile(string ModelFile)
        {
            var baseDir = Directory.GetParent(Path.GetDirectoryName(ModelFile)!)!.FullName;
            return Path.Combine(baseDir, "vocab.txt");
        }
    }
}
