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
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using EtwPilot.Model;
using Meziantou.Framework.WPF.Collections;

namespace EtwPilot.InferenceRuntimes
{
    internal class Ollama : InferenceRuntimeBase
    {
        private readonly OllamaConfigModel m_Config;

        public Ollama(OllamaConfigModel Config,
            Kernel kernel,
            ChatHistory History,
            ConcurrentObservableCollection<InsightsInferenceResultModel> ResultHistory) : base(kernel, History, ResultHistory)
        {
            m_Config = Config;
            m_PromptExecutionSettings = Config.PromptExecutionSettings!;
        }

        public override async Task GenerateAsync(string UserPrompt, CancellationToken Token, ProgressState Progress)
        {
            Progress.UpdateProgressMessage("Performing inference...");

            if (m_ChatHistory.Count == 0)
            {
                //
                // At the start of a conversation, add some tool input to the model to help it
                // answer questions about events, start ETW traces, etc.
                //
                AddInitialToolPrompt();
            }

            //
            // Add user prompt to full chat history
            //
            m_ChatHistory.AddUserMessage(UserPrompt);
            m_PromptExecutionSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                options: new FunctionChoiceBehaviorOptions()
                {
                    AllowConcurrentInvocation = false,
                    AllowParallelCalls = false,
                }); // set to None() for debug mode

            Progress.UpdateProgressValue();

            //
            // Set the progressState as a kernel data object so plugins can retrieve it
            // to show progress in the UI, in the case of function calling.
            //
            if (!m_Kernel.Data.TryGetValue("ProgressState", out _))
            {
                m_Kernel.Data.Add("ProgressState", Progress);
            }
            else
            {
                m_Kernel.Data["ProgressState"] = Progress;
            }

            //
            // NB: Ollama has limitations in streaming model response - see
            //  https://github.com/microsoft/semantic-kernel/issues/10292
            //
            var fullResponse = await GetInferenceResult(Token);
            if (string.IsNullOrEmpty(fullResponse))
            {
                return;
            }

            //
            // Add the user question to the UI
            //
            m_ResultHistory.Add(new InsightsInferenceResultModel()
            {
                Type = InsightsInferenceResultModel.ContentType.UserInput,
                Content = UserPrompt
            });

            //
            // Add model response
            //
            var inferenceResult = new InsightsInferenceResultModel()
            {
                Type = InsightsInferenceResultModel.ContentType.ModelOutput,
                Content = fullResponse
            };
            m_ResultHistory.Add(inferenceResult);

            Progress.UpdateProgressValue();
            //
            // Add the full model response to the chat history
            //
            m_ChatHistory.AddAssistantMessage(fullResponse);
        }
    }
}
