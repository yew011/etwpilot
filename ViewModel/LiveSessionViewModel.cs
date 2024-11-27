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
using etwlib;
using EtwPilot.Model;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using static etwlib.NativeTraceConsumer;
using static etwlib.NativeTraceControl;
using Meziantou.Framework.WPF.Collections;
using System.Collections.Concurrent;
using EtwPilot.Utilities;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;

    public class LiveSessionViewModel : ViewModelBase
    {
        public enum StopCondition
        {
            None,
            SizeMb,
            TimeSec,
            Max
        }

        #region observable properties

        private long _EventsConsumed;
        public long EventsConsumed
        {
            get { return Interlocked.Read(ref _EventsConsumed); }
            set
            {
                if (_EventsConsumed != value)
                {
                    Interlocked.Exchange(ref _EventsConsumed, value);
                    OnPropertyChanged("EventsConsumed");
                }
            }
        }

        private long _BytesConsumed;
        public long BytesConsumed
        {
            get { return Interlocked.Read(ref _BytesConsumed); }
            set
            {
                if (_BytesConsumed != value)
                {
                    Interlocked.Exchange(ref _BytesConsumed, value);
                    OnPropertyChanged("BytesConsumed");
                }
            }
        }

        private ProviderTraceData _CurrentProviderTraceData; // relates to currently selected tab
        public ProviderTraceData CurrentProviderTraceData
        {
            get => _CurrentProviderTraceData;
            set
            {
                if (_CurrentProviderTraceData != value)
                {
                    _CurrentProviderTraceData = value;
                    OnPropertyChanged("CurrentProviderTraceData");
                }
            }
        }
        #endregion

        #region commands

        public AsyncRelayCommand StartLiveSessionCommand { get; set; }
        public AsyncRelayCommand StopLiveSessionCommand { get; set; }
        public AsyncRelayCommand<dynamic> CreateTabsForProvidersCommand { get; set; }
        public AsyncRelayCommand SendToInsightsCommand { get; set; }

        #endregion

        public DateTime StartTime { get; set; }

        public SessionFormModel Configuration { get; set; }
        public Stopwatch Stopwatch { get; set; }
        private CancellationTokenSource CancellationSource;
        private AutoResetEvent TaskCompletedEvent;
        private Action TraceStartedCallback;
        private Action TraceStoppedCallback;
        private bool StartTaskRunning;
        private bool StopTaskRunning;
        private TraceSession _TraceSession;
        private Timer _ElapsedSecTimer;

        //
        // Per-provider trace data, accessed from arbitrary thread ctx.
        //
        private ConcurrentDictionary<Guid, ProviderTraceData> _ProviderTraceData;

        //
        // This is set in LiveSessionView.xaml.cs code-behind. Go there for the lamentation.
        //
        public dynamic TabControl { get; set; }

        public LiveSessionViewModel(
            SessionFormModel Model,
            Action StartedCallback,
            Action StoppedCallback
            ) : base()
        {
            StartLiveSessionCommand = new AsyncRelayCommand(
                Command_StartLiveSession, () => { return !IsRunning(); });
            StopLiveSessionCommand = new AsyncRelayCommand(
                Command_StopLiveSession, () => { return IsRunning(); });
            CreateTabsForProvidersCommand = new AsyncRelayCommand<dynamic>(
                Command_CreateTabsForProviders, (_) => { return true; });
            SendToInsightsCommand = new AsyncRelayCommand(
                Command_SendToInsights, CanExecuteSendToInsights);

            CancellationSource = new CancellationTokenSource();
            TaskCompletedEvent = new AutoResetEvent(false);
            Stopwatch = new Stopwatch();
            Configuration = Model;
            _ProviderTraceData = new ConcurrentDictionary<Guid, ProviderTraceData>();
            TraceStartedCallback = StartedCallback;
            TraceStoppedCallback = StoppedCallback;
            //
            // For the elapsed seconds visual aid to update once a second. This is
            // required because Stopwatch.ElapsedMilliseconds is a property that
            // only updates when it is accessed (queried), so one-way binding to
            // the control does not do that.
            //
            _ElapsedSecTimer = new Timer(new TimerCallback((s) =>
                OnPropertyChanged("Stopwatch")), null, 1000, 1000);
        }

        public override Task ViewModelActivated()
        {
            GlobalStateViewModel.Instance.CurrentViewModel = this;
            return Task.CompletedTask;
        }

        private async Task Command_StartLiveSession()
        {
            Debug.Assert(!StartTaskRunning);
            Debug.Assert(!StopTaskRunning);
            StartTaskRunning = true;
            try
            {
                GlobalStateViewModel.Instance.g_ConverterLibrary.Build(
                    Configuration.ConfiguredProviders);
                TraceStartedCallback();
                StartTime = DateTime.Now;
                await Task.Run(() => ConsumeTraceEvents());
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"Exception occurred: {ex.Message}");
            }
            StartTaskRunning = false;
        }

        private async Task Command_StopLiveSession()
        {
            Debug.Assert(!StopTaskRunning);
            Debug.Assert(_TraceSession != null);
            StopTaskRunning = true;

            try
            {
                TraceStoppedCallback();
                await Task.Run(() =>
                {
                    //
                    // Cancel our work.
                    //
                    CancellationSource.Cancel();
                    //
                    // Stop the trace controller.
                    //
                    _TraceSession.Stop();

                    TaskCompletedEvent.WaitOne();
                });
            }
            catch (Exception ex)
            {
                ProgressState.UpdateProgressMessage($"Exception occurred: {ex.Message}");
            }
            StopTaskRunning = false;
        }

        private async Task Command_CreateTabsForProviders(dynamic AutogeneratingColumnCallback)
        {
            //
            // This routine is invoked only by TabControl_Loaded in LiveSessionView.xaml.cs code-behind
            //
            foreach (var provider in Configuration.ConfiguredProviders)
            {
                var id = provider._EnabledProvider.Id;
                if (!_ProviderTraceData.ContainsKey(id))
                {
                    var result = _ProviderTraceData.TryAdd(id, new ProviderTraceData()
                    {
                        Columns = provider.Columns,
                    });
                    Debug.Assert(result);
                }
                var data = _ProviderTraceData[id];

                var tabName = UiHelper.GetUniqueTabName(id, "ProviderLiveSession");
                Func<string, Task<bool>> tabClosedCallback = delegate (string TabName)
                {
                    data.IsEnabled = false;
                    return Task.FromResult(true);
                };
                var providerDataGrid = new DataGrid()
                {
                    //
                    // Important: Fixing the control to a MaxHeight and MaxWidth
                    // is required, as otherwise the Grid will attempt to re-calculate
                    // control bounds on every single row/ column.Etw captures are
                    // noisy and will hang the UI.
                    //
                    Name = UiHelper.GetUniqueTabName(id, "ProviderData"),
                    AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue,
                    EnableColumnVirtualization = true,
                    EnableRowVirtualization = true,
                    IsReadOnly = true,
                    AutoGenerateColumns = true,
                    RowHeight = 25,
                    MaxHeight = 1600,
                    MaxWidth = 1600,
                    ItemsSource = data.Data.AsObservable,
                    DataContext = data,
                };
                providerDataGrid.AutoGeneratingColumn += AutogeneratingColumnCallback;
                if (!UiHelper.CreateTabControlContextualTab(
                        TabControl,
                        providerDataGrid,
                        tabName,
                        provider._EnabledProvider.Name!,
                        "LiveSessionTabStyle",
                        "LiveSessionTabText",
                        "LiveSessionTabCloseButton",
                        data,
                        tabClosedCallback))
                {
                    Trace(TraceLoggerType.LiveSession,
                          TraceEventType.Error,
                          $"Unable to create live session tab {tabName}");
                }
            }
        }

        private async Task Command_SendToInsights()
        {
            var insightsVm = GlobalStateViewModel.Instance.g_InsightsViewModel;

            //
            // The insights view model might not have been initialized yet, since this
            // only occurs when its tab is activated.
            //
            if (!insightsVm.Initialized)
            {
                if (!insightsVm.ReinitializeCommand.CanExecute(null))
                {
                    ProgressState.FinalizeProgress("Unable to initialize Insights at this time");
                    return;
                }
                await insightsVm.ReinitializeCommand.ExecuteAsync(null);
            }

            //
            // Execute sequence of commands that normally are invoked from UI interaction.
            //
            if (!insightsVm.SetChatTopicCommand.CanExecute(null))
            {
                await insightsVm.ViewModelActivated();
                ProgressState.FinalizeProgress("Unable to set chat topic at this time.");
                return;
            }
            await insightsVm.SetChatTopicCommand.ExecuteAsync(InsightsViewModel.ChatTopic.EventData);
            if (!insightsVm.ImportVectorDbDataFromLiveSessionCommand.CanExecute(null))
            {
                await insightsVm.ViewModelActivated();
                ProgressState.FinalizeProgress("Unable to import data at this time.");
                return;
            }

            await insightsVm.ImportVectorDbDataFromLiveSessionCommand.ExecuteAsync(
                CurrentProviderTraceData.Data.AsObservable.ToList());
            await insightsVm.ViewModelActivated();
        }

        protected override async Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
        {
            ProgressState.InitializeProgress(1);
            var list = CurrentProviderTraceData.Data.AsObservable.ToList();
            if (list.Count == 0)
            {
                ProgressState.FinalizeProgress("No live session data available for export.");
                return;
            }
            try
            {
                var result = await DataExporter.Export<List<ParsedEtwEvent>>(
                    list, Format, "LiveSession",Token);
                ProgressState.UpdateProgressValue();
                ProgressState.FinalizeProgress($"Exported {result.Item1} records to {result.Item2}");
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"ExportData failed: {ex.Message}");
            }
        }

        public bool IsRunning()
        {
            //
            // NB: StartTask.Status is NOT reliable!
            //
            return StartTaskRunning;
        }

        public bool IsStopping()
        {
            //
            // NB: StopTask.Status is NOT reliable!
            //
            return StopTaskRunning;
        }

        private void ConsumeTraceEvents()  // blocking
        {
            //
            // Setup progress bar.
            //
            if (Configuration.StopCondition == StopCondition.None)
            {
                ProgressState.InitializeProgress(100); // arbitrary
                ProgressState.ProgressValue = 50;
            }
            else
            {
                ProgressState.InitializeProgress(Configuration.StopConditionValue);
            }

            //
            // Instantiate trace object
            //
            if (!Configuration.IsRealTime)
            {
                var sb = new StringBuilder(Configuration.Name);
                foreach (char item in Path.GetInvalidFileNameChars())
                {
                    sb = sb.Replace(item.ToString(), "");
                }
                var filename = $"{sb}-{DateTime.Now}.etl";
                var target = Path.Combine(Configuration.LogLocation, filename);
                _TraceSession = new FileTrace(target);
            }
            else
            {
                _TraceSession = new RealTimeTrace(Configuration.Name);
            }

            etwlib.TraceLogger.SetLevel(GlobalStateViewModel.Instance.Settings.TraceLevelEtwlib);

            //
            // Start trace
            //
            using (var parserBuffers = new EventParserBuffers())
            {
                try
                {
                    foreach (var provider in Configuration.ConfiguredProviders)
                    {
                        _TraceSession.AddProvider(provider._EnabledProvider);
                    }

                    _TraceSession.Start();

                    ProgressState.UpdateProgressMessage(
                        $"Live session {Configuration.Name} has started.");
                    Stopwatch.Start();

                    //
                    // Begin consuming events. This is a blocking call.
                    //
                    _TraceSession.Consume(new EventRecordCallback((Event) =>
                    {
                        var evt = (EVENT_RECORD)Marshal.PtrToStructure(
                                Event, typeof(EVENT_RECORD))!;

                        var parser = new EventParser(
                            evt,
                            parserBuffers,
                            _TraceSession.GetPerfFreq());
                        ParsedEtwEvent? parsedEvent = null;

                        //
                        // Parse the event
                        //
                        try
                        {
                            parsedEvent = parser.Parse();
                        }
                        catch (Exception ex)
                        {
                            Trace(TraceLoggerType.LiveSession,
                                  TraceEventType.Error,
                                  $"Unable to parse event: {ex.Message}");
                            return;
                        }

                        if (parsedEvent == null)
                        {
                            //
                            // There are many failure cases that are expected, like
                            // unsupported MOF events. Ignore them.
                            //
                            return;
                        }

                        if (!_ProviderTraceData.ContainsKey(parsedEvent.Provider.Id))
                        {
                            Trace(TraceLoggerType.LiveSession,
                                  TraceEventType.Error,
                                  $"Dropping event for unrecognized provider {parsedEvent.Provider}");
                            return;
                        }
                        _ProviderTraceData[parsedEvent.Provider.Id].Data.Add(parsedEvent);
                        EventsConsumed++;
                    }),
                    new BufferCallback((LogFile) =>
                    {
                        if (CancellationSource.IsCancellationRequested)
                        {
                            return 0;
                        }

                        var logfile = new EVENT_TRACE_LOGFILE();
                        try
                        {
                            logfile = (EVENT_TRACE_LOGFILE)
                                Marshal.PtrToStructure(LogFile, typeof(EVENT_TRACE_LOGFILE))!;
                        }
                        catch (Exception ex)
                        {
                            Trace(TraceLoggerType.LiveSession,
                                  TraceEventType.Error,
                                  $"Unable to cast EVENT_TRACE_LOGFILE: {ex.Message}");
                        }
                        BytesConsumed += logfile.Filled;
                        var mbConsumed = Math.Round((double)BytesConsumed / 1000000, 2);
                        var elapsedSec = (int)Math.Floor((decimal)Stopwatch.ElapsedMilliseconds / 1000);

                        if (Configuration.StopCondition == StopCondition.SizeMb &&
                            mbConsumed > Configuration.StopConditionValue)
                        {
                            return 0;
                        }
                        else if (Configuration.StopCondition == StopCondition.TimeSec &&
                            elapsedSec > Configuration.StopConditionValue)
                        {
                            return 0;
                        }
                        return 1;
                    }));
                }
                catch (Exception ex)
                {
                    Trace(TraceLoggerType.LiveSession,
                          TraceEventType.Error,
                          $"An exception occurred when consuming events: {ex.Message}");
                    throw;
                }
            }

            Stopwatch.Stop();
            ProgressState.FinalizeProgress($"Live session {Configuration.Name} stopped.");
            TaskCompletedEvent.Set();
        }

        private bool CanExecuteSendToInsights()
        {
            return GlobalStateViewModel.Instance.Settings.ModelConfig != null && 
                CurrentProviderTraceData != null &&
                CurrentProviderTraceData.Data.Count > 0;
        }
    }

    public class ProviderTraceData : NotifyPropertyAndErrorInfoBase
    {
        private ConcurrentObservableCollection<ParsedEtwEvent> _Data;
        public ConcurrentObservableCollection<ParsedEtwEvent> Data
        {
            get => _Data;
            private set { _Data = value; }
        }
        public bool IsEnabled { get; set; }
        public List<EtwColumnViewModel> Columns { get; set; }

        public ProviderTraceData()
        {
            _Data = new ConcurrentObservableCollection<ParsedEtwEvent>();
            IsEnabled = true;
            Columns = new List<EtwColumnViewModel>();
        }
    }
}
