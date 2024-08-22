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
using System.Windows.Data;
using EtwPilot.Utilities;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;

    internal class LiveSessionViewModel : ViewModelBase
    {
        public enum StopCondition
        {
            None,
            SizeMb,
            TimeSec,
            Max
        }

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
        private ConcurrentDictionary<string, DynamicRuntimeLibrary> ConverterLibraryCache;

        //
        // Per-provider trace data, accessed from arbitrary thread ctx.
        //
        private ConcurrentDictionary<Guid, ProviderTraceData> _ProviderTraceData;

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

        public LiveSessionViewModel(
            SessionFormModel Model,
            Action StartedCallback,
            Action StoppedCallback
            )
        {
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
            ConverterLibraryCache = new ConcurrentDictionary<string, DynamicRuntimeLibrary>();
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

        public async Task<bool> Stop()
        {
            Debug.Assert(!StopTaskRunning);
            Debug.Assert(_TraceSession != null);
            StopTaskRunning = true;
            var success = true;

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
                StateManager.ProgressState.UpdateProgressMessage(
                    $"Exception occurred: {ex.Message}");
                success = false;
            }
            StopTaskRunning = false;
            return success;
        }

        public async Task<bool> Start()
        {
            Debug.Assert(!StartTaskRunning);
            Debug.Assert(!StopTaskRunning);
            StartTaskRunning = true;
            var success = true;

            try
            {
                BuildConverterLibraryCache();
                TraceStartedCallback();
                await Task.Run(() => ConsumeTraceEvents());
            }
            catch (Exception ex)
            {
                StateManager.ProgressState.FinalizeProgress(
                    $"Exception occurred: {ex.Message}");
                success = false;
            }
            StartTaskRunning = false;
            return success;
        }

        private void ConsumeTraceEvents()  // blocking
        {
            //
            // Setup progress bar.
            //
            if (Configuration.StopCondition == StopCondition.None)
            {
                StateManager.ProgressState.InitializeProgress(100); // arbitrary
                StateManager.ProgressState.ProgressValue = 50;
            }
            else
            {
                StateManager.ProgressState.InitializeProgress(
                    Configuration.StopConditionValue);
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

            etwlib.TraceLogger.SetLevel(StateManager.Settings.TraceLevelEtwlib);

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

                    StateManager.ProgressState.UpdateProgressMessage(
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
            StateManager.ProgressState.FinalizeProgress($"Live session {Configuration.Name} stopped.");
            TaskCompletedEvent.Set();
        }

        public ProviderTraceData? RegisterProviderTab(ConfiguredProvider Provider)
        {
            var id = Provider._EnabledProvider.Id;
            if (!_ProviderTraceData.ContainsKey(id))
            {
                var result = _ProviderTraceData.TryAdd(id, new ProviderTraceData()
                {
                    Columns = Provider.Columns,
                });
                Debug.Assert(result);
            }
            return GetProviderData(id);
        }

        public ProviderTraceData? GetProviderData(Guid ProviderId)
        {
            if (!_ProviderTraceData.ContainsKey(ProviderId))
            {
                Debug.Assert(false);
                return null;
            }
            return _ProviderTraceData[ProviderId];
        }

        public IValueConverter? GetIConverter(string ColumnName)
        {
            if (!ConverterLibraryCache.ContainsKey(ColumnName))
            {
                Debug.Assert(false);
                return null;
            }
            return ConverterLibraryCache[ColumnName].GetInstance() as IValueConverter;
        }

        private void BuildConverterLibraryCache()
        {
            //
            // Compile a small dynamic library for each IConverter supplied in
            // column definitions. This allows users to format display columns
            // to their liking. This is done once per trace session (viewmodel)
            // and reused across tab load/unload operations.
            // These small libraries are kept in memory until the VM is destroyed.
            //
            foreach (var prov in Configuration.ConfiguredProviders)
            {
                foreach (var col in prov.Columns)
                {
                    if (string.IsNullOrEmpty(col.IConverterCode))
                    {
                        continue;
                    }
                    var library = col.GetIConverterLibrary();
                    var result = library.TryCompile(out string err);
                    if (!result)
                    {
                        Debug.Assert(result);
                        ConverterLibraryCache.Clear();
                        return;
                    }
                    result = ConverterLibraryCache.TryAdd(col.Name, library);
                    if (!result)
                    {
                        //
                        // The column was likely defined by a previous configured provider.
                        //
                        continue;
                    }                    
                }
            }
        }
    }

    internal class ProviderTraceData : ViewModelBase
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
