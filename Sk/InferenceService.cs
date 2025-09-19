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
using EtwPilot.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace EtwPilot.Sk
{
    internal class InferenceService
    {
        public PromptExecutionSettings? m_PromptExecutionSettings;

        public InferenceService()
        {

        }

        public void Initialize(IKernelBuilder Builder)
        {
            var ollamaRuntimeConfig = GlobalStateViewModel.Instance.Settings.OllamaConfig;
            var onnxRuntimeConfig = GlobalStateViewModel.Instance.Settings.OnnxGenAIConfig;
            if (ollamaRuntimeConfig == null && onnxRuntimeConfig == null)
            {
                throw new Exception("Chat completion runtime not selected.");
            }
            //
            // Select chat completion service based on selected runtime.
            //
            if (onnxRuntimeConfig != null)
            {
                if (onnxRuntimeConfig.PromptExecutionSettings == null)
                {
                    throw new Exception("Onnx runtime prompt execution settings are missing");
                }
                var modelPath = onnxRuntimeConfig.ModelPath;
                var embeddingsModelPath = onnxRuntimeConfig.BertModelPath;
                var embeddingsVocabFile = onnxRuntimeConfig.GetVocabFile();
                var modelId = onnxRuntimeConfig.PromptExecutionSettings.ModelId;
                if (string.IsNullOrEmpty(modelId))
                {
                    throw new Exception("Model ID is missing from prompt execution settings");
                }
                Builder.AddOnnxRuntimeGenAIChatCompletion(modelId, modelPath!);
                Builder.AddBertOnnxEmbeddingGenerator(embeddingsModelPath, embeddingsVocabFile);
                m_PromptExecutionSettings = onnxRuntimeConfig.PromptExecutionSettings;
            }
            else if (ollamaRuntimeConfig != null)
            {
                if (ollamaRuntimeConfig.PromptExecutionSettings == null)
                {
                    throw new Exception("Onnx runtime prompt execution settings are missing");
                }
                //
                // Ollama config doesn't hold a model ID, even though SK supplies
                // a property for it. The model ID is the model name selected from
                // the list of models available in the ollama server.
                //
                var modelId = ollamaRuntimeConfig.ModelName!;
                Builder.AddOllamaChatCompletion(modelId, new Uri(ollamaRuntimeConfig.EndpointUri!));
                Builder.AddOllamaEmbeddingGenerator(ollamaRuntimeConfig.TextEmbeddingModelName!,
                    new Uri(ollamaRuntimeConfig.EndpointUri!));
                m_PromptExecutionSettings = ollamaRuntimeConfig.PromptExecutionSettings;
            }
            Builder.Services.AddSingleton(this);
        }

        public async Task<string?> RunInferenceAsync(AuthorRole Role, ChatMessageContentItemCollection Items, CancellationToken Token)
        {
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var progress = GlobalStateViewModel.Instance.g_InsightsViewModel.ProgressState;
            using var progressContext = progress.CreateProgressContext(4, $"Inference started...");
            var chatHistory = kernel.GetRequiredService<ChatHistory>();
            var isOllama = GlobalStateViewModel.Instance.Settings.OllamaConfig != null;
            var isOnnx = GlobalStateViewModel.Instance.Settings.OnnxGenAIConfig != null;

            if (m_PromptExecutionSettings == null)
            {
                Debug.Assert(false);
                throw new Exception("Prompt execution settings are not initialized.");
            }

            if (chatHistory.Count == 0)
            {
                //
                // At the start of a conversation, add some tool input to the model to help it
                // answer questions about events, start ETW traces, etc.
                //
                AddInitialToolPrompt(isOnnx);
            }

            //
            // Always add the inference input to the chat history. This could be a user prompt string,
            // images, function call, tool input, etc.
            //
            chatHistory.Add(new ChatMessageContent(Role, Items));

            m_PromptExecutionSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                options: new FunctionChoiceBehaviorOptions()
                {
                    //AllowConcurrentInvocation = false,
                }); // set to None() for debug mode
            progress.UpdateProgressValue();

            //
            // Set the progressState as a kernel data object so plugins can retrieve it
            // to show progress in the UI, in the case of function calling.
            //
            if (!kernel.Data.TryGetValue("ProgressState", out _))
            {
                kernel.Data.Add("ProgressState", progress);
            }
            else
            {
                kernel.Data["ProgressState"] = progress;
            }

            string? fullResponse = null;

            if (isOllama)
            {
                //
                // NB: Ollama has limitations in streaming model response - see
                //  https://github.com/microsoft/semantic-kernel/issues/10292
                //
                fullResponse = await GetInferenceResult(Token);
            }
            else
            {
                //
                // Onnx does not support tool calling
                //
                //fullResponse = await GetInferenceResultWithManualToolCalling(Token);
            }

            if (string.IsNullOrEmpty(fullResponse))
            {
                return null;
            }   

            progress.UpdateProgressValue();
            //
            // Always add the full model response to the chat history
            //
            chatHistory.AddAssistantMessage(fullResponse);
            return fullResponse;
        }

        private async Task<string> GetInferenceResultStreaming(CancellationToken Token, Action<StreamingChatMessageContent> ResponseCallback)
        {
            Debug.Assert(m_PromptExecutionSettings != null);
            var fullResponse = string.Empty;
            await Task.Run(async () =>
            {
                var inferenceResult = new InsightsInferenceResultModel();
                var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
                var chatService = kernel.GetRequiredService<IChatCompletionService>();
                var chatHistory = kernel.GetRequiredService<ChatHistory>();

                await foreach (var content in chatService.GetStreamingChatMessageContentsAsync(
                    chatHistory, m_PromptExecutionSettings, kernel, Token))
                {
                    Token.ThrowIfCancellationRequested();
                    ResponseCallback.Invoke(content);
                    fullResponse += content.Content;
                }
            });
            return fullResponse;
        }

        private async Task<string?> GetInferenceResult(CancellationToken Token)
        {
            Debug.Assert(m_PromptExecutionSettings != null);
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = kernel.GetRequiredService<ChatHistory>();
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory, m_PromptExecutionSettings, kernel, Token);
            return result.Content;
        }

        private void AddInitialToolPrompt(bool IncludeFunctionDefinitions = false)
        {
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var prompt = @$"Your job is to answer questions about the user's system by collecting and analyzing ETW events. 
                When the user asks a question (the analysis goal) that requires ETW data, follow these steps: 

                1) Identify up to 5 ETW providers that produce ETW events related to the analysis goal: 
                   a) Obtain the list of ETW providers on the system using the appropriate tool.
                   b) Select up to 10 providers from this list. If the user provides direction or specific criteria on what providers to select, use it. Otherwise, use your own knowledge of the purpose of providers in the provided list to choose those that might suggest a good fit for the analysis goal. Do NOT invent or make up names. Pass the list of selected provider names to the appropriate tool to retrieve their manifests. The tool will send you a list of JSON-encoded manifests for each provider. 
                {EtwManifestCompressor.GetSystemPromptHint()} 
                   c) Study the manifests to understand what information the providers expose: the events, opcodes, tasks, channels, strings and other keywords. Repeat steps b-c until you feel you have chosen up to 5 providers that are strongly related to the confidence goal.
                2) Pass the provider names to the appropriate tool to start a real-time ETW trace session. Always start a trace with no reduction strategy (ie, specify only provider name).
                   a) When the trace completes, invoke the appropriate tool to retrieve the collected events iteratively until there are no more events.
                   b) If the trace produced too many events, retry the trace using one of these data reduction strategies:
                    i) Consult the manifest of each selected provider and select event ID and version of events of interest, limiting the trace to just the selected events. Do this for each provider you have selected, and pass this information to the appropriate tool to produce a more scoped set of events in a new trace session. Return to step 2a.
                    ii) Limit the trace to one or more processes of interest:
                        1. Call the appropriate tool to get a list of process names/IDs
                        2. Based on name alone, select up to 5 processes from this list. Choose processes whose name might suggest a good fit for the analysis goal. Do not invent or make up names. For example, if the user wants to investigate performance of their browser software, you might select process names that correlate to browsers, such as 'chrome', 'msedge', 'firefox', etc.
                        3. Pass the list of process names and/or IDs to the appropriate tool to limit the trace to events from just these processes. Return to step 2a.
                   c) If the trace produced too few (or no) events, go back to step 1b and select different providers.
                3) Analyze the collected events to achieve the analysis goal.";
            if (IncludeFunctionDefinitions)
            {
                //
                // NB: OnnxRuntime-GenAi does not currently support model function calling:
                //  https://github.com/microsoft/onnxruntime-genai/discussions/969
                //
                // When it does, this code should be removed in favor of using SK's capabilities
                // to serialize plugins/functions and handle model requests for function calls
                // and parsing results.
                //
                // As a result of this missing capability, this InferenceRuntime attempts to
                // mimic it, poorly. Llamacpp is preferable to this.
                //
                prompt += $"{Environment.NewLine}";
                prompt += $"Here are the tools (plugins/functions) if you need them:{Environment.NewLine}";
                foreach (var plugin in kernel.Plugins)
                {
                    prompt += $"Functions for the plugin '{plugin.Name}' with description {plugin.Description}{Environment.NewLine}:";
                    foreach (var function in plugin.GetFunctionsMetadata())
                    {
                        prompt += $"  - {function.Name}: {function.Description}{Environment.NewLine}";
                        prompt += $"  Parameters:{Environment.NewLine}";
                        if (function.Parameters.Count == 0)
                        {
                            prompt += $"  (None){Environment.NewLine}";
                            continue;
                        }
                        foreach (var param in function.Parameters)
                        {
                            prompt += $"    - {param.Name}, required: {param.IsRequired}, type: {param.ParameterType}, description: " +
                                $"{param.Description}{Environment.NewLine}";
                        }
                    }
                }
            }

            var chatHistory = kernel.GetRequiredService<ChatHistory>();
            chatHistory.AddSystemMessage(prompt);
        }

        private async Task<string> BuildRenderedPromptForFunction(JObject Response)
        {
            //
            // Note: this routine should be tossed, aggressively, in favor of SK's own
            // function calling support, when ONNX Runtime GenAI supports it.
            //

            if (Response is null)
            {
                throw new Exception("Model's JSON response is null");
            }
            if (!Response.ContainsKey("plugin") || !Response.ContainsKey("function") ||
                !Response.ContainsKey("arguments"))
            {
                throw new Exception($"Model's response isn't valid JSON: {Response}");
            }
            var plugin = Response["plugin"]!.Value<string>()!;
            var functionName = Response["function"]!.Value<string>()!;
            var args = Response["arguments"]! as JObject;
            var prompt = $"Use this additional context to answer the question: {{{{{plugin}.{functionName}";
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var meta = kernel.Plugins.GetFunction(plugin, functionName).Metadata;
            var kernelArgs = new KernelArguments();
            foreach (var param in meta.Parameters)
            {
                if (!args!.ContainsKey(param.Name) && param.IsRequired)
                {
                    throw new Exception($"Model's response is missing function argument '{param.Name}': {Response}");
                }

                //
                // NB: Obviously our plugin function args cannot have complex types or types other than
                // what is supported below. All the more reason to switch over to Sk's function
                // calling support as soon as ONNX allows it, as all of this is handled for us.
                //
                var val = args![param.Name];
                if (val == null)
                {
                    if (param.IsRequired)
                    {
                        throw new Exception($"Model's response is missing a value for required " +
                            $"function argument '{param.Name}': {Response}");
                    }
                }
                else if (param.ParameterType == typeof(int))
                {
                    kernelArgs.Add(param.Name, val.Value<int>());
                    prompt += $" ${param.Name}";
                }
                else if (param.ParameterType == typeof(string))
                {
                    kernelArgs.Add(param.Name, val.Value<string>());
                    prompt += $" ${param.Name}";
                }
                else
                {
                    throw new Exception($"Parameter type {param.ParameterType} is not supported.");
                }
            }
            prompt += $"}}}}";
            var promptTemplateFactory = new KernelPromptTemplateFactory();
            var promptTemplate = promptTemplateFactory.Create(
                new PromptTemplateConfig()
                {
                    Template = prompt,
                    TemplateFormat = "semantic-kernel",
                });
            return await promptTemplate.RenderAsync(kernel, kernelArgs);
        }
    }
}
