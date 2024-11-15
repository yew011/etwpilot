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
using EtwPilot.Utilities;
using System.ComponentModel;
using Meziantou.Framework.WPF.Collections;
using Microsoft.SemanticKernel;
using System.Windows.Forms;
using Qdrant.Client;
using etwlib;

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
        internal enum DataSource
        {
            Invalid,
            File,
            Live
        }

        internal enum ChatTopic
        {
            Invalid,
            General,
            Manifests,
            Data
        }

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

        #region observable properties

        private ChatTopic _Topic;
        public ChatTopic Topic
        {
            get => _Topic;
            set
            {
                if (_Topic != value)
                {
                    _Topic = value;
                    OnPropertyChanged("Topic");
                }

                ClearErrors(nameof(Topic));
                if (value == ChatTopic.Invalid)
                {
                    AddError(nameof(Topic), "Please choose a topic.");
                }
            }
        }

        private bool _VecDbCommandsAllowed;
        public bool VecDbCommandsAllowed
        {
            get => _VecDbCommandsAllowed;
            set
            {
                if (_VecDbCommandsAllowed != value)
                {
                    _VecDbCommandsAllowed = value;
                    OnPropertyChanged("VecDbCommandsAllowed");
                }
            }
        }

        private bool _Initialized;
        public bool Initialized
        {
            get => _Initialized;
            set
            {
                if (_Initialized != value)
                {
                    _Initialized = value;
                    OnPropertyChanged("ConfigurationReady");
                }
                ClearErrors(nameof(Initialized));
                if (!Initialized)
                {
                    AddError(nameof(Initialized), "Insights is not initialized.");
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
        public AsyncRelayCommand<DataSource> ImportVectorDbDataCommand { get; set; }
        public AsyncRelayCommand RestoreVectorDbCollectionCommand { get; set; }
        public AsyncRelayCommand SaveVectorDbCollectionCommand { get; set; }
        public AsyncRelayCommand ClearCommand { get; set; }
        public AsyncRelayCommand CancelCommand { get; set; }
        public AsyncRelayCommand GenerateCommand { get; set; }
        public AsyncRelayCommand<ChatTopic> SetChatTopicCommand { get; set; }
        public AsyncRelayCommand ReinitializeCommand { get; set; }

        #endregion

        public ConcurrentObservableCollection<InsightsInferenceResultModel> ResultHistory { get; }
        private CancellationTokenSource _cancellationTokenSource;
        private bool m_ModelBusy;
        private Kernel m_Kernel;
        private EtwVectorDb m_VectorDb;

        public InsightsViewModel()
        {
            ImportVectorDbDataCommand = new AsyncRelayCommand<DataSource>(
                Command_ImportVecDbData, _ => CanExecuteVecDbCommand());
            RestoreVectorDbCollectionCommand = new AsyncRelayCommand(
                Command_RestoreVecDbCollection, CanExecuteVecDbCommand);
            SaveVectorDbCollectionCommand = new AsyncRelayCommand(
                Command_SaveVecDbCollection, CanExecuteVecDbCommand);
            ClearCommand = new AsyncRelayCommand(
                Command_Clear, () => { return !m_ModelBusy; });
            CancelCommand = new AsyncRelayCommand(
                Command_Cancel, () => { return true; });
            GenerateCommand = new AsyncRelayCommand(
                Command_Generate, CanExecuteGenerate);
            SetChatTopicCommand = new AsyncRelayCommand<ChatTopic>(
                Command_SetChatTopic, _ => { return !m_ModelBusy; });
            ReinitializeCommand = new AsyncRelayCommand(
                Command_Reinitialize, CanExecuteReinitialize);
            ResultHistory = new ConcurrentObservableCollection<InsightsInferenceResultModel>();
            Prompt = null;
            Initialized = false;
            VecDbCommandsAllowed = false;
            Topic = ChatTopic.Invalid;
            m_VectorDb = new EtwVectorDb();

            //
            // Listen for changes to the model configuration in global settings
            // and update the command availability.
            //
            StateManager.Settings.PropertyChanged += 
                async (object? sender, PropertyChangedEventArgs Args) => {
                    if (Args.PropertyName != "NewSettingsReady")
                    {
                        return;
                    }
                    if (StateManager.Settings.HasModelRelatedUnsavedChanges)
                    {
                        await Initialize();
                    }
                    CanExecuteChanged();
                };

            ErrorsChanged += (object? sender, DataErrorsChangedEventArgs e) =>
            {
                ShowErrors();
                CanExecuteChanged();
            };
        }

        public async Task Initialize()
        {
            StateManager.ProgressState.InitializeProgress(2);
            try
            {
                StateManager.ProgressState.UpdateProgressMessage($"Initializing model....");
                await InitializeModel();
                StateManager.ProgressState.UpdateProgressValue();
                StateManager.ProgressState.UpdateProgressMessage($"Initializing vector db....");
                var qdrantHostUri = StateManager.Settings.QdrantHostUri;
                await m_VectorDb.Initialize(m_Kernel, StateManager, qdrantHostUri);
                StateManager.ProgressState.UpdateProgressValue();
                StateManager.ProgressState.FinalizeProgress($"Ready.");
                Initialized = true;
                CanExecuteChanged();
            }
            catch (Exception ex)
            {
                var msg = $"Model init failed: {ex.Message}";
                Trace(TraceLoggerType.Inference, TraceEventType.Error, msg);
                AddSystemMessageToResult(msg);
            }
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

        private async Task Command_SetChatTopic(ChatTopic _Topic)
        {
            Topic = _Topic;
            VecDbCommandsAllowed = (Topic == ChatTopic.Data || Topic == ChatTopic.Manifests);
            if (Topic == ChatTopic.Manifests)
            {
                var count = await m_VectorDb.GetManifestRecordCount();
                ClearErrors("DummyField");
                if (count == 0)
                {
                    AddError("DummyField", "No manifest data exists. Please import data now.");
                }
                else
                {
                    AddSystemMessageToResult($"Manifest collection has {count} records available.");
                }
            }
            else if (Topic == ChatTopic.Data)
            {
                var count = await m_VectorDb.GetDataRecordCount();
                ClearErrors("DummyField");
                if (count == 0)
                {
                    AddError("DummyField", "No ETW data exists. Please import data now.");
                }
                else
                {
                    AddSystemMessageToResult($"Data collection has {count} records available.");
                }
            }
            CanExecuteChanged();
        }

        private async Task Command_ImportVecDbData(DataSource Source)
        {
            try
            {
                if (Topic == ChatTopic.Manifests)
                {
                    if (Source == DataSource.Live)
                    {
                        StateManager.ProgressState.InitializeProgress(2);
                        StateManager.ProgressState.UpdateProgressMessage($"Generating records....");
                        var result = await Task.Run(() =>
                        {
                            return ProviderParser.GetManifests();
                        });
                        StateManager.ProgressState.UpdateProgressValue();
                        await m_VectorDb.ImportManifestData(result.Values.ToList());
                    }
                    else if (Source == DataSource.File)
                    {

                    }
                    else
                    {
                        throw new Exception($"Unsupported data source {Source}");
                    }
                    StateManager.ProgressState.FinalizeProgress($"Import successful.");
                }
                else if (Topic == ChatTopic.Data)
                {
                    await m_VectorDb.CreateDataCollection();
                }
                else
                {
                    throw new Exception($"Unsupported topic {Topic}");
                }
            }
            catch (Exception ex)
            {
                StateManager.ProgressState.FinalizeProgress($"Import failed: {ex.Message}");
            }
        }

        private async Task Command_RestoreVecDbCollection()
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Multiselect = false;
            dialog.Title = $"Select a {(Topic == ChatTopic.Data ? "data" : "manifests")} collection snapshot";
            var result = dialog.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            var source = dialog.FileName;

            StateManager.ProgressState.InitializeProgress(1);
            try
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                if (Topic == ChatTopic.Manifests)
                {
                    await m_VectorDb.RestoreManifestCollection(source);
                }
                else if (Topic == ChatTopic.Data)
                {
                    await m_VectorDb.RestoreDataCollection(source);
                }
                StateManager.ProgressState.FinalizeProgress($"Collection restored from {source}");
            }
            catch (Exception ex)
            {
                StateManager.ProgressState.FinalizeProgress($"Restore failed: {ex.Message}");
            }
        }

        private async Task Command_SaveVecDbCollection()
        {
            var browser = new FolderBrowserDialog();
            browser.Description = "Select a location to save the collection";
            browser.RootFolder = Environment.SpecialFolder.MyComputer;
            var result = browser.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            var target = browser.SelectedPath;
            StateManager.ProgressState.InitializeProgress(2);
            try
            {
                if (Topic == ChatTopic.Manifests)
                {
                    await m_VectorDb.SaveManifestCollection(target);
                }
                else if (Topic == ChatTopic.Data)
                {
                    await m_VectorDb.SaveDataCollection(target);
                }
                StateManager.ProgressState.FinalizeProgress($"Collection saved to {target}");
            }
            catch (Exception ex)
            {
                StateManager.ProgressState.FinalizeProgress($"Save failed: {ex.Message}");
            }
        }

        private async Task Command_Reinitialize()
        {
            Initialized = false;
            await Initialize();
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
            StateManager.ProgressState.InitializeProgress(1);
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                var userInput = new InsightsInferenceResultModel
                {
                    Content = Prompt,
                    Type = InsightsInferenceResultModel.ContentType.UserInput
                };
                ResultHistory.Add(userInput);

                InsightsInferenceResultModel result = null;
                var additionalContext = string.Empty;

                if (Topic == ChatTopic.Manifests)
                {
                    additionalContext += $"{Environment.NewLine}" +
                        $"Use this additional context on related ETW provider manifests: {{{{etwSearchPlugin.SearchEtwProviderManifests $query}}}}";
                }
                else if (Topic == ChatTopic.Data)
                {
                    additionalContext += $"{Environment.NewLine}" +
                        $"Use this additional context containing actual ETW data: {{{{etwSearchPlugin.SearchEtwProviderManifests $query}}}}";
                }

                StateManager.ProgressState.UpdateProgressValue();
                StateManager.ProgressState.UpdateProgressMessage("Please wait, performing inference...");

                await Task.Run(async () =>
                {
                    await foreach (var chatUpdate in m_Kernel.InvokePromptStreamingAsync<StreamingChatMessageContent>(
                        promptTemplate: @$"Question: {{{{$query}}}}{additionalContext}",
                        arguments: new KernelArguments()
                        {
                            { "query", userInput.Content }
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
                });
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
            StateManager.ProgressState.FinalizeProgress(null);
            m_ModelBusy = false;
        }

        private async Task InitializeModel()
        {
            await Task.Run(async () =>
            {
                var modelPath = StateManager.Settings.ModelPath;
                var embeddingsModelFile = StateManager.Settings.EmbeddingsModelFile;
                var embeddingsVocabFile = StateManager.Settings.ModelConfig!.EmbeddingsVocabFile;
                var modelId = StateManager.Settings.ModelConfig.ModelOptions.Type;
                var qdrantHostUri = StateManager.Settings.QdrantHostUri;

                //
                // Quick validity test on host uri
                //
                using (var client = new QdrantClient(qdrantHostUri))
                {
                    _ = await client.HealthAsync();
                }

                //
                // Create SK kernel from builder, based on onnxruntime-genai and bert text
                // embedding service using qdrant vector db for RAG.
                // Note: must set HasNamedVectors here, even though we also set it when we
                // create the collection from VectorDb.cs. This is due to how SK abstraction
                // implements GetCollection()
                //
                var builder = Kernel.CreateBuilder();
                builder.AddQdrantVectorStore(qdrantHostUri, options: new()
                {
                    HasNamedVectors = true,
                    VectorStoreCollectionFactory = new EtwCollectionFactory()
                });
                builder.AddOnnxRuntimeGenAIChatCompletion(modelId, modelPath)
                       .AddBertOnnxTextEmbeddingGeneration(embeddingsModelFile, embeddingsVocabFile);
                m_Kernel = builder.Build();
                m_Kernel.Plugins.AddFromObject(new QdrantVecDbEtwSearchPlugin(m_Kernel), "etwSearchPlugin");
            });
        }

        private void ShowErrors()
        {
            //
            // Remove past errors
            //
            ResultHistory.Where(item => item.Type == InsightsInferenceResultModel.ContentType.ErrorMessage).ToList().
                ForEach(item => ResultHistory.Remove(item));
            var errors = GetErrors(null);
            var modelInputErrors = GetErrors(nameof(m_VectorDb)).Cast<string>().ToList();
            foreach (var error in errors)
            {
                if (modelInputErrors.Contains(error) && Topic == ChatTopic.General)
                {
                    //
                    // The general topic mode overrides this as an error, because RAG model input
                    // isn't needed.
                    //
                    continue;
                }
                AddErrorMessageToResult(error.ToString()!);
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

        private void AddErrorMessageToResult(string Message)
        {
            var message = Message.Substring(0, Math.Min(Message.Length, 5000));
            ResultHistory.Add(new InsightsInferenceResultModel()
            {
                Type = InsightsInferenceResultModel.ContentType.ErrorMessage,
                Content = message
            });
        }

        private bool CanExecuteGenerate()
        {
            if (m_ModelBusy)
            {
                //
                // The model is performing inference.
                //
                return false;
            }
            if (Topic == ChatTopic.General && !PropertyHasErrors(nameof(Prompt)) && !PropertyHasErrors(nameof(Initialized)))
            {
                //
                // The topic is general chat, prompt is valid, and the model is initialized
                //
                return true;
            }
            //
            // Otherwise, all form errors apply.
            //
            return !HasErrors;
        }

        public bool CanExecuteVecDbCommand()
        {
            if (Topic != ChatTopic.Data && Topic != ChatTopic.Manifests)
            {
                //
                // Vector db operations are not available in general topic mode.
                //
                return false;
            }
            if (m_ModelBusy)
            {
                //
                // The model is performing inference.
                //
                return false;
            }
            //
            // The only form error that applies is configuration (prompt not needed yet)
            //
            return !PropertyHasErrors(nameof(Initialized));
        }

        private bool CanExecuteReinitialize()
        {
            if (m_ModelBusy)
            {
                //
                // The model is performing inference.
                //
                return false;
            }
            return true;
        }

        private void CanExecuteChanged()
        {
            GenerateCommand.NotifyCanExecuteChanged();
            ImportVectorDbDataCommand.NotifyCanExecuteChanged();
            RestoreVectorDbCollectionCommand.NotifyCanExecuteChanged();
            SaveVectorDbCollectionCommand.NotifyCanExecuteChanged();
        }
    }
}