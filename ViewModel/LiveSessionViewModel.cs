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
using System.Collections.ObjectModel;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;
    using Timer = System.Timers.Timer;

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

        //
        // This is required in order to have something to bind to LiveSessionGrid.xaml,
        // which is the UserControl that displays the provider's data in a DataGrid.
        // It is updated whenever the selected provider trace data tab changes, from
        // LiveSessionView code-behind.
        //
        private ProviderTraceData _SelectedProviderTraceData;
        public ProviderTraceData SelectedProviderTraceData
        {
            get => _SelectedProviderTraceData;
            set
            {
                if (_SelectedProviderTraceData != value)
                {
                    _SelectedProviderTraceData = value;
                    OnPropertyChanged("SelectedProviderTraceData");
                }
            }
        }

        private int _SelectedProviderIndex;
        public int SelectedProviderIndex
        {
            get => _SelectedProviderIndex;
            set
            {
                if (_SelectedProviderIndex != value)
                {
                    _SelectedProviderIndex = value;
                    OnPropertyChanged("SelectedProviderIndex");
                }
            }
        }

        private long _EventsConsumed; // could be per-provider, but confusing in UI
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

        private long _BytesConsumed; // bytes consumed are global, not per-provider
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
        #endregion

        #region commands

        public AsyncRelayCommand StartCommand { get; set; }
        public AsyncRelayCommand StopCommand { get; set; }
        public AsyncRelayCommand SendToInsightsCommand { get; set; }

        #endregion

        public DateTime StartTime { get; set; }
        public string m_TabText { get; set; }
        //
        // This collection is bound to the LiveSessionViewTabControl
        //
        public ObservableCollection<TabItem> Tabs { get; set; }

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
        private ProgressContext _Context;

        //
        // Per-provider trace data, accessed from arbitrary thread ctx.
        //
        public ConcurrentDictionary<string, ProviderTraceData> m_ProviderTraceData { get; set; }

        //
        // Each live session gets a copy of the most recent library validated in current
        // settings. Otherwise, a global copy would overwrite the libraries of active
        // live sessions.
        //
        public FormatterLibrary m_FormatterLibrary { get; set; }

        public LiveSessionViewModel(
            SessionFormModel Model,
            Action StartedCallback,
            Action StoppedCallback
            ) : base()
        {
            m_FormatterLibrary = new FormatterLibrary();

            StartCommand = new AsyncRelayCommand(Command_Start, CanExecuteStartCommand);
            StopCommand = new AsyncRelayCommand(Command_Stop, CanExecuteStopCommand);
            SendToInsightsCommand = new AsyncRelayCommand(Command_SendToInsights, CanExecuteSendToInsights);
            CancellationSource = new CancellationTokenSource();
            TaskCompletedEvent = new AutoResetEvent(false);
            Stopwatch = new Stopwatch();
            Configuration = Model;
            m_TabText = Configuration.Name;
            m_ProviderTraceData = new ConcurrentDictionary<string, ProviderTraceData>();
            TraceStartedCallback = StartedCallback;
            TraceStoppedCallback = StoppedCallback;
            BytesConsumed = 0;
            EventsConsumed = 0;
            Tabs = new ObservableCollection<TabItem>();

            //
            // Setup progress tracking.
            //
            if (Configuration.StopCondition == StopCondition.None)
            {
                _Context = ProgressState.CreateProgressContext(100, $"Live session running...");
                ProgressState.ProgressValue = 50;
            }
            else
            {
                _Context = ProgressState.CreateProgressContext(
                    Configuration.StopConditionValue,
                    $"Live session running until {Configuration.StopCondition} {Configuration.StopConditionValue}");
            }

            //
            // 1-second timer to keep taskbar refreshed with trace details
            //
            _ElapsedSecTimer = new Timer();
            _ElapsedSecTimer.Interval = 1000;
            _ElapsedSecTimer.AutoReset = true;
            _ElapsedSecTimer.Elapsed += (sender, e) =>
            {
                var ts = TimeSpan.FromMilliseconds(Stopwatch.ElapsedMilliseconds);
                var bytes = MiscHelper.FormatByteSizeString(BytesConsumed, 0);
                var message = $"{EventsConsumed:n0} events | {bytes} | {ts:mm}:{ts:ss}";
                if (IsStopping())
                {
                    message += $" | Session stopping...";
                }
                else if (!IsRunning())
                {
                    message += $" | Session stopped.";
                    _ElapsedSecTimer.Stop();
                    ProgressState.FinalizeProgress(message);
                    return;
                }
                ProgressState.UpdateProgressMessage(message);
            };

            //
            // Create tabs for each provider. This must be done before the view has been
            // instantiated, which should occur right after this ctor. These tabs are
            // bound to the tab control's ItemsSource.
            //
            // The tabs will not have a close button, because it doesn't make sense to close
            // provider data tabs that are irreversibly part of the live session, but also,
            // the viewmodel for the provider data tabs is ProviderTraceData, which is NOT
            // derived from ViewModelBase (the style has a command that expects that)
            //
            foreach (var provider in Configuration.ConfiguredProviders)
            {
                var name = provider._EnabledProvider.Name;
                if (string.IsNullOrEmpty(name))
                {
                    Debug.Assert(false);
                    continue;
                }
                var result = m_ProviderTraceData.TryAdd(name, new ProviderTraceData()
                {
                    Columns = provider.Columns,
                    m_TabText = name
                });
                Debug.Assert(result);
                var id = provider._EnabledProvider.Id;
                var tabName = UiHelper.GetUniqueTabName(id, "LiveSessionProviderData");
                var newTab = UiHelper.CreateEmptyTab(tabName, name, m_ProviderTraceData[name]);
                if (newTab == null)
                {
                    Debug.Assert(false);
                    return;
                }
                Tabs.Add(newTab); // bound to LiveSessionView tabcontrol
            }
        }

        public async Task<bool> Initialize()
        {
            //
            // This routine is called from SessionViewModel after instantiating a LiveSession
            // but before starting it.
            //

            //
            // If the formatters in the settings object have errors, nothing to do.
            //
            if (!GlobalStateViewModel.Instance.Settings.m_FormatterLibrary.m_Ready)
            {
                return false;
            }

            //
            // Re-publish/copy the already-validated formatters from settings.
            //
            return await m_FormatterLibrary.Publish(
                GlobalStateViewModel.Instance.Settings.Formatters.ToList());
        }

        public override Task ViewModelActivated()
        {
            GlobalStateViewModel.Instance.CurrentViewModel = this;
            GlobalStateViewModel.Instance.g_SessionViewModel.CurrentLiveSession = this;
            return Task.CompletedTask;
        }

        private async Task Command_Start()
        {
            Debug.Assert(!StartTaskRunning);
            Debug.Assert(!StopTaskRunning);
            Debug.Assert(m_FormatterLibrary.m_Ready);
            StartTaskRunning = true;
            NotifyCanExecuteChanged();

            try
            {
                StartTime = DateTime.Now;
                TraceStartedCallback();
                _ElapsedSecTimer.Start();
                await Task.Run(() => ConsumeTraceEvents());
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"Exception occurred: {ex.Message}");
            }
            StartTaskRunning = false;
            NotifyCanExecuteChanged();
        }

        private async Task Command_Stop()
        {
            Debug.Assert(!StopTaskRunning);
            Debug.Assert(_TraceSession != null);
            StopTaskRunning = true;
            NotifyCanExecuteChanged();

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
            NotifyCanExecuteChanged();
        }

        private async Task Command_SendToInsights()
        {
            var insightsVm = GlobalStateViewModel.Instance.g_InsightsViewModel;
            Debug.Assert(SelectedProviderIndex < m_ProviderTraceData.Count);
            await insightsVm.ImportVectorDbDataFromLiveSessionCommand.ExecuteAsync(
                m_ProviderTraceData.ElementAt(SelectedProviderIndex).Value.Data.AsObservable.ToList());
        }

        protected override async Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
        {
            Debug.Assert(SelectedProviderTraceData != null);
            var list = SelectedProviderTraceData.Data.AsObservable.ToList();
            if (list.Count == 0)
            {
                ProgressState.UpdateEphemeralStatustext("No live session data available for export.");
                return;
            }
            try
            {
                var result = await DataExporter.Export<List<ParsedEtwEvent>>(
                    list, Format, SelectedProviderTraceData.m_TabText,Token);
                if (result.Item1 == 0 || result.Item2 == null)
                {
                    throw new Exception("Invalid export format");
                }
                ProgressState.UpdateEphemeralStatustext($"Exported {result.Item1} records to {result.Item2}");
                if (Format != DataExporter.ExportFormat.Clip)
                {
                    ProgressState.SetFollowupActionCommand.Execute(
                        new FollowupAction()
                        {
                            Title = "Open",
                            Callback = new Action<dynamic>((args) =>
                            {
                                var psi = new ProcessStartInfo();
                                psi.FileName = result.Item2;
                                psi.UseShellExecute = true;
                                Process.Start(psi);
                            }),
                            CallbackArgument = null
                        });
                }
            }
            catch (Exception ex)
            {
                ProgressState.UpdateEphemeralStatustext($"ExportData failed: {ex.Message}");
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

                        if (string.IsNullOrEmpty(parsedEvent.Provider.Name) ||
                            !m_ProviderTraceData.ContainsKey(parsedEvent.Provider.Name))
                        {
                            Trace(TraceLoggerType.LiveSession,
                                  TraceEventType.Error,
                                  $"Dropping event for unrecognized/no-name provider {parsedEvent.Provider}");
                            return;
                        }
                        m_ProviderTraceData[parsedEvent.Provider.Name].Data.Add(parsedEvent);
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
            TaskCompletedEvent.Set();
        }

        protected override bool CanExecuteExportDataCommand(DataExporter.ExportFormat Format)
        {
            if (IsRunning()) // Don't allow any data export until session is stopped.
            {
                return false;
            }
            return base.CanExecuteExportDataCommand(Format);
        }

        private bool CanExecuteStartCommand()
        {
            return !IsRunning() && m_FormatterLibrary.m_Ready;
        }

        private bool CanExecuteStopCommand()
        {
            return IsRunning();
        }

        private bool CanExecuteSendToInsights()
        {
            if (m_ProviderTraceData.Count == 0)
            {
                return false;
            }
            Debug.Assert(SelectedProviderIndex < m_ProviderTraceData.Count);
            return GlobalStateViewModel.Instance.Settings.Valid &&
                m_ProviderTraceData.ElementAt(SelectedProviderIndex).Value.Data.Count > 0;
        }

        private void NotifyCanExecuteChanged()
        {
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
            ExportDataCommand.NotifyCanExecuteChanged();
            SendToInsightsCommand.NotifyCanExecuteChanged();
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
        public string m_TabText { get; set; }
        public Guid m_Id { get; set; } // used for converter library lookup

        public ProviderTraceData()
        {
            _Data = new ConcurrentObservableCollection<ParsedEtwEvent>();
            IsEnabled = true;
            Columns = new List<EtwColumnViewModel>();
            m_Id = Guid.NewGuid();
        }
    }
}
