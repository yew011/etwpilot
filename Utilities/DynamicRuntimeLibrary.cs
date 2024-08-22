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
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EtwPilot.Utilities
{
    public class DynamicRuntimeLibrary : IDisposable
    {
        private bool m_Disposed;
        private string m_UsingBlock;
        private string m_Namespace;
        private string m_ClassName;
        private string m_ClassInherit;
        private string m_ClassBody;
        private MemoryStream m_CompiledCode;
        private object m_Instance;
        private Type? m_ClassType;

        public DynamicRuntimeLibrary(
            string UsingBlock,
            string Namespace,
            string ClassName,
            string ClassInherit,
            string ClassBody
            )
        {
            m_UsingBlock = UsingBlock;
            m_Namespace = Namespace;
            m_ClassName = ClassName;
            m_ClassBody = ClassBody;
            m_ClassInherit = ClassInherit;
            m_CompiledCode = new MemoryStream();
        }

        ~DynamicRuntimeLibrary()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;

            m_CompiledCode.Close();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool TryCompile(out string Error)
        {
            try
            {
                Compile();
                Error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                return false;
            }
        }

        public object GetInstance()
        {
            if (m_Instance != null)
            {
                return m_Instance;
            }

            if (m_CompiledCode.Length == 0)
            {
                throw new Exception("No compilation available");
            }

            m_CompiledCode.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(m_CompiledCode.ToArray());
            if (assembly == null)
            {
                throw new Exception("Unable to load compiled code.");
            }
            var typeName = $"{m_Namespace}.{m_ClassName}";
            m_ClassType = assembly.GetType(typeName);
            if (m_ClassType == null)
            {
                throw new Exception($"Unable to locate type named {typeName}");
            }
            var obj = Activator.CreateInstance(m_ClassType);
            if (obj == null)
            {
                throw new Exception($"Unable to create instance of type named {typeName}");
            }
            m_Instance = obj;
            return obj;
        }

        public void InvokeMethod(string MethodName, object[]? Args)
        {
            if (m_CompiledCode.Length == 0)
            {
                throw new Exception("Code must be compiled before invoking methods.");
            }
            var obj = GetInstance();
            m_ClassType!.InvokeMember(MethodName,
                BindingFlags.Default | BindingFlags.InvokeMethod,
                null,
                obj,
                Args);
        }

        private void Compile()
        {
            if (string.IsNullOrEmpty(m_UsingBlock) || string.IsNullOrEmpty(m_Namespace) ||
               string.IsNullOrEmpty(m_ClassName) || string.IsNullOrEmpty(m_ClassBody))
            {
                throw new Exception("Code blocks are incomplete.");
            }
            if (m_CompiledCode.Length != 0)
            {
                throw new Exception("Library already compiled");
            }
            var classDecl = string.IsNullOrEmpty(m_ClassInherit) ? m_ClassName :
                $"{m_ClassName}: {m_ClassInherit}";
            var code = $"{m_UsingBlock} namespace {m_Namespace} {{" +
                    $" public class {classDecl} {{" +
                    $" {m_ClassBody} }} }}";
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var assemblyName = Path.GetRandomFileName();
            var basePath = Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location);
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Windows.Data.IValueConverter).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(basePath!, "netstandard.dll")),
                MetadataReference.CreateFromFile(Path.Combine(basePath!, "system.collections.dll")),
                MetadataReference.CreateFromFile(Path.Combine(basePath!, "system.runtime.dll")),
            };
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var result = compilation.Emit(m_CompiledCode);
            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);
                var sb = new StringBuilder();
                foreach (Diagnostic diagnostic in failures)
                {
                    sb.AppendLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");
                }
                throw new Exception(sb.ToString());
            }
        }
    }
}