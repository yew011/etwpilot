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
using EtwPilot.Utilities;
using Newtonsoft.Json;

namespace EtwPilot
{
    using static TraceLogger;

    public class Settings : IEquatable<Settings>
    {
        public static readonly string DefaultWorkingDirectory = Path.Combine(
            new string[] { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EtwPilot"});
        public static string DefaultSettingsFileName = "settings.json";
        public static string DefaultSettingsFileLocation = Path.Combine(
            DefaultWorkingDirectory, DefaultSettingsFileName);
        public string? DbghelpPath;
        public string? SymbolPath;
        public SourceLevels TraceLevelApp;
        public SourceLevels TraceLevelEtwlib;
        public SourceLevels TraceLevelSymbolresolver;
        public Dictionary<string, Type> EtwEventColumns;
        public bool HideProvidersWithoutManifest;
        public string? ProviderCacheLocation;

        public Settings()
        {
            EtwEventColumns = new Dictionary<string, Type>();

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
        }

        public override bool Equals(object? Other)
        {
            if (Other == null)
            {
                return false;
            }
            var field = Other as Settings;
            return Equals(field);
        }

        public bool Equals(Settings? Other)
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

        public static bool operator ==(Settings? Settings1, Settings? Settings2)
        {
            if ((object)Settings1 == null || (object)Settings2 == null)
                return Equals(Settings1, Settings2);
            return Settings1.Equals(Settings2);
        }

        public static bool operator !=(Settings? Settings1, Settings? Settings2)
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

        public static void Validate(Settings Object)
        {
            if (string.IsNullOrEmpty(Object.SymbolPath))
            {
                throw new Exception("Symbol path is null");
            }
            if (string.IsNullOrEmpty(Object.DbghelpPath))
            {
                throw new Exception("Dbghelp DLL path is null");
            }
            if (string.IsNullOrEmpty(Object.ProviderCacheLocation))
            {
                throw new Exception("Provider cache location is null");
            }
        }

        static public void Save(Settings Object, string Target = null)
        {
            string target = Target;

            if (string.IsNullOrEmpty(target))
            {
                target = DefaultSettingsFileLocation;
            }

            string json;
            try
            {
                Validate(Object);
                json = JsonConvert.SerializeObject(Object, Formatting.Indented);
                File.WriteAllText(target, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not serialize the Settings object " +
                    $"to JSON: {ex.Message}");
            }
        }

        static public Settings Load(string Location)
        {
            if (!File.Exists(Location))
            {
                throw new Exception("File does not exist");
            }

            Settings settings;

            try
            {
                var json = File.ReadAllText(Location);
                settings = (Settings)JsonConvert.DeserializeObject(json, typeof(Settings))!;
                Validate(settings);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not deserialize settings: {ex.Message}");
            }
            return settings;
        }

        static public Settings LoadDefault()
        {
            var target = Path.Combine(DefaultWorkingDirectory, DefaultSettingsFileName);
            if (!File.Exists(target))
            {
                return new Settings()
                {
                    SymbolPath = @"srv*c:\symbols*https://msdl.microsoft.com/download/symbols",
                    DbghelpPath = @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\dbghelp.dll",
                    ProviderCacheLocation = Path.Combine(DefaultWorkingDirectory, "provider-cache.json"),
                    TraceLevelApp = SourceLevels.Verbose,
                    TraceLevelEtwlib = SourceLevels.Critical,
                    TraceLevelSymbolresolver = SourceLevels.Critical,
                };
            }
            return Load(target);
        }
    }
}

