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
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using EtwPilot.Model;
using EtwPilot.ViewModel;
using Meziantou.Framework.WPF.Collections;
using System.Diagnostics;
using OllamaSharp.Models.Chat;
using OllamaSharp;

namespace EtwPilot.InferenceRuntimes
{
    internal abstract class InferenceRuntimeBase
    {
        public IReadOnlyDictionary<string, object?> Attributes { get; }
        protected readonly Kernel m_Kernel;
        protected readonly ChatHistory m_ChatHistory;
        protected ConcurrentObservableCollection<InsightsInferenceResultModel> m_ResultHistory { get; }
        protected PromptExecutionSettings m_PromptExecutionSettings { get; set; }

        public InferenceRuntimeBase(Kernel kernel, ChatHistory chatHistory, ConcurrentObservableCollection<InsightsInferenceResultModel> ResultHistory)
        {
            m_Kernel = kernel;
            m_ChatHistory = chatHistory;
            m_ResultHistory = ResultHistory;
        }

        public abstract Task GenerateAsync(string UserPrompt, CancellationToken Token, ProgressState Progress);

        protected async Task<string> GetInferenceResultStreaming(CancellationToken Token, Action<StreamingChatMessageContent> ResponseCallback)
        {
            Debug.Assert(m_PromptExecutionSettings != null);
            var fullResponse = string.Empty;
            await Task.Run(async () =>
            {
                var inferenceResult = new InsightsInferenceResultModel();
                var chatService = m_Kernel.GetRequiredService<IChatCompletionService>();
                await foreach (var content in chatService.GetStreamingChatMessageContentsAsync(
                    m_ChatHistory, m_PromptExecutionSettings, m_Kernel, Token))
                {
                    Token.ThrowIfCancellationRequested();
                    ResponseCallback.Invoke(content);
                    fullResponse += content.Content;
                }
            });
            return fullResponse;
        }

        protected async Task<string?> GetInferenceResult(CancellationToken Token)
        {
            Debug.Assert(m_PromptExecutionSettings != null);            
            var inferenceResult = new InsightsInferenceResultModel();
            var chatService = m_Kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatService.GetChatMessageContentAsync(
                m_ChatHistory, m_PromptExecutionSettings, m_Kernel, Token);            
            return result.Content;
            
        }

        protected void AddInitialToolPrompt(bool IncludeFunctionDefinitions = false)
        {
            //
            // The sentence about 'function_call' is added to nudge the model to make chained
            // function calls when appropriate. For example, ollama with llama3.2 doesn't seem
            // to honor calling a second function when the first one returned a directive to
            // call a second plugin function.
            // 
            var prompt = $"Your job is to answer questions about ETW events and providers using the provided tools. Follow " +
                $"these rules:{Environment.NewLine}" +
                $"1) If you already have sufficient context about the event(s) or provider(s) in question from a prior " +
                $"tool invocation, use that context to answer the question directly. Use the appropriate tool(s) again if "+
                $"the prior context is insufficient.{Environment.NewLine}" +
                $"2) If you do not have context about the event(s) or provider(s), select an appropriate plugin and function " +
                $"(tool) from the list provided to you in the conversation history.{Environment.NewLine}" +
                $"3) If the question does not appear to relate to either ETW events or providers, ask the user to clarify." +
                $"{Environment.NewLine}" +
                $"4) If a tool result includes a 'next_step', select an appropriate function from the available tools to "+
                $"fulfill it, passing relevant arguments based on the context."+
                $"5) If unsure, pick a function that matches the intent of the 'next_step' or ask for clarification."+
                $"{Environment.NewLine}" +
                $"6) Arguments to tool functions must come from the conversation history, do not make up values. If you do "+
                $"not know what value to provide to a required function parameter, ask the user."+
                $"7) If a tool fails or there is a problem with user input, do not provide information about ETW providers or events " +
                $"from your training data. You should only be using this user's real-time system data. If the tool mentions an error, " +
                $"include it in your response.";

            if (IncludeFunctionDefinitions)
            {
                //
                // Currently only used for runtimes that do not fully support function calling eg Onnx Gen AI
                //
                prompt += $"{Environment.NewLine}";
                prompt += $"Here are the tools (plugins/functions) if you need them:{Environment.NewLine}";
                foreach (var plugin in m_Kernel.Plugins)
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

            m_ChatHistory.Add(new()
            {
                Role = AuthorRole.System,
                Content = prompt,
            });
        }
    }
}
