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
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.Onnx;
using System.Text.Json.Serialization;

public class PromptExecutionSettingsDto
{
    // Standard properties offered by all runtimes
    public float? Temperature { get; set; }
    public float? TopP { get; set; }

    // ExtensionData properties - specific to runtime
    public int? MaxTokens { get; set; }     // Context window size; Onnx: "max_tokens", Ollama: "num_ctx"
    public int? NumPredict { get; set; }    // only offered by ollama (max model response tokens only)
    public bool ShowReasoning { get; set; } = true; // For Ollama, whether to show reasoning steps (eg, qwen3)

    // For any other extension fields
    [JsonExtensionData]
    public Dictionary<string, object> ExtensionData { get; set; } = new();

    public static PromptExecutionSettingsDto FromOllama(OllamaPromptExecutionSettings src)
    {
        var dto = new PromptExecutionSettingsDto
        {
            Temperature = src.Temperature,
            TopP = src.TopP
        };
        if (src.ExtensionData != null && src.ExtensionData.TryGetValue("num_ctx", out var numCtx))
            dto.MaxTokens = Convert.ToInt32(numCtx);
        if (src.ExtensionData != null && src.ExtensionData.TryGetValue("num_predict", out var numPredict))
            dto.NumPredict = Convert.ToInt32(numPredict);
        if (src.ExtensionData != null && src.ExtensionData.TryGetValue("think", out var showReasoning)) // TODO: doesn't do anything..
            dto.ShowReasoning = Convert.ToBoolean(showReasoning);
        // Copy any other extension data
        if (src.ExtensionData != null)
        {
            foreach (var kv in src.ExtensionData)
            {
                if (kv.Key == "num_ctx" || kv.Key == "num_predict" || kv.Key == "think")
                {
                    continue;
                }
                dto.ExtensionData.Add(kv.Key, kv.Value);
            }
        }
        return dto;
    }

    public static PromptExecutionSettingsDto FromOnnx(OnnxRuntimeGenAIPromptExecutionSettings src)
    {
        var dto = new PromptExecutionSettingsDto
        {
            Temperature = src.Temperature,
            TopP = src.TopP
        };
        if (src.ExtensionData != null && src.ExtensionData.TryGetValue("max_tokens", out var maxTokens))
            dto.MaxTokens = Convert.ToInt32(maxTokens);
        // Copy any other extension data
        if (src.ExtensionData != null)
        {
            foreach (var kv in src.ExtensionData)
            {
                if (kv.Key == "max_tokens")
                {
                    continue;
                }
                dto.ExtensionData.Add(kv.Key, kv.Value);
            }
        }
        return dto;
    }

    public OllamaPromptExecutionSettings ToOllama()
    {
        var settings = new OllamaPromptExecutionSettings
        {
            Temperature = this.Temperature,
            TopP = this.TopP
        };
        // Set extension data
        settings.ExtensionData ??= new Dictionary<string, object>();
        if (MaxTokens.HasValue)
            settings.ExtensionData["num_ctx"] = MaxTokens.Value;
        if (NumPredict.HasValue)
            settings.ExtensionData["num_predict"] = NumPredict.Value;
        settings.ExtensionData["think"] = ShowReasoning;
        foreach (var kv in ExtensionData)
            settings.ExtensionData[kv.Key] = kv.Value;
        return settings;
    }

    public OnnxRuntimeGenAIPromptExecutionSettings ToOnnx()
    {
        var settings = new OnnxRuntimeGenAIPromptExecutionSettings
        {
            Temperature = this.Temperature,
            TopP = this.TopP
        };
        // Set extension data
        settings.ExtensionData ??= new Dictionary<string, object>();
        if (MaxTokens.HasValue)
            settings.ExtensionData["max_tokens"] = MaxTokens.Value;
        foreach (var kv in ExtensionData)
            settings.ExtensionData[kv.Key] = kv.Value;
        return settings;
    }
}