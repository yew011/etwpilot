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
using System.Windows.Threading;
using System.Windows.Data;
using Meziantou.Framework.WPF;
using Meziantou.Framework.WPF.Collections;

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

        #region observable properties

        private int _EventsConsumed;
        public int EventsConsumed
        {
            get => _EventsConsumed;
            set
            {
                if (_EventsConsumed != value)
                {
                    _EventsConsumed = value;
                    OnPropertyChanged("EventsConsumed");
                }
            }
        }

        private uint _BytesConsumed;
        public uint BytesConsumed
        {
            get => _BytesConsumed;
            set
            {
                if (_BytesConsumed != value)
                {
                    _BytesConsumed = value;
                    OnPropertyChanged("BytesConsumed");
                }
            }
        }

        private ConcurrentObservableCollection<ParsedEtwEvent> _Data;
        public ConcurrentObservableCollection<ParsedEtwEvent> Data
        {
            get => _Data;
            private set { _Data = value; }
        }
        #endregion

        public SessionFormModel Configuration { get; set; }
        public Stopwatch Stopwatch { get; set; }
        private Task CurrentTask;
        private CancellationTokenSource CancellationSource;
        private AutoResetEvent TaskCompletedEvent;

        public LiveSessionViewModel(SessionFormModel SessionModel)
        {
            CancellationSource = new CancellationTokenSource();
            TaskCompletedEvent = new AutoResetEvent(false);
            Stopwatch = new Stopwatch();
            Configuration = SessionModel;
            Data = new ConcurrentObservableCollection<ParsedEtwEvent>();
        }

        public async Task Stop()
        {
            await Task.Run(() =>
            {
                CancellationSource.Cancel();
                TaskCompletedEvent.WaitOne();
            });
        }

        public async Task Start()
        {
            try
            {
                await Task.Run(() =>
                {
                    StartInternal();
                });
            }
            catch (Exception ex)
            {
                StateManager.ProgressState.UpdateProgressMessage(
                    $"Exception occurred: {ex.Message}");
            }
        }

        private void StartInternal()
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
            TraceSession trace;
            if (!Configuration.IsRealTime)
            {
                var sb = new StringBuilder(Configuration.Name);
                foreach (char item in Path.GetInvalidFileNameChars())
                {
                    sb = sb.Replace(item.ToString(), "");
                }
                var filename = $"{sb}-{DateTime.Now}.etl";
                var target = Path.Combine(Configuration.LogLocation, filename);
                trace = new FileTrace(target);
            }
            else
            {
                trace = new RealTimeTrace(Configuration.Name);
            }

            etwlib.TraceLogger.SetLevel(StateManager.SettingsModel.TraceLevelEtwlib);

            //
            // Start trace
            //
            using (trace)
            using (var parserBuffers = new EventParserBuffers())
            {
                try
                {
                    foreach (var provider in Configuration.EnabledProviders)
                    {
                        trace.AddProvider(provider);
                    }

                    trace.Start();

                    StateManager.ProgressState.UpdateProgressMessage(
                        $"Live session {Configuration.Name} has started.");
                    Stopwatch.Start();

                    //
                    // Begin consuming events. This is a blocking call.
                    //
                    trace.Consume(new EventRecordCallback((Event) =>
                    {
                        var evt = (EVENT_RECORD)Marshal.PtrToStructure(
                                Event, typeof(EVENT_RECORD))!;

                        var parser = new EventParser(
                            evt,
                            parserBuffers,
                            trace.GetPerfFreq());
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

                        Data.Add(parsedEvent);
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
            StateManager.ProgressState.FinalizeProgress();
            TaskCompletedEvent.Set();
        }
    }
}
