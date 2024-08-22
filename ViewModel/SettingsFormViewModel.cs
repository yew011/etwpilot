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
using CommunityToolkit.Mvvm.Input;
using EtwPilot.Model;
using Newtonsoft.Json;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;

    internal class SettingsFormViewModel : ViewModelBase
    {
        public static readonly string DefaultWorkingDirectory = Path.Combine(
            new string[] { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "etwpilot"});
        public static readonly string DefaultSettingsFileName = "settings.json";

        #region observable properties

        private string _DbghelpPath;
        public string DbghelpPath
        {
            get => _DbghelpPath;
            set
            {
                if (_DbghelpPath != value)
                {
                    _DbghelpPath = value;
                    OnPropertyChanged("DbghelpPath");
                    ClearErrors(nameof(DbghelpPath));
                    if (string.IsNullOrEmpty(value) || !File.Exists(value))
                    {
                        AddError(nameof(DbghelpPath), "DbghelpPath is null or invalid");
                    }
                }
            }
        }

        private string _SymbolPath;
        public string SymbolPath
        {
            get => _SymbolPath;
            set
            {
                if (_SymbolPath != value)
                {
                    _SymbolPath = value;
                    OnPropertyChanged("SymbolPath");

                    ClearErrors(nameof(SymbolPath));
                    if (string.IsNullOrEmpty(value))
                    {
                        AddError(nameof(SymbolPath), "Symbol path is null");
                    }
                }
            }
        }

        private SourceLevels _TraceLevelApp;
        public SourceLevels TraceLevelApp
        {
            get => _TraceLevelApp;
            set
            {
                if (_TraceLevelApp != value)
                {
                    _TraceLevelApp = value;
                    OnPropertyChanged("TraceLevelApp");
                }
            }
        }

        private SourceLevels _TraceLevelEtwlib;
        public SourceLevels TraceLevelEtwlib
        {
            get => _TraceLevelEtwlib;
            set
            {
                if (_TraceLevelEtwlib != value)
                {
                    _TraceLevelEtwlib = value;
                    OnPropertyChanged("TraceLevelEtwlib");
                }
            }
        }

        private SourceLevels _TraceLevelSymbolresolver;
        public SourceLevels TraceLevelSymbolresolver
        {
            get => _TraceLevelSymbolresolver;
            set
            {
                if (_TraceLevelSymbolresolver != value)
                {
                    _TraceLevelSymbolresolver = value;
                    OnPropertyChanged("TraceLevelSymbolresolver");
                }
            }
        }

        private bool _HideProvidersWithoutManifest;
        public bool HideProvidersWithoutManifest
        {
            get => _HideProvidersWithoutManifest;
            set
            {
                if (_HideProvidersWithoutManifest != value)
                {
                    _HideProvidersWithoutManifest = value;
                    OnPropertyChanged("HideProvidersWithoutManifest");
                }
            }
        }

        private bool _UseDefaultEtwColumns;
        public bool UseDefaultEtwColumns
        {
            get => _UseDefaultEtwColumns;
            set
            {
                if (_UseDefaultEtwColumns != value)
                {
                    _UseDefaultEtwColumns = value;
                    OnPropertyChanged("UseDefaultEtwColumns");
                }
            }
        }

        private string _ProviderCacheLocation;
        public string ProviderCacheLocation
        {
            get => _ProviderCacheLocation;
            set
            {
                if (_ProviderCacheLocation != value)
                {
                    _ProviderCacheLocation = value;
                    OnPropertyChanged("ProviderCacheLocation");
                    ClearErrors(nameof(ProviderCacheLocation));
                    if (string.IsNullOrEmpty(value))
                    {
                        AddError(nameof(ProviderCacheLocation), "Provider cache location is null");
                    }
                }
            }
        }

        private string _ModelPath;
        public string ModelPath
        {
            get => _ModelPath;
            set
            {
                if (_ModelPath != value)
                {
                    ClearErrors(nameof(ModelPath));
                    _ModelPath = value;
                    OnPropertyChanged("ModelPath");
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (!InsightsConfigurationModel.ValidateModelPath(value))
                        {
                            AddError(nameof(ModelPath), "Model path is invalid");
                        }
                        if (!InsightsConfigurationModel.ValidateEmbeddingsModelFile(EmbeddingsModelFile))
                        {
                            AddError(nameof(EmbeddingsModelFile), "Embeddings model file is invalid");
                        }
                    }
                }
            }
        }

        private string _EmbeddingsModelFile;
        public string EmbeddingsModelFile
        {
            get => _EmbeddingsModelFile;
            set
            {
                if (_EmbeddingsModelFile != value)
                {
                    ClearErrors(nameof(EmbeddingsModelFile));
                    _EmbeddingsModelFile = value;
                    OnPropertyChanged("EmbeddingsModelFile");
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (!InsightsConfigurationModel.ValidateModelPath(ModelPath))
                        {
                            AddError(nameof(ModelPath), "Model path is invalid");
                        }
                        if (!InsightsConfigurationModel.ValidateEmbeddingsModelFile(value))
                        {
                            AddError(nameof(EmbeddingsModelFile), "Embeddings model file is invalid");
                        }
                    }
                }
            }
        }

        private OnnxGenAISearchOptionsModel _SearchOptions;
        public OnnxGenAISearchOptionsModel SearchOptions
        {
            get => _SearchOptions;
            set
            {
                if (_SearchOptions != value)
                {
                    ClearErrors(nameof(SearchOptions));
                    _SearchOptions = value;
                    OnPropertyChanged("SearchOptions");
                }
            }
        }

        #endregion

        [JsonIgnore]
        public bool HasUnsavedChanges { get; set; }

        #region commands
        [JsonIgnore]
        public AsyncRelayCommand LoadSettingsCommand { get; set; }
        [JsonIgnore]
        public AsyncRelayCommand SaveSettingsCommand { get; set; }

        #endregion

        public SettingsFormViewModel()
        {
            LoadSettingsCommand = new AsyncRelayCommand(Command_LoadSettings, () => { return true; });
            SaveSettingsCommand = new AsyncRelayCommand(Command_SaveSettings, () => { return !HasErrors; });

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
        }

        public static SettingsFormViewModel LoadDefault()
        {
            var target = Path.Combine(DefaultWorkingDirectory, DefaultSettingsFileName);
            if (File.Exists(target))
            {
                try
                {
                    return Load(target);
                }
                catch (Exception ex)
                {
                    Trace(TraceLoggerType.Settings,
                          TraceEventType.Error,
                          $"Failed to load default settings: {ex.Message}");
                }
            }
            return new SettingsFormViewModel();
        }

        private async Task Command_LoadSettings()
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Multiselect = false;

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            try
            {
                StateManager.Settings = Load(dialog.FileName);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Unable to load settings from {dialog.FileName}: {ex.Message}");
                return;
            }
            StateManager.ProgressState.InitializeProgress(1);
            StateManager.ProgressState.FinalizeProgress($"Successfully loaded settings from {dialog.FileName}");
        }

        private async Task Command_SaveSettings()
        {
            var sfd = new System.Windows.Forms.SaveFileDialog();
            sfd.Filter = "json files (*.json)|*.json";
            sfd.RestoreDirectory = true;

            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            Save(sfd.FileName);
        }

        public void Save(string? Target)
        {
            string? target = Target;
            if (string.IsNullOrEmpty(target))
            {
                target = Path.Combine(DefaultWorkingDirectory, DefaultSettingsFileName);
            }
            try
            {
                SaveInternal(target);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Unable to save settings: {ex.Message}");
                return;
            }
            StateManager.ProgressState.InitializeProgress(1);
            StateManager.ProgressState.FinalizeProgress($"Successfully saved settings to {target}");
        }

        private void SaveInternal(string Target)
        {
            string json;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Formatting = Formatting.Indented,
                };
                json = JsonConvert.SerializeObject(this, settings);
                File.WriteAllText(Target, json);
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not serialize settings to JSON: {ex.Message}");
            }
        }

        private static SettingsFormViewModel Load(string Location)
        {
            if (!File.Exists(Location))
            {
                throw new Exception("File does not exist");
            }

            try
            {
                var json = File.ReadAllText(Location);
                return (SettingsFormViewModel)JsonConvert.DeserializeObject(
                    json, typeof(SettingsFormViewModel))!;
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not deserialize settings: {ex.Message}");
            }
        }
    }
}
