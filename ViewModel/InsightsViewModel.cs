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
using System;
using Microsoft.SemanticKernel.Connectors.Onnx;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using EtwPilot.Sk.Plugins;
using EtwPilot.Sk.Vector;

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
        public RelayCommand<ChatTopic> SetChatTopicCommand { get; set; }
        public AsyncRelayCommand ReinitializeCommand { get; set; }
        #endregion

        public ConcurrentObservableCollection<InsightsInferenceResultModel> ResultHistory { get; }

        private ChatHistory m_ChatHistory { get; set; }
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
            ClearCommand = new RelayCommand(
                Command_Clear, () => { return !m_ModelBusy; });
            CopyCommand = new RelayCommand(
                Command_Copy, () => { return true; });
            GenerateCommand = new AsyncRelayCommand(
                Command_Generate, CanExecuteGenerate);
            SetChatTopicCommand = new RelayCommand<ChatTopic>(
                Command_SetChatTopic, CanExecuteSetChatTopic);
            ReinitializeCommand = new AsyncRelayCommand(
                Command_Reinitialize, CanExecuteReinitialize);
            m_ChatHistory = new ChatHistory();
            ResultHistory = new ConcurrentObservableCollection<InsightsInferenceResultModel>();
            Prompt = null;
            Initialized = false;
            VecDbCommandsAllowed = false;
            IsManifestDataAvailable = false;
            IsEventDataAvailable = false;
            Topic = ChatTopic.Invalid;
            m_VectorDb = new EtwVectorDb();

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
                        using (var client = new QdrantClient(qdrantHostUri, grpcTimeout: TimeSpan.FromSeconds(10)))
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
                        m_Kernel = builder.Build();
                        await m_VectorDb.Initialize(m_Kernel, qdrantHostUri, ProgressState);
                    }
                    else
                    {

                        m_Kernel = builder.Build();
                    }
                    ProgressState.UpdateProgressValue();
                    //
                    // Add SK plugins
                    //
                    m_Kernel.Plugins.AddFromObject(new VectorSearch(m_VectorDb), "etwVectorSearch");
                    m_Kernel.Plugins.AddFromObject(new EtwTraceSession(m_VectorDb), "etwTraceSession");
                    m_Kernel.Plugins.AddFromObject(new ProcessInfo(), "processInfo");
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

            if (changed.Contains(nameof(SettingsFormViewModel.ModelPath)) ||
                changed.Contains(nameof(SettingsFormViewModel.EmbeddingsModelFile)) ||
                changed.Contains(nameof(SettingsFormViewModel.ModelConfig)) ||
                changed.Contains(nameof(SettingsFormViewModel.QdrantHostUri)))
            {
                if (string.IsNullOrEmpty(GlobalStateViewModel.Instance.Settings.ModelPath) ||
                    string.IsNullOrEmpty(GlobalStateViewModel.Instance.Settings.EmbeddingsModelFile) ||
                    GlobalStateViewModel.Instance.Settings.ModelConfig == null)
                {
                    //
                    // Ignore clearing of settings that are optional from an app standpoint but
                    // prevent us from working. In this case, disable the viewmodel entirely
                    // until a valid value is provided.
                    //
                    Reset();
                }
                else
                {
                    //
                    // When any of these settings are changed, we must completely re-init
                    //
                    await ReinitializeCommand.ExecuteAsync(null);
                }
                CanExecuteChanged();
            }
        }

        private async void Command_SetChatTopic(ChatTopic _Topic)
        {
            VecDbCommandsAllowed = (Topic == ChatTopic.EventData || Topic == ChatTopic.Manifests);
            if (Topic == ChatTopic.EventData || Topic == ChatTopic.Manifests)
            {
                await ShowRecordCount();
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
                    await m_VectorDb.ImportData(Topic, Data, Token);
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
                await ShowRecordCount();
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
                    await m_VectorDb.ImportData(Topic, data, Token);
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
                    await m_VectorDb.Erase(Topic);
                    ProgressState.UpdateProgressMessage($"Importing data...");
                    await m_VectorDb.ImportData(Topic, data, Token);
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
            await ShowRecordCount();
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

        private async Task ShowRecordCount()
        {
            ulong recordsAvailableForTopic = await m_VectorDb.GetRecordCount(Topic);
            if (Topic == ChatTopic.Manifests)
            {
                IsManifestDataAvailable = recordsAvailableForTopic > 0;
            }
            else if (Topic == ChatTopic.EventData)
            {
                IsEventDataAvailable = recordsAvailableForTopic > 0;
            }
            CanExecuteChanged();
            ProgressState.EphemeralStatusText = $"{recordsAvailableForTopic} records available.";
        }

        private async Task Command_RestoreVecDbCollection()
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
                await m_VectorDb.RestoreCollection(Topic, source);
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
            await ShowRecordCount();
        }

        private async Task Command_SaveVecDbCollection()
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
                await m_VectorDb.SaveCollection(Topic, target);
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
            Prompt = null;
            Initialized = false;
            VecDbCommandsAllowed = false;
            IsManifestDataAvailable = false;
            IsEventDataAvailable = false;
            Topic = ChatTopic.Invalid;
            m_VectorDb = new EtwVectorDb();
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
            m_ModelBusy = true;
            ProgressState.InitializeProgress(2);
            ProgressState.m_CurrentCommand = GenerateCommand;
            ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Visible;

            try
            {
                IsViewEnabled = false;

                //
                // Add the user's question to the chat history.
                //
                m_ChatHistory.AddUserMessage(Prompt);

                //
                // If applicable, construct a system prompt for a multi-step process to get
                // at a meaningful reply using plugins as needed.
                //
                var systemPrompt = string.Empty;
                if (Topic == ChatTopic.Manifests)
                {
                    systemPrompt = $"If the user question relates to ETW providers or their manifests, "+
                        "respond with the name or GUID of the provider.";
                }
                else if (Topic == ChatTopic.EventData)
                {
                    systemPrompt = $"If the user question relates to specific ETW events, respond with "+
                        "as much information about the event as the user supplied in their question, in"+
                        "the following format (do NOT include values that the user did not supply in their"+
                        "question:  'This ETW event with ID <ID> (version <version>) generated "+
                        "by provider <provider name or GUID> relates to the process with ID <process ID> "+
                        "with start key <ProcessStartKey>, thread ID <threadId> from the user with SID "+
                        "<UserSid> at timestamp <Timestamp>. The event <Event level>-type info about channel"+
                        "<Channel>, task <task> and opcode <opcode>> pertaining to keywords <keywords>'";
                    m_ChatHistory.AddSystemMessage(systemPrompt);
                }

                ProgressState.UpdateProgressValue();
                ProgressState.UpdateProgressMessage("Please wait, performing inference...");
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
                return Initialized && m_VectorDb.m_Initialized;
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