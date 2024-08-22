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
using CommunityToolkit.Mvvm.Input;
using EtwPilot.Model;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using EtwPilot.Utilities;
using System.ComponentModel;
using Meziantou.Framework.WPF.Collections;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

//
// Remove supression after onnx connector is out of alpha
//
#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0050
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0020

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;

    internal class InsightsViewModel : ViewModelBase
    {
        private struct OutputToken
        {
            public OutputToken(int _Id, string _Value)
            {
                Id = _Id;
                DecodedValue = _Value;
            }

            public int Id;
            public string DecodedValue;
        }

        public enum InferenceType
        {
            None,
            EtwRecordsAsCsv = 1,
            ChatOnly = 2,
            Max
        }

        public class Facts
        {
            public Dictionary<string, string> m_Facts;

            public Facts()
            {
                m_Facts = new Dictionary<string, string>();
            }
        }

        #region observable properties

            private bool _ChatOnlyMode;
        public bool ChatOnlyMode
        {
            get => _ChatOnlyMode;
            set
            {
                if (_ChatOnlyMode != value)
                {
                    _ChatOnlyMode = value;
                    OnPropertyChanged("ChatOnlyMode");
                }
            }
        }

        private bool _ConfigurationReady;
        public bool ConfigurationReady
        {
            get => _ConfigurationReady;
            set
            {
                if (_ConfigurationReady != value)
                {
                    _ConfigurationReady = value;
                    OnPropertyChanged("ConfigurationReady");
                }
                ClearErrors(nameof(ConfigurationReady));
                if (!ConfigurationReady)
                {
                    AddError(nameof(ConfigurationReady), "Configuration is invalid.");
                }
            }
        }

        private string _Prompt;
        public string Prompt
        {
            get => _Prompt;
            set
            {
                if (_Prompt != value)
                {
                    _Prompt = value;
                    OnPropertyChanged("Prompt");
                }
                ClearErrors(nameof(Prompt));
                if (string.IsNullOrEmpty(Prompt))
                {
                    AddError(nameof(Prompt), "Enter a prompt");
                }
            }
        }

        #endregion

        #region commands
        public AsyncRelayCommand NewProviderVecDbCommand { get; set; }
        public AsyncRelayCommand LoadProviderVecDbCommand { get; set; }
        public AsyncRelayCommand ClearCommand { get; set; }
        public AsyncRelayCommand CancelCommand { get; set; }
        public AsyncRelayCommand GenerateCommand { get; set; }
        public AsyncRelayCommand ToggleChatOnlyModeCommand { get; set; }
        public AsyncRelayCommand ReloadModelCommand { get; set; }

        #endregion

        public ConcurrentObservableCollection<InsightsInferenceResultModel> ResultHistory { get; }
        private CancellationTokenSource _cancellationTokenSource;
        private bool m_Initialized;
        private bool m_ModelBusy;
        private int m_TokensUsed;
        private Kernel m_Kernel;
        private InsightsConfigurationModel? m_Configuration;
        private Facts m_Facts;

        public InsightsViewModel()
        {
            m_Configuration = null;
            Prompt = null;
            ConfigurationReady = IsConfigurationReady();
            ResultHistory = new ConcurrentObservableCollection<InsightsInferenceResultModel>();
            m_Facts = new Facts();
            NewProviderVecDbCommand = new AsyncRelayCommand(
                Command_NewProviderVecDb, () => { return !m_ModelBusy && IsConfigurationReady(); });
            LoadProviderVecDbCommand = new AsyncRelayCommand(
                Command_LoadProviderVecDb, () => { return !m_ModelBusy; });
            ClearCommand = new AsyncRelayCommand(
                Command_Clear, () => { return !m_ModelBusy; });
            CancelCommand = new AsyncRelayCommand(
                Command_Cancel, () => { return true; });
            GenerateCommand = new AsyncRelayCommand(
                Command_Generate, CanExecuteGenerate);
            ToggleChatOnlyModeCommand = new AsyncRelayCommand(
                Command_ChatOnlyMode, () => { return !m_ModelBusy; });
            ReloadModelCommand = new AsyncRelayCommand(
                Command_ReloadModel, CanExecuteReloadModel);
            m_TokensUsed = 0;

            //
            // Listen for changes to the model configuration in global settings
            // and update the command availability.
            //
            StateManager.Settings.PropertyChanged += 
                (object? sender, PropertyChangedEventArgs Args) => {
                    ConfigurationReady = IsConfigurationReady();
                    GenerateCommand.NotifyCanExecuteChanged();
                };

            ErrorsChanged += (obj, e) =>
            {
                ShowErrors();
            };

            PropertyChanged += (obj, p) =>
            {
                GenerateCommand.NotifyCanExecuteChanged();
            };

            //
            // Initally, know facts are loaded, so set an error. This will be cleared
            // when user loads input facts. Note this is ignored in chat-only mode.
            //
            // Note: There is no ObservableDictionary in c# core.
            //
            AddError(nameof(m_Facts), "Load facts");
            ShowErrors();
        }

        public void LoadData(LiveSessionViewModel LiveSession)
        {
            //
            // TODO: Convert live session's ParsedEtwEvents to "Facts" using some sort of template.. autogenerated by AI?
            //
            /*
            m_ModelBusy = true;
            StateManager.ProgressState.InitializeProgress(1);
            //
            // Prepare input data
            //
            StateManager.ProgressState.UpdateProgressMessage("Preparing live session data...");
            StateManager.ProgressState.UpdateProgressValue();
            var list = LiveSession.CurrentProviderTraceData.Data.ToList();
            var data = DataExporter.GetDataAsCsv(list);
            if (data == null || data.Length == 0)
            {
                StateManager.ProgressState.FinalizeProgress("No data available for inference.");
                m_ModelBusy = false;
                return;
            }
            m_Facts.Clear();
            ModelInputData.Clear();
            ModelInputData.Add(data);
            m_ModelBusy = false;
            GenerateCommand.NotifyCanExecuteChanged();*/
        }

        public void LoadFacts(string FilePath)
        {
            m_ModelBusy = true;
            StateManager.ProgressState.InitializeProgress(2);
            if (!File.Exists(FilePath))
            {
                StateManager.ProgressState.FinalizeProgress("File does not exist.");
                m_ModelBusy = false;
                return;
            }
            StateManager.ProgressState.UpdateProgressMessage($"Loading facts from {FilePath}...");
            StateManager.ProgressState.UpdateProgressValue();

            try
            {
                var settings = new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error,
                    Error = (sender, eventArgs) => {
                        Console.WriteLine(eventArgs.ErrorContext.Error.Message);  // or write to a log
                        eventArgs.ErrorContext.Handled = true;
                    }
                };
                var facts = JsonConvert.DeserializeObject<Facts>(FilePath, settings);
                if (facts == null)
                {
                    StateManager.ProgressState.FinalizeProgress("No data available.");
                    return;
                }
                m_Facts = facts;
                StateManager.ProgressState.FinalizeProgress($"Facts successfully loaded from {FilePath}");
                m_ModelBusy = false;
                GenerateCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                m_ModelBusy = false;
                StateManager.ProgressState.FinalizeProgress($"Exception loading facts: {ex.Message}");
                GenerateCommand.NotifyCanExecuteChanged();
                return;
            }
        }

        private async Task Command_ChatOnlyMode()
        {
            ChatOnlyMode = !ChatOnlyMode;
            ShowErrors();
            ReloadModelCommand.NotifyCanExecuteChanged();
        }

        private async Task Command_NewProviderVecDb()
        {
            if (!m_Initialized)
            {
                var result = await Reload();
                if (!result)
                {
                    return;
                }
            }
            var embeddingService = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var db = new EtwProviderManifestVectorDb(m_Kernel, StateManager);
            await db.Create(10);
        }

        private async Task Command_LoadProviderVecDb()
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Multiselect = false;

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            var vm = UiHelper.GetGlobalResource<MainWindowViewModel>("g_MainWindowViewModel");
            if (vm == null)
            {
                return;
            }
            vm.CurrentViewModel = this;
            LoadFacts(dialog.FileName);
        }

        private async Task Command_ReloadModel()
        {
            _ = await Reload();
        }

        private async Task Command_Clear()
        {
            ResultHistory.Clear();
        }

        private async Task Command_Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task Command_Generate()
        {
            m_ModelBusy = true;
            StateManager.ProgressState.InitializeProgress(4);
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                //
                // Initialize the model once if necessary
                //
                if (!m_Initialized)
                {
                    var result = await InitializeModel();
                    if (!result)
                    {
                        m_ModelBusy = false;
                        return;
                    }
                }
                else
                {
                    StateManager.ProgressState.UpdateProgressValue();
                }

                var userInput = new InsightsInferenceResultModel
                {
                    //Content = $"<|user|>{Prompt}<|end><|assistant|>",
                    Content = Prompt,
                    Type = InsightsInferenceResultModel.ContentType.UserInput
                };

                ResultHistory.Add(userInput);
                await CaptureInferenceResults(userInput.Content, "");
            }
            catch (OperationCanceledException)
            {
                AddSystemMessageToResult($"Operation Canceled");
            }
            catch (Exception ex)
            {
                AddSystemMessageToResult($"Inference exception: {ex.Message}");
                _cancellationTokenSource?.Cancel();
            }
            StateManager.ProgressState.FinalizeProgress(
                $"Tokens used {m_TokensUsed}/{m_Configuration!.GenAIConfig.ModelOptions.ContextLength}");
            m_ModelBusy = false;
        }

        private async Task<bool> InitializeModel()
        {
            StateManager.ProgressState.UpdateProgressMessage("Initializing AI model...");
            m_ModelBusy = true;
            var result = await Task.Run(async () =>
            {
                try
                {
                    var modelPath = m_Configuration!.ModelPath;
                    var embeddingsModelFile = m_Configuration!.EmbeddingsModelFile;
                    var embeddingsVocabFile = m_Configuration!.EmbeddingsVocabFile;
                    var modelId = m_Configuration.GenAIConfig.ModelOptions.Type;

                    //
                    // Create SK kernel from builder, based on onnxruntime-genai and bert text
                    // embedding service using qdrant vector db for RAG.
                    //
                    var builder = Kernel.CreateBuilder().AddQdrantVectorStore("localhost");
                    builder.AddOnnxRuntimeGenAIChatCompletion(modelId, modelPath)
                           .AddBertOnnxTextEmbeddingGeneration(embeddingsModelFile, embeddingsVocabFile);
                    m_Kernel = builder.Build();
                    m_Kernel.Plugins.AddFromObject(new QdrantVecDbEtwSearchPlugin(m_Kernel), "etwSearchPlugin");
                    m_Initialized = true;
                    StateManager.ProgressState.UpdateProgressValue();
                }
                catch (Exception ex)
                {
                    var msg = $"Model init failed: {ex.Message}";
                    Trace(TraceLoggerType.Inference, TraceEventType.Error, msg);
                    AddSystemMessageToResult(msg);
                    StateManager.ProgressState.FinalizeProgress("Initialization failed.");
                    return false;
                }
                return true;
            });
            m_ModelBusy = false;
            return result;
        }

        private async Task CaptureInferenceResults(string UserInput, string RecallCollectionName)
        {
            InsightsInferenceResultModel result = null;
            await foreach (var chatUpdate in m_Kernel.InvokePromptStreamingAsync<StreamingChatMessageContent>(

                promptTemplate: @"""{{etwSearchPlugin.SearchEtwProviderAsync $query}}. {{$query}}""",
                arguments: new KernelArguments()
                {
                    { "input", UserInput },
                    { "collection", "manifests" }
                }))
            {
                if (result == null)
                {
                    if (string.IsNullOrWhiteSpace(chatUpdate.Content))
                    {
                        continue;
                    }
                    ResultHistory.Add(result = new InsightsInferenceResultModel());
                }
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                result.Content += chatUpdate.Content;
            }
        }

        private async Task<bool> Reload()
        {
            m_ModelBusy = true;
            ResultHistory.Clear();
            m_Initialized = false;
            m_TokensUsed = 0;
            _cancellationTokenSource = new CancellationTokenSource();
            StateManager.ProgressState.InitializeProgress(1);
            var result = await InitializeModel();
            if (!result)
            {
                m_ModelBusy = false;
                return false;
            }
            StateManager.ProgressState.FinalizeProgress("");
            m_ModelBusy = false;
            return true;
        }

        private void ShowErrors()
        {
            ResultHistory.Clear();
            var errors = GetErrors(null);
            var modelInputErrors = GetErrors(nameof(m_Facts)).Cast<string>().ToList();
            foreach (var error in errors)
            {
                if (modelInputErrors.Contains(error) && ChatOnlyMode)
                {
                    //
                    // The "chat only" option overrides this as an error, because model input
                    // isn't needed for chat mode.
                    //
                    continue;
                }
                AddSystemMessageToResult(error.ToString()!);
            }
        }

        private void AddSystemMessageToResult(string Message)
        {
            var message = Message.Substring(0, Math.Min(Message.Length, 5000));
            ResultHistory.Add(new InsightsInferenceResultModel()
            {
                Type = InsightsInferenceResultModel.ContentType.SystemMessage,
                Content = message
            });
        }

        private bool CanExecuteGenerate()
        {
            if (!ConfigurationReady || m_ModelBusy)
            {
                return false;
            }
            if (!HasErrors)
            {
                return true;
            }
            if (ChatOnlyMode && HasErrors)
            {
                //
                // If there are errors, but there's only one error and it's on ModelInputData,
                // then the "chat only" option overrides this as an error, because model input
                // isn't needed for chat mode.
                //
                var errors = GetErrors(null).Cast<string>().ToList();
                if (errors.Count == 1)
                {
                    var error = GetErrors(nameof(m_Facts)).Cast<string>().ToList();
                    if (error.Count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool CanExecuteReloadModel()
        {
            if (m_ModelBusy)
            {
                return false;
            }

            return ChatOnlyMode;
        }

        private bool IsConfigurationReady()
        {
            m_Configuration = InsightsConfigurationModel.Load(
                StateManager.Settings.ModelPath, 
                StateManager.Settings.EmbeddingsModelFile);
            return m_Configuration != null;
        }
    }
}