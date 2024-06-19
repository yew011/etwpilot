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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace EtwPilot.Model
{
    using static EtwPilot.Utilities.TraceLogger;

    internal class SettingsModel : IEquatable<SettingsModel>
    {
        public static readonly string DefaultWorkingDirectory = Path.Combine(
            new string[] { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "etwpilot"});
        public static string DefaultSettingsFileName = "settings.json";
        public static string DefaultSettingsFileLocation = Path.Combine(
            DefaultWorkingDirectory, DefaultSettingsFileName);
        public string? DbghelpPath { get; set; }
        public string? SymbolPath { get; set; }
        public SourceLevels TraceLevelApp { get; set; }
        public SourceLevels TraceLevelEtwlib { get; set; }
        public SourceLevels TraceLevelSymbolresolver { get; set; }
        public ObservableCollection<EtwColumn> EtwEventColumns { get; set; }
        public bool HideProvidersWithoutManifest { get; set; }
        public string? ProviderCacheLocation { get; set; }

        public SettingsModel()
        {
            if (!Directory.Exists(DefaultWorkingDirectory))
            {
                try
                {
                    Directory.CreateDirectory(DefaultWorkingDirectory);
                }
                catch (Exception ex)
                {
                    Trace(TraceLoggerType.Settings,
                          TraceEventType.Warning,
                          $"Unable to create settings directory " +
                          $"'{DefaultWorkingDirectory}': {ex.Message}");
                }
            }

            SymbolPath = @"srv*c:\symbols*https://msdl.microsoft.com/download/symbols";
            DbghelpPath = @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\dbghelp.dll";
            ProviderCacheLocation = Path.Combine(DefaultWorkingDirectory, "provider-cache.json");
            TraceLevelApp = SourceLevels.Verbose;
            TraceLevelEtwlib = SourceLevels.Critical;
            TraceLevelSymbolresolver = SourceLevels.Critical;
            EtwEventColumns = new ObservableCollection<EtwColumn>();
        }

        public override bool Equals(object? Other)
        {
            if (Other == null)
            {
                return false;
            }
            var field = Other as SettingsModel;
            return Equals(field);
        }

        public bool Equals(SettingsModel? Other)
        {
            if (Other == null)
            {
                return false;
            }
            return DbghelpPath == Other.DbghelpPath &&
                SymbolPath == Other.SymbolPath &&
                ProviderCacheLocation == Other.ProviderCacheLocation &&
                TraceLevelApp == Other.TraceLevelApp &&
                TraceLevelEtwlib == Other.TraceLevelEtwlib &&
                TraceLevelSymbolresolver == Other.TraceLevelSymbolresolver &&
                HideProvidersWithoutManifest == Other.HideProvidersWithoutManifest &&
                (EtwEventColumns.Count == Other.EtwEventColumns.Count &&
                !EtwEventColumns.Except(Other.EtwEventColumns).Any());
        }

        public static bool operator ==(SettingsModel? Settings1, SettingsModel? Settings2)
        {
            if ((object)Settings1 == null || (object)Settings2 == null)
                return Equals(Settings1, Settings2);
            return Settings1.Equals(Settings2);
        }

        public static bool operator !=(SettingsModel? Settings1, SettingsModel? Settings2)
        {
            if ((object)Settings1 == null || (object)Settings2 == null)
                return !Equals(Settings1, Settings2);
            return !(Settings1.Equals(Settings2));
        }

        public override int GetHashCode()
        {
            return (DbghelpPath,
                SymbolPath,
                ProviderCacheLocation,
                TraceLevelApp,
                TraceLevelEtwlib,
                TraceLevelSymbolresolver,
                EtwEventColumns,
                HideProvidersWithoutManifest).GetHashCode();
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(SymbolPath))
            {
                throw new Exception("Symbol path is null");
            }
            if (string.IsNullOrEmpty(DbghelpPath))
            {
                throw new Exception("Dbghelp DLL path is null");
            }
            if (string.IsNullOrEmpty(ProviderCacheLocation))
            {
                throw new Exception("Provider cache location is null");
            }
            if (EtwEventColumns.Count == 0)
            {
                throw new Exception("At least one ETW column is required.");
            }
        }

        public void SetDefaultEtwColumns()
        {
            EtwEventColumns = EtwColumn.GetDefaultColumns();
        }
    }
}
