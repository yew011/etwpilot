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

public class PromptExecutionSettingsDto // Data Transfer Object
{
    // Standard properties offered by all runtimes
    public float? Temperature { get; set; }
    public float? TopP { get; set; }

    // ExtensionData properties - specific to runtime
    public int? MaxTokens { get; set; }     // Context window size; Onnx: "max_tokens", Ollama: "num_ctx"
    public int? NumPredict { get; set; }    // only offered by ollama (max model response tokens only)
    public bool ShowReasoning { get; set; } = false; // For Ollama, whether to show reasoning steps (eg, qwen3)
    public string ReasoningEffort { get; set; } = "low"; // only gpt-oss for now, "low", "medium", "high"

    // For any other extension fields
    [JsonExtensionData]
    public Dictionary<string, object> ExtensionData { get; set; } = new();

    private static readonly string[] s_ModelsSupportingThink = new[]
        {
            "gpt-oss", "deepseek-r1", "qwen3", "magistral", "deepseek-v3.1"
        };
    [JsonIgnore] // ui only
    public string[] s_ReasoningEfforts { get; private set; } = new[] {
            "low", "medium", "high"
        };

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
        // Important: ollama will throw an exception if the 'think' key
        // appears at all, when the model doesn't support it and regardless
        // of the value being set for it.
        if (ShowReasoning)
        {
            // gpt-oss model supports levels of thinking, otherwise just a bool
            settings.ExtensionData["reasoning_effort"] = ReasoningEffort;
            settings.ExtensionData["think"] = ShowReasoning;
        }
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

    public static bool ModelSupportsThink(string modelName)
    {
        return s_ModelsSupportingThink.Any(prefix => modelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}