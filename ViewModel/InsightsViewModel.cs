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
using Microsoft.Win32;
using Qdrant.Client;
using etwlib;
using EtwPilot.Utilities;
using System.Text;
using Microsoft.SemanticKernel.ChatCompletion;
using EtwPilot.Sk.Plugins;
using EtwPilot.Sk.Vector;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Embeddings;
using EtwPilot.InferenceRuntimes;

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
                    OnPropertyChanged("Initialized");
                }
                ClearErrors(nameof(Initialized));
                if (!value)
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
                if (!value)
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
                if (!value)
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
                if (string.IsNullOrEmpty(value))
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
        public RelayCommand ClearCommand { get; set; }
        public RelayCommand CopyCommand { get; set; }
        public AsyncRelayCommand GenerateCommand { get; set; }
        public AsyncRelayCommand<ChatTopic> SetChatTopicCommand { get; set; }
        public AsyncRelayCommand ReinitializeCommand { get; set; }
        #endregion

        public ConcurrentObservableCollection<InsightsInferenceResultModel> ResultHistory { get; }

        private ChatHistory m_ChatHistory { get; set; }
        private bool m_ModelBusy;
        private Kernel m_Kernel;
        private EtwVectorDb? m_VectorDb;
        private OnnxGenAI? m_OnnxGenAIRuntime;
        private LlamaCpp? m_LlamaCppRuntime;
        private Ollama? m_OllamaRuntime;

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
            ClearCommand = new RelayCommand(
                Command_Clear, () => { return !m_ModelBusy; });
            CopyCommand = new RelayCommand(
                Command_Copy, () => { return true; });
            GenerateCommand = new AsyncRelayCommand(
                Command_Generate, CanExecuteGenerate);
            SetChatTopicCommand = new AsyncRelayCommand<ChatTopic>(
                Command_SetChatTopic, CanExecuteSetChatTopic);
            ReinitializeCommand = new AsyncRelayCommand(
                Command_Reinitialize, CanExecuteReinitialize);
            m_ChatHistory = new ChatHistory();
            ResultHistory = new ConcurrentObservableCollection<InsightsInferenceResultModel>();
            Prompt = string.Empty;
            Initialized = false;
            VecDbCommandsAllowed = false;
            IsManifestDataAvailable = false;
            IsEventDataAvailable = false;
            Topic = ChatTopic.Invalid;

            ErrorsChanged += (object? sender, DataErrorsChangedEventArgs e) =>
            {
                ShowErrors();
                CanExecuteChanged();
            };
        }

        public override async Task ViewModelActivated()
        {
            GlobalStateViewModel.Instance.CurrentViewModel = this;
            if (!Initialized && CanExecuteReinitialize())
            {
                await GlobalStateViewModel.Instance.g_InsightsViewModel.Initialize();
            }
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
            IsViewEnabled = false;
            ProgressState.InitializeProgress(2);
            try
            {
                var ollamaRuntimeConfig = GlobalStateViewModel.Instance.Settings.OllamaConfig;
                var onnxRuntimeConfig = GlobalStateViewModel.Instance.Settings.OnnxGenAIConfig;
                if (ollamaRuntimeConfig == null && onnxRuntimeConfig == null)
                {
                    throw new Exception("Chat completion runtime not selected.");
                }
                EraseResultHistory();
                ProgressState.UpdateProgressMessage($"Initializing Semantic Kernel....");
                await Task.Run(async () =>
                {
                    var qdrantHostUri = GlobalStateViewModel.Instance.Settings.QdrantHostUri;
                    var builder = Kernel.CreateBuilder();

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
                        var modelId  = onnxRuntimeConfig.PromptExecutionSettings.ModelId;
                        if (string.IsNullOrEmpty(modelId))
                        {
                            throw new Exception("Model ID is missing from prompt execution settings");
                        }
                        builder.AddOnnxRuntimeGenAIChatCompletion(modelId, modelPath!);
                        builder.AddBertOnnxTextEmbeddingGeneration(embeddingsModelPath, embeddingsVocabFile);
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
                        builder.AddOllamaChatCompletion(modelId, new Uri(ollamaRuntimeConfig.EndpointUri!));
                        builder.AddOllamaTextEmbeddingGeneration(ollamaRuntimeConfig.TextEmbeddingModelName!,
                            new Uri(ollamaRuntimeConfig.EndpointUri!));
                    }

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
                        var client = new QdrantClient(qdrantHostUri, grpcTimeout: TimeSpan.FromSeconds(10));
                        var health = await client.HealthAsync();
                        ProgressState.UpdateProgressMessage(
                            $"{health.Title} version {health.Version} commit {health.Commit}");
                        //
                        // Setup vector db kernel service
                        //
                        m_VectorDb = new EtwVectorDb(client, qdrantHostUri);
                        await m_VectorDb.Initialize();
                        builder.Services.AddKeyedSingleton<EtwVectorDb>(m_VectorDb);
                        //
                        // Build the kernel
                        //
                        m_Kernel = builder.Build();
                    }
                    else
                    {
                        //
                        // Build the kernel
                        //
                        m_Kernel = builder.Build();
                    }
                    ProgressState.UpdateProgressValue();
                    //
                    // Add SK plugins
                    //
                    m_Kernel.Plugins.AddFromObject(new VectorSearch(m_Kernel), "etwVectorSearch");
                    m_Kernel.Plugins.AddFromObject(new EtwTraceSession(m_Kernel), "etwTraceSession");
                    m_Kernel.Plugins.AddFromObject(new ProcessInfo(), "processInfo");
                    //
                    // Initialize an inference runtime
                    //
                    m_OnnxGenAIRuntime = null;
                    m_OllamaRuntime = null;
                    m_LlamaCppRuntime = null;

                    if (onnxRuntimeConfig != null)
                    {
                        m_OnnxGenAIRuntime = new OnnxGenAI(onnxRuntimeConfig, m_Kernel, m_ChatHistory);
                    }
                    else if (ollamaRuntimeConfig != null)
                    {
                        m_OllamaRuntime = new Ollama(ollamaRuntimeConfig, m_Kernel, m_ChatHistory);
                    }

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
            finally
            {
                IsViewEnabled = true;
            }
        }

        public override async Task Command_SettingsChanged()
        {
            var changed = GlobalStateViewModel.Instance.Settings.ChangedProperties;

            if (changed.Contains(nameof(OnnxGenAIConfigModel.PromptExecutionSettings)) ||
                changed.Contains(nameof(OnnxGenAIConfigModel.BertModelPath)) ||
                changed.Contains(nameof(OnnxGenAIConfigModel.ModelPath)) ||
                changed.Contains(nameof(OnnxGenAIConfigModel.RuntimeConfigFile)) ||
                changed.Contains(nameof(OllamaConfigModel.PromptExecutionSettings)) ||
                changed.Contains(nameof(OllamaConfigModel.EndpointUri)) ||
                changed.Contains(nameof(OllamaConfigModel.RuntimeConfigFile)) ||
                changed.Contains(nameof(OllamaConfigModel.TextEmbeddingModelName)) ||
                changed.Contains(nameof(OllamaConfigModel.ModelName)) ||
                changed.Contains(nameof(SettingsFormViewModel.QdrantHostUri)))
            {
                await ReinitializeCommand.ExecuteAsync(null);
                CanExecuteChanged();
            }
        }

        private async Task Command_SetChatTopic(ChatTopic _Topic, CancellationToken Token)
        {
            VecDbCommandsAllowed = (Topic == ChatTopic.EventData || Topic == ChatTopic.Manifests);
            if (Topic == ChatTopic.EventData || Topic == ChatTopic.Manifests)
            {
                await ShowRecordCount(Token);
            }
            ShowErrors();
            CanExecuteChanged();
        }

        private async Task Command_ImportVecDbDataFromLiveSession(List<ParsedEtwEvent>? Data, CancellationToken Token)
        {
            //
            // LiveSession is currently the active VM. Switch to Insights.
            //
            GlobalStateViewModel.Instance.g_MainWindowViewModel.RibbonTabControlSelectedIndex = 2;

            if (!Initialized)
            {
                if (!ReinitializeCommand.CanExecute(null))
                {
                    ProgressState.FinalizeProgress("Unable to initialize Insights at this time");
                    return;
                }
                await ReinitializeCommand.ExecuteAsync(null);
                if (!Initialized)
                {
                    ProgressState.FinalizeProgress("Insights failed to initialize.");
                    return;
                }
            }

            //
            // Execute sequence of commands that normally are invoked from UI interaction.
            //
            if (!SetChatTopicCommand.CanExecute(ChatTopic.EventData))
            {
                ProgressState.FinalizeProgress("Vector DB not initialized - can't set chat topic to EventData.");
                return;
            }
            SetChatTopicCommand.Execute(ChatTopic.EventData);
            if (!ImportVectorDbDataFromLiveSessionCommand.CanExecute(null))
            {
                ProgressState.FinalizeProgress("Unable to import data at this time.");
                return;
            }

            if (Data == null || Data.Count == 0)
            {
                ProgressState.FinalizeProgress($"No data provided.");
            }
            else
            {
                var error = "";
                try
                {
                    IsViewEnabled = false;
                    ProgressState.InitializeProgress(2);
                    ProgressState.UpdateProgressMessage($"Importing records....");
                    ProgressState.m_CurrentCommand = ImportVectorDbDataFromLiveSessionCommand;
                    ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Visible;
                    var textEmbSvc = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
                    await m_VectorDb!.ImportData(EtwVectorDb.s_EtwEventCollectionName, Data, textEmbSvc, Token);
                    ProgressState.FinalizeProgress($"Imported {Data.Count} records.");
                }
                catch (OperationCanceledException)
                {
                    error = "Operation cancelled";
                }
                catch (Exception ex)
                {
                    error = $"Import command failed: {ex.Message}";
                }

                if (!string.IsNullOrEmpty(error))
                {
                    ProgressState.FinalizeProgress(error);
                }
                ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Hidden;
                ProgressState.m_CurrentCommand = null;
                IsViewEnabled = true;
                await ShowRecordCount(Token);
            }
        }

        private async Task Command_ImportVecDbData(DataSource Source, CancellationToken Token)
        {
            var error = "";

            try
            {
                IsViewEnabled = false;
                ProgressState.InitializeProgress(2);
                ProgressState.m_CurrentCommand = ImportVectorDbDataCommand;
                ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Visible;

                if (Topic == ChatTopic.Manifests)
                {
                    ProgressState.UpdateProgressMessage("Waiting on data...");
                    var data = await GetImportDataFromSource<ParsedEtwManifest>(Source);
                    if (data == null || data.Count == 0)
                    {
                        throw new Exception("No data provided");
                    }
                    ProgressState.UpdateProgressValue();
                    ProgressState.UpdateProgressMessage($"Importing data...");
                    var textEmbSvc = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
                    await m_VectorDb!.ImportData(EtwVectorDb.s_EtwProviderManifestCollectionName, data, textEmbSvc, Token);
                    ProgressState.FinalizeProgress($"Imported {data.Count} records.");
                }
                else if (Topic == ChatTopic.EventData)
                {
                    ProgressState.UpdateProgressMessage("Waiting on data...");
                    var data = await GetImportDataFromSource<ParsedEtwEvent>(Source);
                    if (data == null || data.Count == 0)
                    {
                        throw new Exception("No data provided");
                    }
                    ProgressState.UpdateProgressValue();
                    //
                    // NB: Since etw event data is ephemeral, unlike provider manifests,
                    // we delete and recreate the collection on each import.
                    //
                    await m_VectorDb!.Erase(EtwVectorDb.s_EtwEventCollectionName, Token);
                    ProgressState.UpdateProgressMessage($"Importing data...");
                    var textEmbSvc = m_Kernel.GetRequiredService<ITextEmbeddingGenerationService>();
                    await m_VectorDb!.ImportData(EtwVectorDb.s_EtwEventCollectionName, data, textEmbSvc, Token);
                    ProgressState.FinalizeProgress($"Imported {data.Count} records.");
                }
                else
                {
                    throw new Exception("Unrecognized chat topic");
                }
            }
            catch (OperationCanceledException)
            {
                error = "Operation cancelled";
            }
            catch (Exception ex)
            {
                error = $"Import command failed: {ex.Message}";
            }

            if (!string.IsNullOrEmpty(error))
            {
                ProgressState.FinalizeProgress(error);
            }
            ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Hidden;
            ProgressState.m_CurrentCommand = null;
            IsViewEnabled = true;
            await ShowRecordCount(Token);
        }

        private async Task<List<T>?> GetImportDataFromSource<T>(DataSource Source)
        {
            if (Topic == ChatTopic.Manifests)
            {
                if (Source == DataSource.Live)
                {
                    var result = await Task.Run(() =>
                    {
                        return ProviderParser.GetManifests();
                    });                    
                    return result.Values.Cast<T>().ToList();
                }
                else if (Source == DataSource.XMLFile)
                {
                    return DataImporter.Import<List<T>>(ImportFormat.Xml);
                }
                else if (Source == DataSource.JSONFile)
                {
                    return DataImporter.Import<List<T>>(ImportFormat.Json);
                }
                else
                {
                    throw new Exception($"Unsupported data source {Source}");
                }
            }
            else if (Topic == ChatTopic.EventData)
            {
                if (Source == DataSource.Live)
                {
                    //
                    // Pull from the most recently active livesession
                    //
                    var vm = GlobalStateViewModel.Instance.g_SessionViewModel.GetMostRecentLiveSession();
                    if (vm == null)
                    {
                        throw new Exception("No live sessions available.");
                    }
                    var list = new List<ParsedEtwEvent>();
                    foreach (var kvp in vm.m_ProviderTraceData)
                    {
                        var data = kvp.Value.Data.AsObservable.ToList();
                        list.AddRange(data);
                    }
                    return list.Cast<T>().ToList();
                }
                else if (Source == DataSource.XMLFile)
                {
                    return DataImporter.Import<List<T>>(ImportFormat.Xml);
                }
                else if (Source == DataSource.JSONFile)
                {
                    return DataImporter.Import<List<T>>(ImportFormat.Json);
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

        private async Task ShowRecordCount(CancellationToken Token)
        {
            ulong recordsAvailableForTopic = 0;
            if (m_VectorDb != null)
            {
                if (Topic == ChatTopic.Manifests)
                {
                    recordsAvailableForTopic = await m_VectorDb.GetRecordCount(
                        EtwVectorDb.s_EtwProviderManifestCollectionName, Token);
                    IsManifestDataAvailable = recordsAvailableForTopic > 0;
                }
                else if (Topic == ChatTopic.EventData)
                {
                    recordsAvailableForTopic = await m_VectorDb.GetRecordCount(
                        EtwVectorDb.s_EtwEventCollectionName, Token);
                    IsEventDataAvailable = recordsAvailableForTopic > 0;
                }
            }
            CanExecuteChanged();
            ProgressState.EphemeralStatusText = $"{recordsAvailableForTopic} records available.";
        }

        private async Task Command_RestoreVecDbCollection(CancellationToken Token)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Multiselect = false;
            dialog.Title = $"Select a {(Topic == ChatTopic.EventData ? "data" : "manifests")} collection snapshot";
            var result = dialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            var source = dialog.FileName;
            var error = "";
            ProgressState.InitializeProgress(1);
            try
            {
                IsViewEnabled = false;
                ProgressState.m_CurrentCommand = RestoreVectorDbCollectionCommand;
                ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Visible;
                if (Topic == ChatTopic.Manifests)
                {
                    await m_VectorDb!.RestoreCollection(
                        EtwVectorDb.s_EtwProviderManifestCollectionName, source, Token);
                }
                else if (Topic == ChatTopic.EventData)
                {
                    await m_VectorDb!.RestoreCollection(
                        EtwVectorDb.s_EtwEventCollectionName, source, Token);
                }
                else
                {
                    throw new Exception("Unknown collection type");
                }
                ProgressState.FinalizeProgress($"Collection restored from {source}");
            }
            catch (OperationCanceledException)
            {
                error = "Operation cancelled";
            }
            catch (Exception ex)
            {
                error = $"Restore failed: {ex.Message}";
            }
            if (!string.IsNullOrEmpty(error))
            {
                ProgressState.FinalizeProgress($"Restore failed: {error}");
            }
            ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Hidden;
            ProgressState.m_CurrentCommand = null;
            IsViewEnabled = true;
            await ShowRecordCount(Token);
        }

        private async Task Command_SaveVecDbCollection(CancellationToken Token)
        {
            var browser = new OpenFolderDialog();
            browser.Title = "Select a location to save the collection";
            browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            var target = browser.FolderName;
            ProgressState.InitializeProgress(2);
            try
            {
                IsViewEnabled = false;
                if (Topic == ChatTopic.Manifests)
                {
                    await m_VectorDb!.SaveCollection(
                        EtwVectorDb.s_EtwProviderManifestCollectionName, target, Token);
                }
                else if (Topic == ChatTopic.EventData)
                {
                    await m_VectorDb!.SaveCollection(
                        EtwVectorDb.s_EtwEventCollectionName, target, Token);
                }
                else
                {
                    throw new Exception("Unknown collection type");
                }
                ProgressState.FinalizeProgress($"Collection saved to {target}");
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"Save failed: {ex.Message}");
            }
            finally
            {
                IsViewEnabled = true;
            }
        }

        private async Task Command_Reinitialize()
        {
            Reset();
            await Initialize();
        }

        private void Reset()
        {
            Prompt = string.Empty;
            Initialized = false;
            VecDbCommandsAllowed = false;
            IsManifestDataAvailable = false;
            IsEventDataAvailable = false;
            Topic = ChatTopic.Invalid;
            m_VectorDb = null;
        }

        private void Command_Clear()
        {
            m_ChatHistory.Clear();
        }
        private void Command_Copy()
        {
            if (m_ChatHistory.Count == 0)
            {
                return;
            }
            StringBuilder sb = new StringBuilder();
            m_ChatHistory.ToList().ForEach(r => sb.AppendLine(r.Content));
            System.Windows.Clipboard.SetText(sb.ToString());
        }

        private async Task Command_Generate(CancellationToken Token)
        {
            IsViewEnabled = false;
            m_ModelBusy = true;
            ProgressState.InitializeProgress(1); // runtime will adjust manually.
            ProgressState.m_CurrentCommand = GenerateCommand;
            ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Visible;

            try
            {
                if (m_OnnxGenAIRuntime != null)
                {
                    await m_OnnxGenAIRuntime!.GenerateAsync(Prompt, Token, ProgressState);
                }
                else if (m_OllamaRuntime != null)
                {
                    await m_OllamaRuntime!.GenerateAsync(Prompt, Token, ProgressState);
                }
                else if (m_LlamaCppRuntime != null)
                {
                    await m_LlamaCppRuntime!.GenerateAsync(Prompt, Token, ProgressState);
                }
            }
            catch (OperationCanceledException)
            {
                AddSystemMessageToResult($"Operation Canceled");
            }
            catch (Exception ex)
            {
                AddSystemMessageToResult($"Inference exception: {ex.Message}");
            }
            IsViewEnabled = true;
            ProgressState.m_CurrentCommand = null;
            ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Hidden;
            ProgressState.FinalizeProgress("");
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
                return Initialized && m_VectorDb != null && m_VectorDb.m_Initialized;
            }
            return Initialized;
        }

        private bool CanExecuteGenerate()
        {
            if (GlobalStateViewModel.Instance.Settings.HasErrors)
            {
                return false;
            }
            if (m_ModelBusy || !Initialized)
            {
                //
                // The model is performing inference or not initialized properly.
                //
                return false;
            }
            return true;
            /*
            if (PropertyHasErrors(nameof(Prompt)))
            {
                return false;
            }
            if (Topic == ChatTopic.General)
            {
                return true;
            }
            else if (Topic == ChatTopic.Manifests)
            {
                return IsManifestDataAvailable;
            }
            else if (Topic == ChatTopic.EventData)
            {
                return IsEventDataAvailable;
            }
            return false;*/
        }

        public bool CanExecuteVecDbCommand()
        {
            if (GlobalStateViewModel.Instance.Settings.HasErrors)
            {
                return false;
            }
            if (!Initialized || m_VectorDb == null)
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
            if (GlobalStateViewModel.Instance.Settings.HasErrors)
            {
                return false;
            }
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