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
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using etwlib;
using static etwlib.NativeTraceConsumer;
using static etwlib.NativeTraceControl;
using System.Configuration;
using System.Diagnostics;
using EtwPilot.Utilities;
using System.Runtime.InteropServices;
using Meziantou.Framework.WPF.Collections;
using Newtonsoft.Json.Linq;
using EtwPilot.Sk.Vector;

namespace EtwPilot.Sk.Plugins
{
    using static EtwPilot.Utilities.TraceLogger;

    public class TraceSessionParameters
    {
        public int StopOnSizeMb { get; set; }
        public int StopOnTimeSec { get; set; }
        public List<string> ProviderNamesOrGuids { get; set; }
        public List<int> TargetProcessIds { get; set; }
        public List<string> TargetProcessNames { get; set; }

        public List<int> EventIds { get; set; }

        public TraceSessionParameters()
        {
            StopOnSizeMb = 0;
            StopOnTimeSec = 0;
            ProviderNamesOrGuids = new List<string>();
            TargetProcessIds = new List<int>();
            TargetProcessNames = new List<string>();
            EventIds = new List<int>();
        }

    }
    internal class EtwTraceSession
    {
        private DateTime StartTime;
        private Stopwatch _Stopwatch { get; set; }
        private ConcurrentObservableCollection<ParsedEtwEvent> Data;
        private long _EventsConsumed;
        private long EventsConsumed
        {
            get { return Interlocked.Read(ref _EventsConsumed); }
            set
            {
                if (_EventsConsumed != value)
                {
                    Interlocked.Exchange(ref _EventsConsumed, value);
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
                }
            }
        }
        private readonly EtwVectorDb m_VectorDb;

        public EtwTraceSession(EtwVectorDb VectorDb)
        {
            m_VectorDb = VectorDb;
            _Stopwatch = new Stopwatch();
            Data = new ConcurrentObservableCollection<ParsedEtwEvent>();
            EventsConsumed = 0;
            BytesConsumed = 0;
        }

        [KernelFunction("StartTrace")]
        [Description("Starts an ETW trace session to find ETW events of interest.")]
        public async Task StartTrace(
            [Description("Settings that control what ETW data is collected")]
            [Required()]TraceSessionParameters Parameters,
            CancellationToken Token)
        {
            EventsConsumed = 0;
            BytesConsumed = 0;
            Data.Clear();
            StartTime = DateTime.Now;
            await Task.Run(() => ConsumeTraceEvents(Parameters, Token));
            await m_VectorDb.ImportData(ViewModel.InsightsViewModel.ChatTopic.EventData, Data.ToList(), Token);
        }

        private void ConsumeTraceEvents(TraceSessionParameters Parameters, CancellationToken Token)
        {
            ValidateParameters(Parameters);
            var providers = GetEnabledProviders(Parameters);
            var randomTraceName = $"etwpilot-chat-trace-{Guid.NewGuid()}";

            using (var session = new RealTimeTrace(randomTraceName))
            {
                etwlib.TraceLogger.SetLevel(SourceLevels.Error); // TODO: Save/restore this
                using (var parserBuffers = new EventParserBuffers())
                {
                    try
                    {
                        foreach (var provider in providers)
                        {
                            session.AddProvider(provider);
                        }

                        session.Start();
                        _Stopwatch.Start();

                        //
                        // Begin consuming events. This is a blocking call.
                        //
                        session.Consume(new EventRecordCallback((Event) =>
                        {
                            var evt = (EVENT_RECORD)Marshal.PtrToStructure(Event, typeof(EVENT_RECORD))!;
                            var parser = new EventParser(
                                evt,
                                parserBuffers,
                                session.GetPerfFreq());
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
                                Trace(TraceLoggerType.SkPlugin,
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
                            if (Token.IsCancellationRequested)
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
                                Trace(TraceLoggerType.SkPlugin,
                                      TraceEventType.Error,
                                      $"Unable to cast EVENT_TRACE_LOGFILE: {ex.Message}");
                            }

                            BytesConsumed += logfile.Filled;
                            var mbConsumed = Math.Round((double)BytesConsumed / 1000000, 2);
                            var elapsedSec = (int)Math.Floor((decimal)_Stopwatch.ElapsedMilliseconds / 1000);

                            if (Parameters.StopOnSizeMb > 0 && mbConsumed > Parameters.StopOnSizeMb)
                            {
                                return 0;
                            }
                            else if (Parameters.StopOnTimeSec > 0 && elapsedSec > Parameters.StopOnTimeSec)
                            {
                                return 0;
                            }
                            return 1;
                        }));
                    }
                    catch (Exception ex)
                    {
                        Trace(TraceLoggerType.SkPlugin,
                              TraceEventType.Error,
                              $"An exception occurred when consuming events: {ex.Message}");
                        throw;
                    }
                }

                _Stopwatch.Stop();
            }
        }

        private void ValidateParameters(TraceSessionParameters Parameters)
        {
            if (Parameters.ProviderNamesOrGuids.Count == 0)
            {
                throw new Exception("At least one provider must be added to TraceSessionParameters");
            }
            if (Parameters.StopOnSizeMb == 0 && Parameters.StopOnTimeSec == 0)
            {
                throw new Exception("A non-zero stop condition of event data size (in megabytes) or" +
                    " trace time (in seconds) is required");
            }
        }

        private List<EnabledProvider> GetEnabledProviders(TraceSessionParameters Parameters)
        {
            //
            // Gather info about each provider to be enabled in the trace.
            //
            var providers = new List<EnabledProvider>();
            foreach (var entry in Parameters.ProviderNamesOrGuids)
            {
                ParsedEtwProvider? provider;
                if (!Guid.TryParse(entry, out Guid id))
                {
                    provider = ProviderParser.GetProvider(entry);

                }
                else
                {
                    provider = ProviderParser.GetProvider(new Guid(entry));
                }

                if (provider == default)
                {
                    throw new Exception($"Unable to locate a provider named {entry}");
                }
                providers.Add(new EnabledProvider(provider.Id,
                        provider.Name!,
                        (byte)NativeTraceControl.EventTraceLevel.Information,
                        0,
                        ulong.MaxValue));
            }

            //
            // Setup per-provider ETW filters. For now, we're allowing a simple subset of
            // possible filters through AI function calling - things like stackwalk and
            // payload filters are too complex.
            //
            foreach (var provider in providers)
            {
                //
                // Scope filter
                //
                if (Parameters.TargetProcessIds.Count > 0) // process
                {
                    provider.SetProcessFilter(Parameters.TargetProcessIds);
                }
                if (Parameters.TargetProcessNames.Count > 0) // exe
                {
                    var str = EtwHelper.GetEtwStringList(Parameters.TargetProcessNames);
                    provider.SetFilteredExeName(str);
                }
                //
                // Attribute filter
                //
                if (Parameters.EventIds.Count > 0)
                {
                    provider.SetEventIdsFilter(Parameters.EventIds, true);
                }
            }

            return providers;
        }
    }
}
