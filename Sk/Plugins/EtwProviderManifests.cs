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
using etwlib;
using System.Diagnostics;
using EtwPilot.Sk.Vector;
using EtwPilot.ViewModel;

namespace EtwPilot.Sk.Plugins
{
    using static EtwPilot.Utilities.TraceLogger;

    internal class EtwProviderManifests
    {
        private readonly Kernel m_Kernel;

        public EtwProviderManifests(Kernel kernel)
        {
            m_Kernel = kernel;
        }

        [KernelFunction("LoadProviderManifests")]
        [Description("Loads information about all ETW providers registered on the system.")]
        public async Task<string> LoadProviderManifests(CancellationToken Token)
        {
            ProgressState? progress = null;

            if (m_Kernel.Data.TryGetValue("ProgressState", out object? _progress))
            {
                progress = _progress as ProgressState;
            }

            progress?.UpdateProgressMessage($"The model is loading ETW providers, please wait...");

            try
            {
                var vectorDb = m_Kernel.GetRequiredService<EtwVectorDb>();
                var result = await Task.Run(() =>
                {
                    return ProviderParser.GetManifests(2);
                });
                if (result == null || result.Count == 0)
                {
                    Trace(TraceLoggerType.SkPlugin, TraceEventType.Error, $"No provider manifests found");
                    return $$"""
                    {
                        "status": "failed",
                        "message": "Unable to locate any ETW providers.",
                        "next_step": "Do nothing."
                    }
                    """;
                }
                var records = result.Values.ToList();
                progress?.UpdateProgressMessage($"Importing data...");
                await vectorDb.ImportData(vectorDb.s_EtwProviderManifestCollectionName, records, Token, progress);
                return $$"""
                {
                    "status": "success",
                    "message": "There were {{records.Count}} manifests imported.",
                    "next_step": "Invoke the tool that allows you to search ETW manifests."
                }
                """;
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.SkPlugin,
                      TraceEventType.Error,
                      $"Vector data import failed: {ex.Message}");
                //
                // Todo: will diagnostic info help the model somehow?
                //
                return $$"""
                    {
                        "status": "failed",
                        "message": "Unable to import the ETW data into the vector database.",
                        "next_step": "Do nothing."
                    }
                    """;
            }
            finally
            {
                progress?.UpdateProgressMessage($"Import finished.");
            }         
        }
    }
}
