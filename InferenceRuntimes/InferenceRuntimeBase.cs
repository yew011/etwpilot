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

namespace EtwPilot.InferenceRuntimes
{
    internal abstract class InferenceRuntimeBase
    {
        public IReadOnlyDictionary<string, object?> Attributes { get; }
        protected readonly Kernel m_Kernel;
        protected readonly ChatHistory m_ChatHistory;
        protected ConcurrentObservableCollection<InsightsInferenceResultModel> m_ResultHistory { get; }
        protected PromptExecutionSettings m_PromptExecutionSettings { get; set; }

        public InferenceRuntimeBase(Kernel kernel, ChatHistory chatHistory)
        {
            m_Kernel = kernel;
            m_ChatHistory = chatHistory;
        }

        public abstract Task GenerateAsync(string UserPrompt, CancellationToken Token, ProgressState Progress);

        protected async Task<string> GetInferenceResult(CancellationToken Token, Action<StreamingChatMessageContent>? ResponseCallback = null)
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
                    ResponseCallback?.Invoke(content);
                    fullResponse += content.Content;
                }
            });
            return fullResponse;
        }
    }
}
