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
using EtwPilot.ViewModel;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EtwPilot.Utilities
{
    using static TraceLogger;

    public class Formatter : IEquatable<Formatter>
    {
        public Guid Id { get; set; }
        public string Namespace { get; set; }
        public string ClassName { get; set; }
        public string Inheritence { get; set; }
        public string FunctionName { get; set; }
        public string Body { get; set; }

        #region IEquatable
        public override bool Equals(object? Other)
        {
            if (Other == null)
            {
                return false;
            }
            var form = Other as Formatter;
            return Equals(form);
        }

        public bool Equals(Formatter? Other)
        {
            if (Other == null)
            {
                return false;
            }
            return Id == Other.Id;
        }

        public static bool operator ==(Formatter? Obj1, Formatter? Obj2)
        {
            if (Obj1 is null)
            {
                return Obj2 is null;
            }
            if (Obj2 is null)
            {
                return Obj1 is null;
            }
            if ((object)Obj1 == null || (object)Obj2 == null)
                return Equals(Obj1, Obj2);
            return Obj1.Equals(Obj2);
        }

        public static bool operator !=(Formatter? Obj1, Formatter? Obj2)
        {
            if (Obj1 is null)
            {
                return Obj2 is not null;
            }
            if (Obj2 is null)
            {
                return Obj1 is not null;
            }
            if ((object)Obj1 == null || (object)Obj2 == null)
                return !Equals(Obj1, Obj2);
            return !(Obj1.Equals(Obj2));
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() +
                Namespace.GetHashCode() +
                ClassName.GetHashCode();
        }
        #endregion

        public Formatter()
        {
        }

        public override string ToString()
        {
            return $"{Namespace}.{ClassName}.{FunctionName}";
        }
    }

    public class AsyncFormatter : DependencyObject
    {
        public class CacheEntry : NotifyPropertyAndErrorInfoBase
        {
            public ParsedEtwEvent Event { get; set; }

            public bool IsFormatted { get; set; }

            #region observable properties

            private string? _UnformattedValue;
            public string? UnformattedValue
            {
                get => _UnformattedValue;
                set
                {
                    _UnformattedValue = value;
                    OnPropertyChanged("UnformattedValue");
                }
            }

            private string? _FormattedValue;
            public string? FormattedValue
            {
                get => _FormattedValue;
                set
                {
                    _FormattedValue = value;
                    OnPropertyChanged("FormattedValue");
                }
            }
            #endregion
        }

        #region dependency properties

        //
        // This DP is linked to a given cell's TextBlock.Text property, so whenever the
        // value is populated, we kick off formatting.
        //
        public static readonly DependencyProperty ObservedTextProperty =
            DependencyProperty.RegisterAttached(
                "ObservedText",
                typeof(string),
                typeof(AsyncFormatter),
                new PropertyMetadata(default(string), OnObservedTextChanged));

        public static string GetObservedText(DependencyObject obj)
        {
            return (string)obj.GetValue(ObservedTextProperty);
        }

        public static void SetObservedText(DependencyObject obj, string value)
        {
            obj.SetValue(ObservedTextProperty, value);
        }

        #endregion

        public object TargetInstance { get; set; }
        public Type TargetType { get; set; }
        public string MethodName { get; set; }

        private readonly Formatter m_Formatter;
        private readonly FormatterLibrary m_Library;
        private ConcurrentBag<CacheEntry> m_Cache;

        public AsyncFormatter(Formatter _Formatter, FormatterLibrary library)
        {
            m_Formatter = _Formatter;
            m_Library = library;
            m_Cache = new ConcurrentBag<CacheEntry>();
        }

        public CacheEntry GetFormatterEntry(string Contents)
        {
            var cached = m_Cache.FirstOrDefault(c => c.UnformattedValue == Contents);
            if (cached != default)
            {
                return cached;
            }
            //
            // The caller should populate...
            //
            return new CacheEntry();
        }

        private static async void OnObservedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var textBlock = d as TextBlock;
            if (textBlock == null)
            {
                Debug.Assert(false);
                return;
            }

            //
            // Retrieve the column formatter from the TextBlock's Tag.
            //
            var asyncFormatter = textBlock.Tag as AsyncFormatter;
            if (asyncFormatter == null)
            {
                Debug.Assert(false);
                return;
            }

            //
            // Get the cell's current contents.
            //
            var contents = e.NewValue as string;
            if (string.IsNullOrEmpty(contents))
            {
                return;
            }

            //
            // See if this value has been formatted before. If so, stop processing now.
            //
            var context = asyncFormatter.GetFormatterEntry(contents);
            if (context.IsFormatted)
            {
                //
                // Remove the binding for this cell, it is done
                //
                BindingOperations.ClearBinding(textBlock, TextBlock.TextProperty);
                textBlock.Text = context.FormattedValue;
                return;
            }

            //
            // We want to pass the full event as opposed to the cell's contents because the
            // formatter might want to consider other data in the event when formatting.
            //
            var evt = textBlock.DataContext as ParsedEtwEvent;
            if (evt == null)
            {
                Debug.Assert(false);
                return;
            }

            context.Event = evt;
            context.UnformattedValue = contents;

            //
            // Overwrite this cell's binding to the result of the async formatter.
            //
            textBlock.SetBinding(TextBlock.TextProperty, new Binding()
            {
                Source = context,
                Path = new PropertyPath("FormattedValue"),
                IsAsync = true
            });
            await asyncFormatter.RunAsync(context);
            context.IsFormatted = true;
        }

        public async Task RunAsync(CacheEntry Entry)
        {
            Debug.Assert(Entry.FormattedValue==null);
            Debug.Assert(Entry.Event != null);

            object[] args = [
                    Entry.Event,
                    Entry.UnformattedValue!,
                    GlobalStateViewModel.Instance.m_StackwalkHelper // only used by some formatters
                ];
            try
            {
                Entry.FormattedValue = await m_Library.InvokeMethodAsync<string?>(m_Formatter, args);
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.FormatterLibrary, TraceEventType.Error, ex.Message);
                if (ex.InnerException != null)
                {
                    Trace(TraceLoggerType.FormatterLibrary, TraceEventType.Error, ex.InnerException.Message);
                }
            }
        }
    }

    public class FormatterLibrary
    {
        private List<Formatter> m_Formatters;
        public bool m_Ready { get; set; } // only after finalized
        private Assembly m_Assembly;

        public FormatterLibrary()
        {
            m_Formatters = new List<Formatter>();
        }

        public async Task<bool> Publish(List<Formatter> Formatters)
        {
            if (Formatters.Count == 0)
            {
                //
                // There should always at least be the default formatters.
                //
                return false;
            }

            m_Formatters = Formatters;

            var code = @"
                using System;
                using System.Threading.Tasks; 
                using System.Diagnostics;
                using System.Text;
                using etwlib;
                using EtwPilot.Utilities;
                using static EtwPilot.Utilities.TraceLogger;";

            foreach (var formatter in m_Formatters)
            {
                code += GetFormattedCode(formatter);
            }

            try
            {
                Compile(code);
                if (!await SanityCheck(code))
                {
                    throw new Exception("Sanity check failed");
                }
                m_Ready = true;
                return true;
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.FormatterLibrary, TraceEventType.Error, ex.Message);
                var info = GetDiagnosticInfo();
                Trace(TraceLoggerType.FormatterLibrary, TraceEventType.Error, info);
                return false;
            }
        }

        public AsyncFormatter GetAsyncFormatter(Formatter Formatter)
        {
            return new AsyncFormatter(Formatter, this);
        }

        private string GetFormattedCode(Formatter Formatter)
        {
            var code = @$"namespace {Formatter.Namespace} {{
                public class {Formatter.ClassName}";
            if (!string.IsNullOrEmpty(Formatter.Inheritence))
            {
                code += $": {Formatter.Inheritence}";
            }
            code += @$" {{
                    public async Task<string?> {Formatter.FunctionName}(object[] Args) {{
                    {Formatter.Body}
                    }}
                }}
            }}";
            return code;
        }

        public void Compile(string Code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(Code);
            var assemblyName = Path.GetRandomFileName();
            var etwpilotPath = Path.GetDirectoryName(typeof(MainWindow).GetTypeInfo().Assembly.Location);
            /*
             * Note: Can also use `Basic.Reference.Assemblies.Net90` as follows, to avoid janky
             * typeof(XXX).Assembly.Location approach below, but I'm hesitant to bind this project
             * to the one maintainer of this nuget package updating his packages with each new .net release
             * 
             * var references = new MetadataReference[] {
                MetadataReference.CreateFromFile(Path.Combine(etwpilotPath!, "etwlib.dll")),
                MetadataReference.CreateFromFile(Path.Combine(etwpilotPath!, "etwpilot.dll")),
            }.Concat(Net90.References.All);
            */
            var basePath = Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location);
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(Path.Combine(etwpilotPath!, "etwlib.dll")),
                MetadataReference.CreateFromFile(Path.Combine(etwpilotPath!, "etwpilot.dll")),
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Windows.Data.IValueConverter).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(basePath!, "netstandard.dll")),
                MetadataReference.CreateFromFile(Path.Combine(basePath!, "system.collections.dll")),
                MetadataReference.CreateFromFile(Path.Combine(basePath!, "system.runtime.dll")),
                MetadataReference.CreateFromFile(Path.Combine(basePath!, "system.diagnostics.debug.dll")),
                MetadataReference.CreateFromFile(Path.Combine(basePath!, "system.diagnostics.tracesource.dll")),
            };
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);
                    var sb = new StringBuilder();
                    foreach (Diagnostic diagnostic in failures)
                    {
                        sb.AppendLine($"{diagnostic.Location} {diagnostic.Id}: {diagnostic.GetMessage()}");
                    }
                    throw new Exception(sb.ToString());
                }

                if (ms.Length == 0)
                {
                    throw new Exception("Empty IL stream");
                }
                ms.Seek(0, SeekOrigin.Begin);
                m_Assembly = Assembly.Load(ms.ToArray());
                if (m_Assembly == null)
                {
                    throw new Exception("Unable to generate assembly from IL.");
                }
            }
        }

        public object GetInstance(Formatter Formatter, out Type InstanceType)
        {
            if (m_Assembly == null)
            {
                throw new Exception("No assembly available");
            }
            var typeName = $"{Formatter.Namespace}.{Formatter.ClassName}";
            var classType = m_Assembly.GetType(typeName);
            if (classType == null)
            {
                throw new Exception($"Unable to locate type named {typeName}");
            }
            var obj = Activator.CreateInstance(classType);
            if (obj == null)
            {
                throw new Exception($"Unable to create instance of type named {typeName}");
            }
            InstanceType = classType;
            return obj;
        }

        public object? InvokeMethod(Formatter Formatter, object[]? Args)
        {
            if (m_Assembly == null)
            {
                throw new Exception("No assembly available");
            }
            var obj = GetInstance(Formatter, out Type type);
            var method = Formatter.FunctionName;
            var methodInfo = type.GetMethod(method);
            if (methodInfo == null)
            {
                throw new Exception($"Method {method} not found in type {type}");
            }
            return type.InvokeMember(method,
                BindingFlags.Default | BindingFlags.InvokeMethod,
                null,
                obj,
                new object[] { Args });
        }

        public async Task<T?> InvokeMethodAsync<T>(Formatter Formatter, object[] Args)
        {
            if (m_Assembly == null)
            {
                throw new Exception("No assembly available");
            }
            var method = Formatter.FunctionName;
            var obj = GetInstance(Formatter, out Type type);
            var methodInfo = type.GetMethod(method);
            if (methodInfo == null)
            {
                throw new Exception($"Method {method} not found in type {type}");
            }
            return await (Task<T?>)type.InvokeMember(
                    method,
                    BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public,
                    Type.DefaultBinder,
                    obj,
                    new object[] { Args })!;
        }

        public string GetDiagnosticInfo()
        {
            var info = new StringBuilder();
            if (m_Assembly != null)
            {
                info.AppendLine($"Compiled assembly {m_Assembly.GetName().Name} types:");
                foreach (Type type in m_Assembly.GetTypes())
                {
                    info.AppendLine($"   {type.FullName}");
                    foreach (var method in type.GetMethods())
                    {
                        info.AppendLine($"      Method {method.Name}");
                    }
                }
            }
            return info.ToString();
        }

        internal static string GetGuidBasedName(Guid Id, string Prefix)
        {
            return $"{Prefix}_{Id.ToString().Replace("-", "_")}";
        }

        private async Task<bool> SanityCheck(string Code)
        {
            foreach (var formatter in m_Formatters)
            {
                var result = GetAsyncFormatter(formatter);
                if (result == null)
                {
                    Trace(TraceLoggerType.FormatterLibrary, TraceEventType.Error, Code);
                    var info = GetDiagnosticInfo();
                    Trace(TraceLoggerType.FormatterLibrary, TraceEventType.Error, info);
                    Debug.Assert(false);
                    return false;
                }
                var dummyEvent = new ParsedEtwEvent()
                {
                    ProcessId = 1337,
                    ActivityId = Guid.NewGuid(),
                    Channel = new ParsedEtwString("Channel1", 1000),
                    Provider = new ParsedEtwProvider()
                    {
                        Name = "Dummy Provider",
                        Id = Guid.NewGuid()
                    },
                    EventId = 1,
                    Version = 1,
                    ProcessStartKey = 1,
                    ThreadId = 133737,
                    UserSid = "S-1-1337",
                    Timestamp = DateTime.Now,
                    Level = "LEVEL_HELL_YEA",
                    Keywords = "KEYWORDS_HELL_YEA",
                    KeywordsUlong = 1,
                    Task = new ParsedEtwString("COOL_TASK", 1),
                    Opcode = new ParsedEtwString("COOL_OP_BRO", 2),
                    StackwalkAddresses = new List<ulong>(),
                    StackwalkMatchId = 0,
                    TemplateData = new List<ParsedEtwTemplateItem>()
                };

                var ctx = result.GetFormatterEntry("500");
                ctx.Event = dummyEvent;
                ctx.UnformattedValue = "500";
                await result.RunAsync(ctx);
                var formatted = ctx.FormattedValue;
                //
                // TODO: We can't really validate what was produced. Need to add some sort
                // of built-in unit testing capability here to truly validate formatters.
                //
            }
            return true;
        }
    }
}
