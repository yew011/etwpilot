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
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace EtwPilot.InferenceRuntimes
{
    internal class OnnxGenAI : InferenceRuntimeBase
    {
        private readonly OnnxGenAIConfigModel m_Config;

        public OnnxGenAI(OnnxGenAIConfigModel Config, Kernel kernel, ChatHistory History) : base(kernel, History)
        {
            m_Config = Config;
        }

        public override async Task GenerateAsync(string UserPrompt, CancellationToken Token, ProgressState Progress)
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

            /*promptSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                options: new FunctionChoiceBehaviorOptions()
            {
                AllowConcurrentInvocation = true,
                AllowParallelCalls = true,
            }); // set to None() for debug mode
            */

            Progress.ProgressMax = 4;
            Progress.UpdateProgressMessage("Performing inference...");

            //
            // Add the user question to the chat history
            //
            m_ChatHistory.AddUserMessage(UserPrompt);

            if (m_ChatHistory.Count == 0)
            {
                //
                // At the start of a conversation, add some tool input to the model to help it
                // answer questions about events, start ETW traces, etc.
                //
                AddInitialToolPrompt();
            }

            var charsProcessed = 0;
            var isJsonResponse = false;
            var inferenceResult = new InsightsInferenceResultModel();

            //
            // Get model response - if we are being invoked by the tool, the model response
            // will be a JSON object meant for us to parse as part of function calling.
            // Otherwise, the model response goes straight into the UI _and_ the chat history.
            //
            var fullResponse = await GetInferenceResult(Token, (content) => 
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
                        Progress.UpdateProgressMessage("Awaiting model instructions...");
                    }
                    
                    if (!isJsonResponse)
                    {
                        if (charsProcessed == 0)
                        {
                            Progress.ProgressValue = 3; // skips 3 steps
                            Progress.UpdateProgressMessage("Receiving model response...");
                            //
                            // Add a new message object to the UI, which we will continually update as
                            // characters from the response comes in.
                            //
                            inferenceResult.Type = InsightsInferenceResultModel.ContentType.ModelOutput;
                            m_ResultHistory.Add(inferenceResult);
                        }
                        inferenceResult.Content += content.Content;
                    }
                    charsProcessed++;
                }
            });

            Progress.UpdateProgressValue();

            if (!isJsonResponse)
            {
                //
                // The response was directed at the user, add it to our chat history.
                //
                m_ChatHistory.AddAssistantMessage(fullResponse);
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
                Progress.UpdateProgressMessage("Parsing model instructions...");
                var json = JsonConvert.DeserializeObject(fullResponse) as JObject;
                var prompt = await BuildRenderedPromptForFunction(json!);
                var promptSettings = GlobalStateViewModel.Instance.Settings.OnnxGenAIConfig!.PromptExecutionSettings;
                Progress.UpdateProgressValue();
                Progress.UpdateProgressMessage("Performing task...");
                var result2 = await m_Kernel.InvokePromptAsync(
                    prompt, arguments: new KernelArguments(promptSettings));
                //
                // Add a tool response to the chat history containing the rendered prompt, to give the
                // model the additional context and then issue the query again.
                //
                m_ChatHistory.Add(new() { Role = AuthorRole.Tool, Content = result2.RenderedPrompt });
                //
                // Issue the final request to the model, which contains all of the additional context
                //
                Progress.UpdateProgressValue();
                Progress.UpdateProgressMessage("Issuing final prompt...");
                fullResponse = await GetInferenceResult(Token);
                Progress.UpdateProgressValue();
                m_ChatHistory.AddAssistantMessage(fullResponse);
            }
        }

        private void AddInitialToolPrompt()
        {
            var prompt = $"Your job is to answer questions about ETW events and providers. You must respond to " +
                $"user questions in one of three ways:{Environment.NewLine}" +
                $"   1) If you have already been given additional context about the event(s) or provider(s) in question, " +
                $"use that context to answer the question directly. If the context is insufficient to accurately answer " +
                $"the user's question, select a plugin and function below to get more context.{Environment.NewLine}" +
                $"   2) If you do not have context about the event(s) or provider(s), select an appropriate plugin and function " +
                $"below.{Environment.NewLine}" +
                $"   3) If the question does not appear to relate to either ETW events or providers, ask the user to clarify." +
                $"{Environment.NewLine}" +
                $"If you select option 1 or 2, in your response, include values for parameters to the function you chose, " +
                $"gleaned from information provided by the user in their question. Consult the information below to determine " +
                $"what parameters are available for each function, and if a user does not provide a required parameter, ask " +
                $"the user to provide it. If you have all of the information you need to form all required parameters, respond " +
                $"with your selections in JSON format and do not include any additional text in your response.{Environment.NewLine}" +
                $"For example, if the user asks for details about event ID 3 produced by the provider named " +
                $"Microsoft-Antimalware-Engine and you do not have sufficient context about event ID 3, you would respond with: " +
                $"{{'plugin':'etwTraceSession','function': 'StartTrace','arguments':{{'StopOnTimeSec':5, 'Provider':" +
                $"['Microsoft-Antimalware-Engine']}}}}{Environment.NewLine}" +
                $"Here are the plugins/functions if you need them:{Environment.NewLine}";
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
            m_ChatHistory.Add(new()
            {
                Role = AuthorRole.Tool,
                Content = prompt,
            });
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
            var meta = m_Kernel.Plugins.GetFunction(plugin, functionName).Metadata;
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
            return await promptTemplate.RenderAsync(m_Kernel, kernelArgs);
        }
    }
}
