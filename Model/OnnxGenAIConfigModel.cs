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
using System.Diagnostics;
using System.IO;
using Microsoft.SemanticKernel.Connectors.Onnx;
using EtwPilot.Utilities;
using Newtonsoft.Json;
using EtwPilot.ViewModel;

//
// Remove supression after onnx connector is out of alpha
//
#pragma warning disable SKEXP0070

namespace EtwPilot.Model
{
    using static EtwPilot.Utilities.TraceLogger;

    public class OnnxGenAIConfigModel : NotifyPropertyAndErrorInfoBase
    {
        #region observable properties

        private string? _ModelPath;
        public string? ModelPath
        {
            get => _ModelPath;
            set
            {
                if (_ModelPath == value)
                {
                    return;
                }

                _ModelPath = value;
                if (!string.IsNullOrEmpty(value))
                {
                    ClearErrors(nameof(ModelPath));
                    if (!ValidateModelPath())
                    {
                        AddError(nameof(ModelPath), "Model path is invalid");
                    }
                    else
                    {
                        OnPropertyChanged("ModelPath");
                    }
                }
                else
                {
                    OnPropertyChanged("ModelPath");
                    RuntimeConfigFile = null;
                }
            }
        }

        private string _BertModelPath;
        public string BertModelPath
        {
            get => _BertModelPath;
            set
            {
                if (_BertModelPath == value)
                {
                    return;
                }
                _BertModelPath = value;
                if (!string.IsNullOrEmpty(value))
                {
                    ClearErrors(nameof(BertModelPath));
                    if (!ValidateBertPath())
                    {
                        AddError(nameof(BertModelPath), "BERT path is invalid");
                    }
                    else
                    {
                        OnPropertyChanged("BertModelPath");
                    }
                }
                else
                {
                    OnPropertyChanged("BertModelPath");
                }
            }
        }

        private string? _RuntimeConfigFile;
        public string? RuntimeConfigFile {
            get => _RuntimeConfigFile;
            set
            {
                _RuntimeConfigFile = value;
                if (!string.IsNullOrEmpty(value))
                {
                    ClearErrors(nameof(RuntimeConfigFile));
                    if (!ValidateRuntimeConfigFile())
                    {
                        AddError(nameof(RuntimeConfigFile), "Runtime config file is invalid");
                    }
                    else
                    {
                        try
                        {
                            PromptExecutionSettings = JsonConvert.DeserializeObject<OnnxRuntimeGenAIPromptExecutionSettings>(value);
                            OnPropertyChanged("RuntimeConfigFile");
                        }
                        catch (Exception ex)
                        {
                            AddError(nameof(RuntimeConfigFile), "Failed to deserialize runtime config file");
                            Trace(TraceLoggerType.Settings,
                                  TraceEventType.Error,
                                  $"Exception occurred when deserializing {RuntimeConfigFile} into OnnxRuntimeGenAIPromptExecutionSettings: {ex.Message}");
                        }
                    }
                }
                else
                {
                    OnPropertyChanged("RuntimeConfigFile");
                    PromptExecutionSettings = null;
                }
            }
        }

        #endregion

        [JsonIgnore] // recomputed on each load
        public OnnxRuntimeGenAIPromptExecutionSettings? PromptExecutionSettings { get; set; }

        public OnnxGenAIConfigModel()
        {
        }

        public string GetVocabFile()
        {
            if (ModelPath == null)
            {
                throw new Exception("Model path is missing");
            }
            var vocabFile = Path.Combine(ModelPath, "vocab.txt");
            if (!File.Exists(vocabFile))
            {
                throw new Exception($"Computed vocab file location {vocabFile} does not exist");
            }
            return vocabFile;
        }

        private bool ValidateModelPath()
        {
            if (string.IsNullOrEmpty(ModelPath) || !Directory.Exists(ModelPath))
            {
                return false;
            }
            //
            // All ONNX generative models follow these file name and location conventions.
            // Refer to https://onnxruntime.ai/docs/
            //
            var modelFile = Path.Combine(ModelPath, "model.onnx");
            if (!File.Exists(modelFile))
            {
                return false;
            }
            return true;
        }

        private bool ValidateRuntimeConfigFile()
        {
            return (!string.IsNullOrEmpty(RuntimeConfigFile) && File.Exists(RuntimeConfigFile));
        }

        private bool ValidateBertPath()
        {
            if (string.IsNullOrEmpty(ModelPath) || !Directory.Exists(ModelPath))
            {
                return false;
            }
            //
            // All sentence transformer models follow these file name and location conventions.
            // Refer to https://huggingface.co/sentence-transformers
            //
            var vocabFile = Path.Combine(ModelPath, "vocab.txt");
            if (!File.Exists(vocabFile))
            {
                return false;
            }
            var modelFile = Path.Combine(ModelPath, "onnx", "model.onnx");
            if (!File.Exists(modelFile))
            {
                return false;
            }
            return true;
        }
    } 
}
