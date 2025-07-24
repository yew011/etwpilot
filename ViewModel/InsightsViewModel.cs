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
using etwlib;
using EtwPilot.Model;
using EtwPilot.Sk;
using EtwPilot.Sk.Plugins;
using EtwPilot.Sk.Vector;
using EtwPilot.Utilities;
using Meziantou.Framework.WPF.Collections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;
    using static System.Runtime.InteropServices.JavaScript.JSType;

    public class InsightsViewModel : ViewModelBase
    {
        public enum DataSource
        {
            Invalid,
            XMLFile,
            JSONFile,
            Live,
            CollectionSnapshot
        }

        #region observable properties

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
        public AsyncRelayCommand<DataSource> ImportManifestsVectorDbDataCommand { get; set; }
        public AsyncRelayCommand<DataSource> ImportEtwEventsVectorDbDataCommand { get; set; }
        public AsyncRelayCommand<List<ParsedEtwEvent>> ImportVectorDbDataFromLiveSessionCommand { get; set; }
        public AsyncRelayCommand ExportManifestsVectorDbCommand { get; set; }
        public AsyncRelayCommand ExportEtwEventsVectorDbCommand { get; set; }
        public RelayCommand ClearCommand { get; set; }
        public RelayCommand CopyCommand { get; set; }
        public AsyncRelayCommand GenerateCommand { get; set; }
        public AsyncRelayCommand ReinitializeCommand { get; set; }
        public AsyncRelayCommand RefreshConvoCommand { get; set; }
        #endregion

        private bool m_ModelBusy;
        public ConcurrentObservableCollection<InsightsInferenceResultModel> m_ResultHistory { get; private set; } =
            new ConcurrentObservableCollection<InsightsInferenceResultModel>();
        public Kernel m_Kernel { get; private set; }

        public InsightsViewModel() : base()
        {
            ImportManifestsVectorDbDataCommand = new AsyncRelayCommand<DataSource>(
                Command_ImportManifestVecDbData, _ => CanExecuteVecDbCommand());
            ImportEtwEventsVectorDbDataCommand = new AsyncRelayCommand<DataSource>(
                Command_ImportEtwEventVecDbData, _ => CanExecuteVecDbCommand());
            ImportVectorDbDataFromLiveSessionCommand = new AsyncRelayCommand<List<ParsedEtwEvent>>(
                Command_ImportVecDbDataFromLiveSession, _ => CanExecuteVecDbCommand());
            ExportManifestsVectorDbCommand = new AsyncRelayCommand(
                Command_ExportManifestsVectorDb, CanExecuteVecDbCommand);
            ExportEtwEventsVectorDbCommand = new AsyncRelayCommand(
                Command_ExportEtwEventsVectorDb, CanExecuteVecDbCommand);
            ClearCommand = new RelayCommand(
                Command_Clear, () => { return !m_ModelBusy && m_Kernel != null; });
            CopyCommand = new RelayCommand(
                Command_Copy, () => { return m_Kernel != null; });
            GenerateCommand = new AsyncRelayCommand(
                Command_Generate, CanExecuteGenerate);
            ReinitializeCommand = new AsyncRelayCommand(
                Command_Reinitialize, CanExecuteReinitialize);
            RefreshConvoCommand = new AsyncRelayCommand(
                Command_RefreshConvo, CanExecuteRefreshConvo);
            Prompt = string.Empty;
            Initialized = false;

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
            ProgressState.InitializeProgress(2);
            try
            {
                ProgressState.UpdateProgressMessage($"Initializing Semantic Kernel....");
                await Task.Run(async () =>
                {
                    var qdrantHostUri = GlobalStateViewModel.Instance.Settings.QdrantHostUri;
                    var builder = Kernel.CreateBuilder();

                    //
                    // Set a debug logger
                    //
                    var loggerFactory = LoggerFactory.Create(b =>
                    {
                        b.AddDebug().SetMinimumLevel(LogLevel.Trace);
                    });
                    builder.Services.AddSingleton(loggerFactory);

                    //
                    // Inference runtime service (Ollama, Onnx, LlamaCpp, etc.)
                    // This must be done prior to initializing vector store bc it needs embeddings service
                    //
                    var inferenceService = new InferenceService();
                    inferenceService.Initialize(builder);

                    ProgressState.UpdateProgressValue();

                    //
                    // RAG is optional and uses qdrant vector db.
                    //
                    if (!string.IsNullOrEmpty(qdrantHostUri))
                    {
                        var vecDb = new EtwVectorDbService(qdrantHostUri);
                        await vecDb.InitializeAsync(builder);
                    }

                    ProgressState.UpdateProgressValue();

                    //
                    // Chat histories must be accessible to inference runtimes and agents.
                    //
                    var chatHistory = new ChatHistory();
                    builder.Services.AddSingleton(chatHistory);
                    builder.Services.AddSingleton(m_ResultHistory);

                    //
                    // Build the kernel
                    //
                    m_Kernel = builder.Build();
                    m_Kernel.Plugins.AddFromObject(new ProcessInfo(), "processInfo");
                    m_Kernel.Plugins.AddFromObject(new EtwAnalysis(), "etwAnalysis");
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

        public override async Task Command_SettingsChanged()
        {
            var changed = GlobalStateViewModel.Instance.Settings.ChangedProperties;

            if (changed.Contains(nameof(OnnxGenAIConfigModel.PromptExecutionSettings)) ||
                changed.Contains(nameof(OnnxGenAIConfigModel.BertModelPath)) ||
                changed.Contains(nameof(OnnxGenAIConfigModel.ModelPath)) ||
                changed.Contains(nameof(OllamaConfigModel.PromptExecutionSettings)) ||
                changed.Contains(nameof(OllamaConfigModel.EndpointUri)) ||
                changed.Contains(nameof(OllamaConfigModel.TextEmbeddingModelName)) ||
                changed.Contains(nameof(OllamaConfigModel.ModelName)) ||
                changed.Contains(nameof(SettingsFormViewModel.QdrantHostUri)))
            {
                await ReinitializeCommand.ExecuteAsync(null);
                CanExecuteChanged();
            }
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

            if (!ImportEtwEventsVectorDbDataCommand.CanExecute(null))
            {
                ProgressState.FinalizeProgress("Unable to import ETW event data at this time.");
                return;
            }

            await ImportEtwEventsVectorDbDataCommand.ExecuteAsync(DataSource.Live);
        }

        private async Task Command_ImportManifestVecDbData(DataSource Source, CancellationToken Token)
        {
            var error = "";
            try
            {
                ProgressState.InitializeProgress(2);
                ProgressState.m_CurrentCommand = ImportManifestsVectorDbDataCommand;
                ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Visible;
                await ImportInternal<ParsedEtwManifest, EtwProviderManifestRecord>(Source, Token);
            }
            catch (OperationCanceledException)
            {
                error = "Operation cancelled";
            }
            catch (Exception ex)
            {
                error = $"Import command failed: {ex.Message}";
            }
            finally
            {
                if (!string.IsNullOrEmpty(error))
                {
                    ProgressState.FinalizeProgress(error);
                }
                ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Hidden;
                ProgressState.m_CurrentCommand = null;
            }
        }

        private async Task Command_ImportEtwEventVecDbData(DataSource Source, CancellationToken Token)
        {
            var error = "";
            try
            {
                ProgressState.InitializeProgress(2);
                ProgressState.m_CurrentCommand = ImportEtwEventsVectorDbDataCommand;
                ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Visible;
                await ImportInternal<ParsedEtwEvent, EtwEventRecord>(Source, Token);
            }
            catch (OperationCanceledException)
            {
                error = "Operation cancelled";
            }
            catch (Exception ex)
            {
                error = $"Import command failed: {ex.Message}";
            }
            finally
            {
                if (!string.IsNullOrEmpty(error))
                {
                    ProgressState.FinalizeProgress(error);
                }
                ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Hidden;
                ProgressState.m_CurrentCommand = null;
            }
        }

        private async Task ImportInternal<T, U>( // eg, T = ParsedEtwEvent, U = EtwEventRecord
            DataSource Source,
            CancellationToken Token
            ) where T : class where U : class, IEtwRecord<T>
        {
            var vecDb = m_Kernel.GetRequiredService<EtwVectorDbService>();
            var isSnapshotRestore = false;
            ProgressState.UpdateProgressMessage("Waiting on data...");
            var data = new List<T>();

            if (Source == DataSource.Live)
            {
                data = typeof(T) switch
                {
                    Type t when t == typeof(EtwEventRecord) => await Task.Run(() =>
                    {
                        //
                        // Pull the manifests of all active providers on the system
                        //
                        var manifests = ProviderParser.GetManifests();
                        if (manifests == null || manifests.Count == 0)
                        {
                            throw new Exception("No manifests available in current system.");
                        }
                        return manifests.Values.ToList() as List<T>;
                    }),
                    Type t when t == typeof(EtwProviderManifestRecord) => await Task.Run(() =>
                    {
                        //
                        // Pull from the most recently active livesession
                        //
                        var vm = GlobalStateViewModel.Instance.g_SessionViewModel.GetMostRecentLiveSession();
                        if (vm == null)
                        {
                            throw new Exception("No live sessions available.");
                        }
                        var events = new List<ParsedEtwEvent>();
                        foreach (var kvp in vm.m_ProviderTraceData)
                        {
                            var eventsThisProvider = kvp.Value.Data.AsObservable.ToList();
                            events.AddRange(eventsThisProvider);
                        }
                        return events as List<T>;
                    }),
                    _ => throw new InvalidOperationException($"No collection name defined for type {typeof(T).Name}")
                };
            }
            else if (Source == DataSource.XMLFile)
            {
                data = DataImporter.Import<List<T>>(ImportFormat.Xml);
            }
            else if (Source == DataSource.JSONFile)
            {
                data = DataImporter.Import<List<T>>(ImportFormat.Json);
            }
            else if (Source == DataSource.CollectionSnapshot)
            {
                isSnapshotRestore = true;
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;
                dialog.Multiselect = false;
                dialog.Title = $"Select a collection snapshot";
                var result = dialog.ShowDialog();
                if (!result.HasValue || !result.Value)
                {
                    throw new Exception("No file selected");
                }
                var collectionFilename = dialog.FileName;
                ProgressState.UpdateProgressValue();
                ProgressState.UpdateProgressMessage($"Restoring collection...");
                await vecDb!.RestoreCollectionAsync<T>(collectionFilename, Token);
                ProgressState.FinalizeProgress($"Collection restored successfully.");
            }
            else
            {
                throw new Exception($"Unsupported data source {Source}");
            }

            if (data == null || data.Count == 0)
            {
                throw new Exception("No data provided");
            }

            if (!isSnapshotRestore)
            {
                ProgressState.UpdateProgressValue();
                ProgressState.UpdateProgressMessage($"Importing data...");
                await vecDb.ImportDataAsync<T, U>(data, Token);
                ProgressState.FinalizeProgress($"Imported {data.Count} records.");
            }
            var recordCount = await vecDb.GetRecordCountAsync<T>(Token);
            ProgressState.EphemeralStatusText = $"{recordCount} records available.";
            CanExecuteChanged();
        }

        private async Task Command_ExportEtwEventsVectorDb(CancellationToken Token)
        {
            ProgressState.m_CurrentCommand = ExportEtwEventsVectorDbCommand;
            ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Visible;
            var vecDb = m_Kernel.GetRequiredService<EtwVectorDbService>();
            if (vecDb == null || !vecDb.m_Initialized)
            {
                ProgressState.UpdateEphemeralStatustext("Vector DB service not initialized.");
                return;
            }
            await ExportVecDbInternal<EtwEventRecord>(Token);
        }

        private async Task Command_ExportManifestsVectorDb(CancellationToken Token)
        {
            ProgressState.m_CurrentCommand = ExportManifestsVectorDbCommand;
            ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Visible;
            var vecDb = m_Kernel.GetRequiredService<EtwVectorDbService>();
            if (vecDb == null || !vecDb.m_Initialized)
            {
                ProgressState.UpdateEphemeralStatustext("Vector DB service not initialized.");
                return;
            }
            await ExportVecDbInternal<EtwProviderManifestRecord>(Token);
        }

        private async Task ExportVecDbInternal<T>(CancellationToken Token) where T : class
        {
            var browser = new Microsoft.Win32.OpenFolderDialog();
            browser.Title = "Select a location to save the collection";
            browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            var target = browser.FolderName;
            var error = string.Empty;
            try
            {
                ProgressState.InitializeProgress(2);
                ProgressState.UpdateProgressValue();
                ProgressState.UpdateProgressMessage("Exporting vector DB collection...");
                var vecDb = m_Kernel.GetRequiredService<EtwVectorDbService>();
                await vecDb!.SaveCollectionAsync<T>(target, Token);
                ProgressState.UpdateProgressValue();
                ProgressState.FinalizeProgress($"Collection saved to {target}");
            }
            catch (OperationCanceledException)
            {
                error = "Operation cancelled";
            }
            catch (Exception ex)
            {
                error = $"Export command failed: {ex.Message}";
            }
            finally
            {
                if (!string.IsNullOrEmpty(error))
                {
                    ProgressState.FinalizeProgress(error);
                }
                ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Hidden;
                ProgressState.m_CurrentCommand = null;
            }
        }

        private async Task Command_Reinitialize()
        {
            Reset();
            await Initialize();
        }

        private async Task Command_RefreshConvo()
        {
            try
            {
                var chatHistory = m_Kernel.GetRequiredService<ChatHistory>();
                var resultHistory = m_Kernel.GetRequiredService<ConcurrentObservableCollection<InsightsInferenceResultModel>>();
                chatHistory.Clear();
                resultHistory.Clear();
            }
            catch (Exception)
            {
                ProgressState.UpdateEphemeralStatustext("Chat service not ready.");
            }
            CanExecuteChanged();
        }

        private void Reset()
        {
            if (m_Kernel == null)
            {
                return;
            }

            try
            {
                var chatHistory = m_Kernel.GetRequiredService<ChatHistory>();
                var resultHistory = m_Kernel.GetRequiredService<ConcurrentObservableCollection<InsightsInferenceResultModel>>();
                chatHistory.Clear();
                resultHistory.Clear();
            }
            catch (Exception)
            {
                ProgressState.UpdateEphemeralStatustext("Chat service not ready.");
            }

            m_ModelBusy = false;
            m_Kernel = null;
            Prompt = string.Empty;
            Initialized = false;
            CanExecuteChanged();
        }

        private void Command_Clear()
        {
            try
            {
                var chatHistory = m_Kernel.GetRequiredService<ChatHistory>();
                chatHistory.Clear();
            }
            catch (Exception)
            {
                ProgressState.UpdateEphemeralStatustext("Chat service not ready.");
            }
        }
        private void Command_Copy()
        {
            try
            {
                var chatHistory = m_Kernel.GetRequiredService<ChatHistory>();
                if (chatHistory.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    chatHistory.ToList().ForEach(r => sb.AppendLine(r.Content));
                    System.Windows.Clipboard.SetText(sb.ToString());
                }
            }
            catch (Exception)
            {
                ProgressState.UpdateEphemeralStatustext("Chat service not ready.");
            }
        }

        private async Task Command_Generate(CancellationToken Token)
        {
            m_ModelBusy = true;
            ProgressState.InitializeProgress(1); // runtime will adjust manually.
            ProgressState.m_CurrentCommand = GenerateCommand;
            ProgressState.CancelCommandButtonVisibility = System.Windows.Visibility.Visible;

            try
            {
                var service = m_Kernel.GetRequiredService<InferenceService>();
                _ = await service.RunInferenceAsync(AuthorRole.User,
                    new ChatMessageContentItemCollection() {
                        new Microsoft.SemanticKernel.TextContent(Prompt)
                }, Token);
            }
            catch (OperationCanceledException)
            {
                AddSystemMessageToResult($"Operation Canceled");
            }
            catch (Exception ex)
            {
                AddSystemMessageToResult($"Inference exception: {ex.Message}");
            }
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
            // Show form errors
            //
            var fields = new string[] { nameof(Initialized), nameof(Prompt) };
            foreach (var field in fields)
            {
                if (PropertyHasErrors(field))
                {
                    var errors = GetErrors(field).Cast<string>().ToList();
                    errors.ForEach(e => AddErrorMessageToResult(e));
                }
            }
        }

        private void AddSystemMessageToResult(string Message)
        {
            if (m_Kernel == null)
            {
                return;
            }
            var message = Message.Substring(0, Math.Min(Message.Length, 5000));
            var resultHistory = m_Kernel.GetRequiredService<ConcurrentObservableCollection<InsightsInferenceResultModel>>();
            resultHistory.Add(new InsightsInferenceResultModel()
            {
                Type = InsightsInferenceResultModel.ContentType.SystemMessage,
                Content = message
            });
        }

        private void AddErrorMessageToResult(string Message)
        {
            if (m_Kernel == null)
            {
                return;
            }
            var message = Message.Substring(0, Math.Min(Message.Length, 5000));
            var resultHistory = m_Kernel.GetRequiredService<ConcurrentObservableCollection<InsightsInferenceResultModel>>();
            resultHistory.Add(new InsightsInferenceResultModel()
            {
                Type = InsightsInferenceResultModel.ContentType.ErrorMessage,
                Content = message
            });
        }

        private void EraseResultHistory(InsightsInferenceResultModel.ContentType ContentType = InsightsInferenceResultModel.ContentType.None)
        {
            if (m_Kernel == null)
            {
                return;
            }
            var resultHistory = m_Kernel.GetRequiredService<ConcurrentObservableCollection<InsightsInferenceResultModel>>();
            if (ContentType != InsightsInferenceResultModel.ContentType.None)
            {
                resultHistory.Where(item => item.Type == ContentType).ToList().
                    ForEach(item => resultHistory.Remove(item));
            }
            else
            {
                resultHistory.ToList().ForEach(item => resultHistory.Remove(item));
            }
        }

        private bool CanExecuteGenerate()
        {
            if (GlobalStateViewModel.Instance.Settings.HasErrors)
            {
                return false;
            }
            if (m_ModelBusy || !Initialized || m_Kernel == null)
            {
                //
                // The model is busy, not initialized, or not available.
                //
                return false;
            }
            return !PropertyHasErrors(nameof(Prompt));
        }

        public bool CanExecuteVecDbCommand()
        {
            if (!Initialized || m_Kernel == null)
            {
                return false;
            }
            var vecDb = m_Kernel.GetRequiredService<EtwVectorDbService>();
            if (vecDb == null || !vecDb.m_Initialized)
            {
                //
                // The vector db service is not initialized.
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

        private bool CanExecuteRefreshConvo()
        {
            if (GlobalStateViewModel.Instance.Settings.HasErrors)
            {
                return false;
            }
            if (m_Kernel == null)
            {
                return false;
            }
            var chatHistory = m_Kernel.GetRequiredService<ChatHistory>();
            return !m_ModelBusy && chatHistory.Count > 0;
        }

        private void CanExecuteChanged()
        {
            GenerateCommand.NotifyCanExecuteChanged();
            ImportEtwEventsVectorDbDataCommand.NotifyCanExecuteChanged();
            ImportManifestsVectorDbDataCommand.NotifyCanExecuteChanged();
            ImportVectorDbDataFromLiveSessionCommand.NotifyCanExecuteChanged();
            ExportEtwEventsVectorDbCommand.NotifyCanExecuteChanged();
            ExportManifestsVectorDbCommand.NotifyCanExecuteChanged();
            RefreshConvoCommand.NotifyCanExecuteChanged();
        }
    }
}