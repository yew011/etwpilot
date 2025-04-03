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

namespace EtwPilot.InferenceRuntimes
{
    internal class Ollama : InferenceRuntimeBase
    {
        private readonly OllamaConfigModel m_Config;

        public Ollama(OllamaConfigModel Config, Kernel kernel, ChatHistory History) : base(kernel, History)
        {
            m_Config = Config;
            m_PromptExecutionSettings = Config.PromptExecutionSettings!;
        }

        public override async Task GenerateAsync(string UserPrompt, CancellationToken Token, ProgressState Progress)
        {
            Progress.UpdateProgressMessage("Performing inference...");

            m_ChatHistory.AddUserMessage(UserPrompt);

            m_PromptExecutionSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                options: new FunctionChoiceBehaviorOptions()
                {
                    AllowConcurrentInvocation = true,
                    AllowParallelCalls = true,
                }); // set to None() for debug mode

            Progress.UpdateProgressValue();
            var fullResponse = await GetInferenceResult(Token);
            Progress.UpdateProgressValue();
            m_ChatHistory.AddAssistantMessage(fullResponse);
        }
    }
}
