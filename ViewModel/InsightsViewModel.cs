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
using System.ComponentModel;
using Meziantou.Framework.WPF.Collections;
using Microsoft.SemanticKernel;
using System.Windows.Forms;
using Qdrant.Client;
using etwlib;
using EtwPilot.Vector;
using EtwPilot.Utilities;

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

    public class InsightsViewModel : ViewModelBase
    {
        public enum DataSource
        {
            Invalid,
            XMLFile,
            JSONFile,
            Live
        }

        public enum ChatTopic
        {
            Invalid,
            General,
            Manifests,
            EventData
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

        private bool _IsManifestDataAvailable;
        public bool IsManifestDataAvailable
        {
            get => _IsManifestDataAvailable;
            set
            {
                if (_IsManifestDataAvailable != value)
                {
                    _IsManifestDataAvailable = value;
                    OnPropertyChanged("IsManifestDataAvailable");
                }
                ClearErrors(nameof(IsManifestDataAvailable));
                if (!IsManifestDataAvailable)
                {
                    AddError(nameof(IsManifestDataAvailable), "No manifest data available, import data now");
                }
            }
        }

        private bool _IsEventDataAvailable;
        public bool IsEventDataAvailable
        {
            get => _IsEventDataAvailable;
            set
            {
                if (_IsEventDataAvailable != value)
                {
                    _IsEventDataAvailable = value;
                    OnPropertyChanged("IsEventDataAvailable");
                }
                ClearErrors(nameof(IsEventDataAvailable));
                if (!IsEventDataAvailable)
                {
                    AddError(nameof(IsEventDataAvailable), "No event data available, import data now");
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
        public AsyncRelayCommand<List<ParsedEtwEvent>> ImportVectorDbDataFromLiveSessionCommand { get; set; }
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
        public InsightsViewModel() : base()
        {
            ImportVectorDbDataCommand = new AsyncRelayCommand<DataSource>(
                Command_ImportVecDbData, _ => CanExecuteVecDbCommand());
            ImportVectorDbDataFromLiveSessionCommand = new AsyncRelayCommand<List<ParsedEtwEvent>>(
                Command_ImportVecDbDataFromLiveSession, _ => CanExecuteVecDbCommand());
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
                Command_SetChatTopic, CanExecuteSetChatTopic);
            ReinitializeCommand = new AsyncRelayCommand(
                Command_Reinitialize, CanExecuteReinitialize);
            ResultHistory = new ConcurrentObservableCollection<InsightsInferenceResultModel>();
            Prompt = null;
            Initialized = false;
            VecDbCommandsAllowed = false;
            IsManifestDataAvailable = false;
            IsEventDataAvailable = false;
            Topic = ChatTopic.Invalid;
            m_VectorDb = new EtwVectorDb();

            //
            // Listen for changes to the model configuration in global settings
            // and update the command availability.
            //
            GlobalStateViewModel.Instance.Settings.PropertyChanged += 
                async (object? sender, PropertyChangedEventArgs Args) => {
                if (Args.PropertyName != "NewSettingsReady")
                {
                    return;
                }
                if (GlobalStateViewModel.Instance.Settings.HasModelRelatedUnsavedChanges)
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

        public override Task ViewModelActivated()
        {
            GlobalStateViewModel.Instance.CurrentViewModel = this;
            return Task.CompletedTask;
        }

        public async Task Initialize()
        {
            if (Initialized)
            {
                //
                // To reinitialize, invoke Command_Reinitialize
                //
                return;
            }
            ProgressState.InitializeProgress(2);
            try
            {
                if (GlobalStateViewModel.Instance.Settings.ModelConfig == null)
                {
                    throw new Exception("Model configuration missing in settings.");
                }
                EraseResultHistory();
                ProgressState.UpdateProgressMessage($"Initializing Semantic Kernel....");
                await Task.Run(async () =>
                {
                    var modelPath = GlobalStateViewModel.Instance.Settings.ModelPath;
                    var embeddingsModelFile = GlobalStateViewModel.Instance.Settings.EmbeddingsModelFile;
                    var embeddingsVocabFile = GlobalStateViewModel.Instance.Settings.ModelConfig!.EmbeddingsVocabFile;
                    var modelId = GlobalStateViewModel.Instance.Settings.ModelConfig.ModelOptions.Type;
                    var qdrantHostUri = GlobalStateViewModel.Instance.Settings.QdrantHostUri;

                    //
                    // SK builder is based on onnxruntime-genai and bert text embedding service.
                    //
                    var builder = Kernel.CreateBuilder().
                    AddOnnxRuntimeGenAIChatCompletion(modelId, modelPath).
                    AddBertOnnxTextEmbeddingGeneration(embeddingsModelFile, embeddingsVocabFile);
                    ProgressState.UpdateProgressValue();

                    //
                    // RAG is optional and uses qdrant vector db.
                    //
                    if (!string.IsNullOrEmpty(qdrantHostUri))
                    {
                        ProgressState.UpdateProgressMessage(
                            $"Initializing qdrant host {qdrantHostUri} vector db...");
                        //
                        // Quick validity test on host uri
                        //
                        using (var client = new QdrantClient(qdrantHostUri))
                        {
                            var health = await client.HealthAsync();
                            ProgressState.UpdateProgressMessage(
                                $"{health.Title} version {health.Version} commit {health.Commit}");
                        }
                        builder.AddQdrantVectorStore(qdrantHostUri, options: new()
                        {
                            //
                            // Note: must set HasNamedVectors here, even though we also set it when we
                            // create the collection from VectorDb.cs. This is due to how SK abstraction
                            // implements GetCollection()
                            //
                            HasNamedVectors = true,
                            VectorStoreCollectionFactory = m_VectorDb
                        });
                        await m_VectorDb.Initialize(m_Kernel, qdrantHostUri, ProgressState);
                    }
                    ProgressState.UpdateProgressValue();
                    m_Kernel = builder.Build();

                    //
                    // Add search plugin
                    //
                    m_Kernel.Plugins.AddFromObject(
                        new QdrantVecDbEtwSearchPlugin(m_VectorDb), "etwSearchPlugin");
                });
                ProgressState.FinalizeProgress($"Ready.");
                Initialized = true;
                CanExecuteChanged();
            }
            catch (Exception ex)
            {
                var msg = $"Model init failed: {ex.Message}";
                Trace(TraceLoggerType.Inference, TraceEventType.Error, msg);
                AddSystemMessageToResult(msg);
                ProgressState.FinalizeProgress(msg);
            }
        }

        private async Task Command_SetChatTopic(ChatTopic _Topic)
        {
            Topic = _Topic;
            VecDbCommandsAllowed = (Topic == ChatTopic.EventData || Topic == ChatTopic.Manifests);
            CanExecuteChanged();
        }

        private async Task Command_ImportVecDbDataFromLiveSession(List<ParsedEtwEvent> Data, CancellationToken Token)
        {
            try
            {
                ProgressState.InitializeProgress(2);
                ProgressState.UpdateProgressMessage($"Generating records....");
                m_CurrentCommand = ImportVectorDbDataFromLiveSessionCommand;
                CancelCommandButtonVisibility = System.Windows.Visibility.Visible;
                await ImportVecDbData(Data, Token);
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"Import command failed: {ex.Message}");
            }
            finally
            {
                CancelCommandButtonVisibility = System.Windows.Visibility.Hidden;
                m_CurrentCommand = null;
            }
        }

        private async Task Command_ImportVecDbData(DataSource Source, CancellationToken Token)
        {
            try
            {
                m_CurrentCommand = ImportVectorDbDataCommand;
                CancelCommandButtonVisibility = System.Windows.Visibility.Visible;
                ProgressState.InitializeProgress(2);
                ProgressState.UpdateProgressMessage($"Generating records....");
                if (Topic == ChatTopic.Manifests)
                {
                    if (Source == DataSource.Live)
                    {
                        var result = await Task.Run(() =>
                        {
                            return ProviderParser.GetManifests();
                        });
                        ProgressState.UpdateProgressValue();
                        await ImportVecDbData(result.Values.ToList(), Token);
                    }
                    else if (Source == DataSource.XMLFile)
                    {
                        var objects = await DataImporter.Import<List<ParsedEtwManifest>>(ImportFormat.Xml);
                        if (objects == null)
                        {
                            throw new Exception("Import operation returned no data");
                        }
                        ProgressState.UpdateProgressValue();
                        await ImportVecDbData(objects, Token);
                    }
                    else if (Source == DataSource.JSONFile)
                    {
                        var objects = await DataImporter.Import<List<ParsedEtwManifest>>(ImportFormat.Json);
                        if (objects == null)
                        {
                            throw new Exception("Import operation returned no data");
                        }
                        ProgressState.UpdateProgressValue();
                        await ImportVecDbData(objects, Token);
                    }
                    else
                    {
                        throw new Exception($"Unsupported data source {Source}");
                    }
                }
                else if (Topic == ChatTopic.EventData)
                {
                    //
                    // NB: Since etw event data is ephmeral, unlike provider manifests,
                    // we delete and recreate the collection on each import.
                    //
                    await m_VectorDb.Erase(Topic);

                    if (Source == DataSource.Live)
                    {
                        //
                        // Pull from the most recently active livesession
                        //
                        var g_MainWindowVm = UiHelper.GetGlobalResource<MainWindowViewModel>(
                            "g_MainWindowViewModel");
                        if (g_MainWindowVm == null)
                        {
                            throw new Exception("Main window VM inaccessible");
                        }
                        var vm = GlobalStateViewModel.Instance.g_SessionViewModel.GetMostRecentLiveSession();
                        if (vm == null)
                        {
                            throw new Exception("No live sessions available.");
                        }
                        ProgressState.UpdateProgressValue();
                        await ImportVecDbData(
                            vm.CurrentProviderTraceData.Data.AsObservable.ToList(), Token);
                    }
                    else if (Source == DataSource.XMLFile)
                    {
                        var objects = await DataImporter.Import<List<ParsedEtwEvent>>(ImportFormat.Xml);
                        if (objects == null)
                        {
                            throw new Exception("Import operation returned no data");
                        }
                        ProgressState.UpdateProgressValue();
                        await ImportVecDbData(objects, Token);
                    }
                    else if (Source == DataSource.JSONFile)
                    {
                        var objects = await DataImporter.Import<List<ParsedEtwEvent>>(ImportFormat.Json);
                        if (objects == null)
                        {
                            throw new Exception("Import operation returned no data");
                        }
                        ProgressState.UpdateProgressValue();
                        await ImportVecDbData(objects, Token);
                    }
                    else
                    {
                        throw new Exception($"Unsupported data source {Source}");
                    }
                }
                else
                {
                    throw new Exception($"Unsupported topic {Topic}");
                }
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"Import command failed: {ex.Message}");
            }
            finally
            {
                CancelCommandButtonVisibility = System.Windows.Visibility.Hidden;
                m_CurrentCommand = null;
            }
        }

        private async Task ImportVecDbData(dynamic Data, CancellationToken Token)
        {
            ProgressState.UpdateProgressMessage($"Importing records....");
            await m_VectorDb.ImportData(Topic, Data, Token);
            ProgressState.FinalizeProgress($"Imported {Data.Count} records.");
            if (Topic == ChatTopic.Manifests)
            {
                IsManifestDataAvailable = await m_VectorDb.GetRecordCount(Topic) > 0;
            }
            else if (Topic == ChatTopic.EventData)
            {
                IsEventDataAvailable = await m_VectorDb.GetRecordCount(Topic) > 0;
            }
            CanExecuteChanged();
        }

        private async Task Command_RestoreVecDbCollection()
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Multiselect = false;
            dialog.Title = $"Select a {(Topic == ChatTopic.EventData ? "data" : "manifests")} collection snapshot";
            var result = dialog.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            var source = dialog.FileName;

            ProgressState.InitializeProgress(1);
            try
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                await m_VectorDb.RestoreCollection(Topic, source);
                ProgressState.FinalizeProgress($"Collection restored from {source}");

                if (Topic == ChatTopic.Manifests)
                {
                    IsManifestDataAvailable = await m_VectorDb.GetRecordCount(Topic) > 0;
                }
                else if (Topic == ChatTopic.EventData)
                {
                    IsEventDataAvailable = await m_VectorDb.GetRecordCount(Topic) > 0;
                }
                CanExecuteChanged();
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"Restore failed: {ex.Message}");
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
            ProgressState.InitializeProgress(2);
            try
            {
                await m_VectorDb.SaveCollection(Topic, target);
                ProgressState.FinalizeProgress($"Collection saved to {target}");
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"Save failed: {ex.Message}");
            }
        }

        private async Task Command_Reinitialize()
        {
            Prompt = null;
            Initialized = false;
            VecDbCommandsAllowed = false;
            IsManifestDataAvailable = false;
            IsEventDataAvailable = false;
            Topic = ChatTopic.Invalid;
            m_VectorDb = new EtwVectorDb();
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
            ProgressState.InitializeProgress(2);
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
                else if (Topic == ChatTopic.EventData)
                {
                    additionalContext += $"{Environment.NewLine}" +
                        $"Use this additional context containing actual ETW data: {{{{etwSearchPlugin.SearchEtwProviderManifests $query}}}}";
                }

                ProgressState.UpdateProgressValue();
                ProgressState.UpdateProgressMessage("Please wait, performing inference...");

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
            ProgressState.FinalizeProgress(null);
            m_ModelBusy = false;
        }

        protected override Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
        {
            throw new NotImplementedException();
        }

        private void ShowErrors()
        {
            //
            // Remove past errors
            //
            EraseResultHistory(InsightsInferenceResultModel.ContentType.ErrorMessage);

            //
            // Show form errors that apply regardless of chat topic.
            //
            var fields = new string[] { nameof(Initialized), nameof(Prompt), nameof(Topic) };
            foreach (var field in fields)
            {
                if (PropertyHasErrors(field))
                {
                    var errors = GetErrors(field).Cast<string>().ToList();
                    errors.ForEach(e => AddErrorMessageToResult(e));
                }
            }
            //
            // Only show errors that apply to the selected chat topic.
            //
            switch (Topic)
            {
                case ChatTopic.General:
                    {
                        //
                        // No additional errors are pertinent.
                        //
                        break;
                    }
                case ChatTopic.Manifests:
                    {
                        if (PropertyHasErrors(nameof(IsManifestDataAvailable)))
                        {
                            var errors = GetErrors(nameof(IsManifestDataAvailable)).Cast<string>().ToList();
                            errors.ForEach(e => AddErrorMessageToResult(e));
                        }
                        break;
                    }
                case ChatTopic.EventData:
                    {
                        if (PropertyHasErrors(nameof(IsEventDataAvailable)))
                        {
                            var errors = GetErrors(nameof(IsEventDataAvailable)).Cast<string>().ToList();
                            errors.ForEach(e => AddErrorMessageToResult(e));
                        }
                        break;
                    }
                default:
                    {
                        break;
                    }
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

        private void EraseResultHistory(InsightsInferenceResultModel.ContentType ContentType = InsightsInferenceResultModel.ContentType.None)
        {
            if (ContentType != InsightsInferenceResultModel.ContentType.None)
            {
                ResultHistory.Where(item => item.Type == ContentType).ToList().
                    ForEach(item => ResultHistory.Remove(item));
            }
            else
            {
                ResultHistory.ToList().ForEach(item => ResultHistory.Remove(item));
            }
        }

        private bool CanExecuteSetChatTopic(ChatTopic Topic)
        {
            if (Topic == ChatTopic.Manifests || Topic == ChatTopic.EventData)
            {
                return Initialized && m_VectorDb.m_Initialized;
            }
            return Initialized;
        }

        private bool CanExecuteGenerate()
        {
            if (m_ModelBusy || !Initialized)
            {
                //
                // The model is performing inference or not initialized properly.
                //
                return false;
            }
            if (Topic == ChatTopic.General && !PropertyHasErrors(nameof(Prompt)))
            {
                //
                // The topic is general chat and prompt is valid.
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
            if (!Initialized)
            {
                return false;
            }
            if (Topic != ChatTopic.EventData && Topic != ChatTopic.Manifests)
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
            return true;
        }

        private bool CanExecuteReinitialize()
        {
            return !m_ModelBusy;
        }

        private void CanExecuteChanged()
        {
            GenerateCommand.NotifyCanExecuteChanged();
            ImportVectorDbDataCommand.NotifyCanExecuteChanged();
            RestoreVectorDbCollectionCommand.NotifyCanExecuteChanged();
            SaveVectorDbCollectionCommand.NotifyCanExecuteChanged();
            SetChatTopicCommand.NotifyCanExecuteChanged();
        }
    }
}