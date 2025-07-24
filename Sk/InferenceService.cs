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
using Meziantou.Framework.WPF.Collections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;
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
            var chatHistory = kernel.GetRequiredService<ChatHistory>();
            var resultHistory = kernel.GetRequiredService<ConcurrentObservableCollection<InsightsInferenceResultModel>>();
            var isOllama = GlobalStateViewModel.Instance.Settings.OllamaConfig != null;
            var isOnnx = GlobalStateViewModel.Instance.Settings.OnnxGenAIConfig != null;

            if (m_PromptExecutionSettings == null)
            {
                Debug.Assert(false);
                throw new Exception("Prompt execution settings are not initialized.");
            }
            progress.UpdateProgressMessage("Performing inference...");

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

            //
            // If the input was from a user role, add the user prompt to the UI.
            //
            if (Role == AuthorRole.User)
            {
                var text = Items[0] as TextContent;
                if (text == null || string.IsNullOrEmpty(text.Text))
                {
                    throw new Exception("User message was empty");
                }
                resultHistory.Add(new InsightsInferenceResultModel()
                {
                    Type = InsightsInferenceResultModel.ContentType.UserInput,
                    Content = text.Text
                });
            }

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

            string? fullResponse;

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
                fullResponse = await GetInferenceResultWithManualToolCalling(Token);
            }

            if (string.IsNullOrEmpty(fullResponse))
            {
                return null;
            }

            //
            // If the input was from a user role, add the model response to the UI.
            //
            if (Role == AuthorRole.User)
            {
                resultHistory.Add(new InsightsInferenceResultModel()
                {
                    Type = InsightsInferenceResultModel.ContentType.ModelOutput,
                    Content = fullResponse
                });
            }    

            progress.UpdateProgressValue();
            //
            // Always add the full model response to the chat history
            //
            chatHistory.AddAssistantMessage(fullResponse);
            return fullResponse;
        }

        private async Task<string?> GetInferenceResultWithManualToolCalling(CancellationToken Token)
        {
            var charsProcessed = 0;
            var isJsonResponse = false;
            var inferenceResult = new InsightsInferenceResultModel();
            var kernel = GlobalStateViewModel.Instance.g_InsightsViewModel.m_Kernel;
            var progress = GlobalStateViewModel.Instance.g_InsightsViewModel.ProgressState;
            var chatHistory = kernel.GetRequiredService<ChatHistory>();
            var resultHistory = kernel.GetRequiredService<ConcurrentObservableCollection<InsightsInferenceResultModel>>();
            //
            // On first invocation, we must add the initial tool prompt to the chat history
            //
            var fullResponse = await GetInferenceResultStreaming(Token, (content) =>
            {
                if (content.Content is { Length: > 0 })
                {
                    if (charsProcessed == 0 && content.Content.Substring(0, 1) == "{")
                    {
                        //
                        // On first character of response, determine if we want to treat it
                        // like a JSON response for tool consumption as opposed to a response
                        // to the user. In this case, we don't send each character to the UI
                        // but instead wait for the full JSON response to parse it.
                        //
                        isJsonResponse = true;
                        progress.UpdateProgressMessage("Awaiting model instructions...");
                    }

                    if (!isJsonResponse)
                    {
                        if (charsProcessed == 0)
                        {
                            progress.ProgressValue = 3; // skips 3 steps
                            progress.UpdateProgressMessage("Receiving model response...");
                            //
                            // Add a new message object to the UI, which we will continually update as
                            // characters from the response comes in.
                            //
                            inferenceResult.Type = InsightsInferenceResultModel.ContentType.ModelOutput;
                            resultHistory.Add(inferenceResult);
                        }
                        inferenceResult.Content += content.Content;
                    }
                    charsProcessed++;
                }
            });

            progress.UpdateProgressValue();

            if (!isJsonResponse)
            {
                //
                // The response was directed at the user
                //
                return fullResponse;
            }
            else
            {
                //
                // The response was directed at a tool, as part of our hack at function
                // calling. Parse it now and do not keep it in the history.
                //
                // We will use the model's plugin/function/argument selections to craft
                // another prompt with additional context to answer the user's original
                // question. This prompt might result in a vector search or some other
                // action (like launching an ETW trace session to gather events),
                // depending on the plugin choice.
                //
                progress.UpdateProgressMessage("Parsing model instructions...");
                var json = JsonConvert.DeserializeObject(fullResponse) as JObject;
                var prompt = await BuildRenderedPromptForFunction(json!);
                var promptSettings = GlobalStateViewModel.Instance.Settings.OnnxGenAIConfig!.PromptExecutionSettings;
                progress.UpdateProgressValue();
                progress.UpdateProgressMessage("Performing task...");
                var result2 = await kernel.InvokePromptAsync(prompt, arguments: new KernelArguments(promptSettings));
                //
                // Add a tool response to the chat history containing the rendered prompt, to give the
                // model the additional context and then issue the query again.
                //
                chatHistory.Add(new() { Role = AuthorRole.Tool, Content = result2.RenderedPrompt });
                //
                // Issue the final request to the model, which contains all of the additional context
                //
                progress.UpdateProgressValue();
                progress.UpdateProgressMessage("Issuing final prompt...");
                fullResponse = await GetInferenceResult(Token);
                progress.UpdateProgressValue();
                return fullResponse;
            }
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
            //
            // The sentence about 'function_call' is added to nudge the model to make chained
            // function calls when appropriate. For example, ollama with llama3.2 doesn't seem
            // to honor calling a second function when the first one returned a directive to
            // call a second plugin function.
            // 
            /*var prompt = $"Your job is to answer questions about ETW events and providers using the provided tools. Follow " +
                $"these rules:{Environment.NewLine}" +
                $"1) If you already have sufficient context about the event(s) or provider(s) in question from a prior " +
                $"tool invocation, use that context to answer the question directly. Use the appropriate tool(s) again if "+
                $"the prior context is insufficient, but stop if a tool returns error messages.{Environment.NewLine}" +
                $"2) If you do not have context about the event(s) or provider(s), select an appropriate plugin and function " +
                $"(tool) from the list provided to you in the conversation history.{Environment.NewLine}" +
                $"3) If the question does not appear to relate to either ETW events or providers, ask the user to clarify." +
                $"{Environment.NewLine}" +
                $"4) If a tool result includes a 'next_step', select an appropriate function from the available tools to "+
                $"fulfill it, passing relevant arguments based on the conversation history. If you do not have a value for "+
                $"a required parameter, do not invent one. Ask the user for the value."+
                $"5) If unsure, pick a function that matches the intent of the 'next_step' or ask for clarification."+
                $"{Environment.NewLine}" +
                $"6) Arguments to tool functions must come from the conversation history, do not make up values. If you do "+
                $"not know what value to provide to a required function parameter, ask the user."+
                $"7) If a tool fails or there is a problem with user input, do not provide information about ETW providers or events " +
                $"from your training data. You should only be using this user's real-time system data. If the tool mentions an error, " +
                $"include it in your response.";*/
            var prompt = $"Your job is to answer questions about the user's system by collecting and analyzing ETW events. " +
                $"When the user asks a question (the analysis goal) that requires ETW data, follow these steps:{Environment.NewLine}" +
                $"1) Start an ETW analysis agent using the appropriate tool/function. To start an agent, you will need to " +
                $"provide the agent an ETW provider name, GUID/ID, or keywords that might match providers on the system. Base your " +
                $"selections on the user's question (analysis goal).{Environment.NewLine}" +
                $"2) The agent will send you the complete manifest(s) of one or more ETW providers that match your initial input. " +
                $"From this list, pick up to 10 providers that seem most relevant to the analysis goal, and of those providers, pick " +
                $"relevant events. Using the provided manifest format of these events, create a reasonable set of exemplar events " +
                $"to be used in a vector similarity search against realtime events. Populate the fields in your events with values that " +
                $"are useful for accomplishing the analysis goal, using constants available in the manifest or other values that " +
                $"could match real events. Fields of particular interest include task, channel, opcode, keyword and template fields. " +
                $"You must include the event ID in the exemplar events.{Environment.NewLine}"+
                $"Send the list of provider IDs and the exemplar events to the agent using the correct tool/function.{Environment.NewLine}" +
                $"3) The agent will start an ETW trace to capture events from the selected providers. It will perform a similarity " +
                $"search against the exemplar events you provided, and return a list of realtime events that resemble the exemplar events. " +
                $"Use these realtime events to answer the user's question and achieve the analysis goal.{Environment.NewLine}" +
                $"While performing these steps, follow these general rules:{Environment.NewLine}" +
                $"1) Follow the instructions contained in messages in the chat history from the tools/functions.{Environment.NewLine}" +
                $"2) Stop if a tool returns error messages.{Environment.NewLine}" +
                $"3) If a tool result includes a 'next_step', select an appropriate function from the available tools to " +
                $"fulfill it, passing relevant arguments based on the conversation history.{Environment.NewLine}" +
                $"4) If you are unsure, pick a function that matches the intent of the 'next_step' or ask for clarification." +
                $"{Environment.NewLine}";
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
