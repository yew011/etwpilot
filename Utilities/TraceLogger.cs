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
using System.Diagnostics;
using System.IO;

namespace EtwPilot.Utilities
{
    using EtwPilot.ViewModel;

    public static class TraceLogger
    {
        public static readonly string m_TraceFileDir = Path.Combine(new string[] { 
            SettingsFormViewModel.DefaultWorkingDirectory, "Logs" });
        public static string m_Location = Path.Combine(new string[] { m_TraceFileDir,
                            $"EtwPilot-{DateTime.Now.ToString("yyyy-MM-dd-HHmmss")}.txt"});
        private static TextWriterTraceListener m_TraceListener =
            new TextWriterTraceListener(m_Location, "EtwPilotListener");
        private static SourceSwitch m_Switch =
            new SourceSwitch("EtwPilotListener", "Verbose");
        private static TraceSource[] Sources = {
            new TraceSource("MainWindow", SourceLevels.Verbose),
            new TraceSource("LiveSession", SourceLevels.Verbose),
            new TraceSource("Settings", SourceLevels.Verbose),
            new TraceSource("Providers", SourceLevels.Verbose),
            new TraceSource("Sessions", SourceLevels.Verbose),
            new TraceSource("UiHelper", SourceLevels.Verbose),
            new TraceSource("Inference", SourceLevels.Verbose),
            new TraceSource("Vector", SourceLevels.Verbose),
            new TraceSource("FormatterLibrary", SourceLevels.Verbose),
            new TraceSource("SymbolResolver", SourceLevels.Verbose),
            new TraceSource("AsyncFormatter", SourceLevels.Verbose),
            new TraceSource("SkPlugin", SourceLevels.Verbose),
            new TraceSource("Agents", SourceLevels.Verbose),
        };

        public enum TraceLoggerType
        {
            MainWindow,
            LiveSession,
            Settings,
            Providers,
            Sessions,
            UiHelper,
            Inference,
            Vector,
            FormatterLibrary,
            SymbolResolver,
            AsyncFormatter,
            SkPlugin,
            Agents,
            Max
        }

        public static void Initialize()
        {
            System.Diagnostics.Trace.AutoFlush = true;
            foreach (var source in Sources)
            {
                source.Listeners.Add(m_TraceListener);
                source.Switch = m_Switch;
            }

            if (Directory.Exists(SettingsFormViewModel.DefaultWorkingDirectory))
            {
                if (!Directory.Exists(m_TraceFileDir))
                {
                    try
                    {
                        Directory.CreateDirectory(m_TraceFileDir);
                    }
                    catch (Exception) // swallow
                    {
                    }
                }
            }
        }

        public static void SetLevel(SourceLevels Level)
        {
            m_Switch.Level = Level;
        }

        public static void Trace(TraceLoggerType Type, TraceEventType EventType, string Message)
        {
            if (Type >= TraceLoggerType.Max)
            {
                throw new Exception("Invalid logger type");
            }
            Sources[(int)Type].TraceEvent(EventType, 1, Message);
        }
    }
}
