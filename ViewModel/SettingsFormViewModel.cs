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
using CommunityToolkit.Mvvm.Input;
using EtwPilot.Model;
using Microsoft.Win32;
using Newtonsoft.Json;
using EtwPilot.Utilities;
using System.ComponentModel;
using System.Windows;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;

    public class SettingsFormViewModel : ViewModelBase
    {
        public static readonly string DefaultWorkingDirectory = Path.Combine(
            new string[] { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "etwpilot"});
        public static readonly string DefaultSettingsFileName = "settings.json";

        #region default formatters
        private static readonly Guid s_Decimal2HexFormatterId = new Guid("7ca43fd3-972c-4e44-878d-812a9eec1888");
        private static readonly Guid s_StackwalkAddressesToStringFormatterId = new Guid("bf5259b7-a1db-40bb-b099-d3046340d95b");

        [JsonIgnore]
        public List<Formatter> m_DefaultFormatters = new List<Formatter>() {
            new Formatter() {
                Id = s_Decimal2HexFormatterId,
                Namespace = FormatterLibrary.GetGuidBasedName(s_Decimal2HexFormatterId, "Formatters"),
                ClassName = FormatterLibrary.GetGuidBasedName(s_Decimal2HexFormatterId, "Class"),
                FunctionName="Decimal2Hex",
                Body="""
                    if (Args.Length != 3)
                    {
                        throw new Exception($"Invalid argument length: Got {Args.Length}, expected 3");
                    }
                    var evt = Args[0] as ParsedEtwEvent;
                    var contents = Args[1] as string;
                    if (evt == null || string.IsNullOrEmpty(contents))
                    {
                        Trace(TraceLoggerType.AsyncFormatter, TraceEventType.Verbose, $"Decimal2Hex: returning null");
                        return "";
                    }
                    var val = System.Convert.ToInt64(contents);
                    Trace(TraceLoggerType.AsyncFormatter, TraceEventType.Verbose, $"Decimal2Hex: returning 0x{val:X}");
                    return $"0x{val:X}";
                    """
            },
            new Formatter() {
                Id = s_StackwalkAddressesToStringFormatterId,
                Namespace = FormatterLibrary.GetGuidBasedName(s_StackwalkAddressesToStringFormatterId, "Formatters"),
                ClassName = FormatterLibrary.GetGuidBasedName(s_StackwalkAddressesToStringFormatterId, "Class"),
                FunctionName="StackwalkAddressesToString",
                Body="""
                    if (Args.Length != 3)
                    {
                        throw new Exception($"Invalid argument length: Got {Args.Length}, expected 3");
                    }
                    var evt = Args[0] as ParsedEtwEvent;
                    var contents = Args[1] as string;
                    var stackwalkHelper = Args[2] as StackwalkHelper;
                    if (evt == null || string.IsNullOrEmpty(contents) || stackwalkHelper == null)
                    {
                        Trace(TraceLoggerType.AsyncFormatter, TraceEventType.Verbose,
                            $"StackwalkAddressesToString: returning null");
                        return "";
                    }
                    var addresses = evt.StackwalkAddresses;
                    if (addresses == null || addresses.Count == 0)
                    {
                        Trace(TraceLoggerType.AsyncFormatter, TraceEventType.Verbose, 
                            $"StackwalkAddressesToString: no stackwalk addresses, returning null");
                        return "";
                    }
                    var sb = new StringBuilder();
                    await stackwalkHelper.ResolveAddresses((int)evt.ProcessId, addresses, sb);
                    Trace(TraceLoggerType.AsyncFormatter, TraceEventType.Verbose,
                        $"StackwalkAddressesToString: returning {sb.ToString()}");
                    return sb.ToString();
                    """
            },
        };

        #endregion

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
                    ClearErrors(nameof(DbghelpPath));
                    if (string.IsNullOrEmpty(value) || !File.Exists(value))
                    {
                        AddError(nameof(DbghelpPath), "DbghelpPath is null or invalid");
                    }
                    else
                    {
                        OnPropertyChanged("DbghelpPath");
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
                    ClearErrors(nameof(SymbolPath));
                    if (string.IsNullOrEmpty(value))
                    {
                        AddError(nameof(SymbolPath), "Symbol path is null");
                    }
                    else
                    {
                        OnPropertyChanged("SymbolPath");
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
                    if (!string.IsNullOrEmpty(value) && !Path.Exists(value))
                    {
                        AddError(nameof(ProviderCacheLocation), "Provider cache location is invalid");
                    }
                }
            }
        }

        private string _QdrantHostUri;
        public string QdrantHostUri
        {
            get => _QdrantHostUri;
            set
            {
                if (_QdrantHostUri != value)
                {
                    _QdrantHostUri = value;
                    OnPropertyChanged("QdrantHostUri");
                }
            }
        }

        private PromptExecutionSettingsDto _promptExecutionSettingsDto;
        [JsonIgnore]
        public PromptExecutionSettingsDto PromptExecutionSettingsDto
        {
            get => _promptExecutionSettingsDto;
            set
            {
                if (_promptExecutionSettingsDto != value)
                {
                    _promptExecutionSettingsDto = value;
                    OnPropertyChanged("PromptExecutionSettingsDto");
                }
            }
        }

        private OnnxGenAIConfigModel? _OnnxGenAIConfig;
        public OnnxGenAIConfigModel? OnnxGenAIConfig
        {
            get => _OnnxGenAIConfig;
            set
            {
                if (_OnnxGenAIConfig != null)
                {
                    UnsubscribeFromChildErrors(_OnnxGenAIConfig);
                }
                if (value != null)
                {
                    _OnnxGenAIConfig = value;
                    ClearErrors(nameof(OnnxGenAIConfig));
                    SubscribeToChildErrors(value, nameof(OnnxGenAIConfig));
                    OperationInProgressVisibility = Visibility.Hidden;
                    OperationInProgressMessage = "";
                }
                _OnnxGenAIConfig = value;
                OnPropertyChanged(nameof(OnnxGenAIConfig));
            }
        }

        private OllamaConfigModel? _OllamaConfig;
        public OllamaConfigModel? OllamaConfig
        {
            get => _OllamaConfig;
            set
            {
                if (_OllamaConfig != null)
                {
                    UnsubscribeFromChildErrors(_OllamaConfig);
                }
                if (value != null)
                {
                    ClearErrors(nameof(OllamaConfig));
                    SubscribeToChildErrors(value, nameof(OllamaConfig));
                    OperationInProgressVisibility = Visibility.Hidden;
                    OperationInProgressMessage = "";
                }
                _OllamaConfig = value;
                OnPropertyChanged(nameof(OllamaConfig));
            }
        }

        private bool _Valid;
        [JsonIgnore] // only used for UI
        public bool Valid
        {
            get => _Valid;
            set
            {
                if (_Valid != value)
                {
                    _Valid = value;
                    OnPropertyChanged("Valid");
                }
            }
        }

        private Visibility _OperationInProgressVisibility;
        [JsonIgnore] // UI only
        public Visibility OperationInProgressVisibility
        {
            get => _OperationInProgressVisibility;
            set
            {
                _OperationInProgressVisibility = value;
                OnPropertyChanged(nameof(OperationInProgressVisibility));
            }
        }

        private string _OperationInProgressMessage;
        [JsonIgnore] // UI only
        public string OperationInProgressMessage
        {
            get => _OperationInProgressMessage;
            set
            {
                _OperationInProgressMessage = value;
                OnPropertyChanged(nameof(OperationInProgressMessage));
            }
        }

        #endregion

        #region commands
        [JsonIgnore]
        public RelayCommand LoadSettingsCommand { get; set; }
        [JsonIgnore]
        public RelayCommand SaveSettingsCommand { get; set; }
        [JsonIgnore]
        public RelayCommand<Formatter> RemoveFormatterCommand { get; set; }
        [JsonIgnore]
        public RelayCommand<Formatter> AddFormatterCommand { get; set; }
        [JsonIgnore]
        public RelayCommand<Formatter> UpdateFormatterCommand { get; set; }
        [JsonIgnore]
        public RelayCommand AddDefaultFormattersCommand { get; set; }

        public ObservableCollection<Formatter> Formatters { get; set; }

        #endregion

        //
        // The way we notify VMs that their settings changed is to keep a list of properties
        // that changed in the Settings object while the form was open - when it closes,
        // inside MainWindowView code-behind, we send the list to the VM's
        // SettingsChanged_Command command. The reason we don't use INotifyPropertyChanged
        // directly is that this would cause the VM to re-evaluate or re-initialize every
        // time a single property changed, which makes no sense.
        //
        // Note: optional properties that are null'ed do make the list - but such properties
        // that are assigned an invalid value do NOT make this list and do NOT persist to
        // the settings file.
        //
        [JsonIgnore]
        public List<string> ChangedProperties;

        //
        // This formatter object is only used for validating the settings form.
        // Formatters are copied into live sessions because they contain a compiled
        // assembly that must be readonly.
        //
        [JsonIgnore]
        public FormatterLibrary m_FormatterLibrary { get; private set; }

        public SettingsFormViewModel() : base()
        {
            LoadSettingsCommand = new RelayCommand(Command_LoadSettings, () => { return true; });
            SaveSettingsCommand = new RelayCommand(Command_SaveSettings, () => { return !HasErrors; });
            RemoveFormatterCommand = new RelayCommand<Formatter>(
                Command_RemoveFormatter, _ => { return true; });
            AddFormatterCommand = new RelayCommand<Formatter>(
                Command_AddFormatter, _ => { return true; });
            UpdateFormatterCommand = new RelayCommand<Formatter>(
                Command_UpdateFormatter, _ => { return true; });
            AddDefaultFormattersCommand = new RelayCommand(
                Command_AddDefaultFormatters, () => { return true; });
            
            ChangedProperties = new List<string>();
            Formatters = new ObservableCollection<Formatter>();
            m_FormatterLibrary = new FormatterLibrary();
            OperationInProgressMessage = "";
            OperationInProgressVisibility = Visibility.Hidden;

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
            Valid = true;
            PromptExecutionSettingsDto = new PromptExecutionSettingsDto();

            ErrorsChanged += (object? sender, DataErrorsChangedEventArgs e) =>
            {
                //
                // If this is an error change on a property in a child, update our global
                // error on that child
                //
                var child = GetChild(e.PropertyName);
                if (child != null)
                {
                    if (child == OllamaConfig)
                    {
                        if (!OllamaConfig!.HasErrors)
                        {
                            ClearErrors(nameof(OllamaConfig));
                        }
                    }
                    else if (child == OnnxGenAIConfig)
                    {
                        if (!OnnxGenAIConfig!.HasErrors)
                        {
                            ClearErrors(nameof(OnnxGenAIConfig));
                        }
                    }
                }

                //
                // This is only used to update UI
                //
                Valid = !HasErrors;
            };
        }

        public void InitializeFromCodeBehind()
        {
            //
            // We don't do this in ctor because init of class members would make tracking
            // field changes useless, because there would always be a change due to ctor.
            //
            PropertyChanged += (obj, args) =>
            {
                if (!string.IsNullOrEmpty(args.PropertyName) &&
                    !ChangedProperties.Contains(args.PropertyName))
                {
                    ChangedProperties.Add(args.PropertyName);
                }
                LoadSettingsCommand.NotifyCanExecuteChanged();
                SaveSettingsCommand.NotifyCanExecuteChanged();
            };

            Formatters.CollectionChanged += (sender, e) =>
            {
                if (!ChangedProperties.Contains(nameof(Formatters)))
                {
                    ChangedProperties.Add(nameof(Formatters));
                }
            };
        }

        public static SettingsFormViewModel LoadDefault()
        {
            //
            // Important: This settings load path is _only_ invoked from the GlobalStateViewModel
            // singleton on app startup. There are no listeners at this point, so no need to
            // report this via the SettingsChanged command for VMs. Validation is also NOT
            // performed in this path, as GlobalStateViewModel instance has not been constructed yet.
            //

            var target = Path.Combine(DefaultWorkingDirectory, DefaultSettingsFileName);
            if (File.Exists(target))
            {
                try
                {
                    return Load(target, Validate:false);
                }
                catch (Exception ex)
                {
                    Trace(TraceLoggerType.Settings,
                          TraceEventType.Error,
                          $"Failed to load default settings: {ex.Message}");
                }
            }
            var newSettings = new SettingsFormViewModel();
            newSettings.AddDefaultFormattersCommand.Execute(null);
            return newSettings;
        }

        public async Task<bool> Validate()
        {
            //
            // This routine is invoked in two places:
            //      1) in this file, whenever a new SettingsFormViewModel object is created
            //      2) from GlobalStateViewModel:ApplySettingsChanges() when pending changes
            //      are about to be sent to all the VMs.
            //

            ClearErrors(nameof(Formatters));
            if (!await m_FormatterLibrary.Publish(Formatters.ToList()))
            {
                AddError(nameof(Formatters), "Failed to publish formatters");
            }

            //
            // Onnx or Ollama runtime must be valid.
            //
            ClearErrors(nameof(OnnxGenAIConfig));
            if (OnnxGenAIConfig != null && OnnxGenAIConfig.HasErrors)
            {
                AddError(nameof(OnnxGenAIConfig), "Invalid Onnx GenAI config");
            }
            ClearErrors(nameof(OllamaConfig));
            if (OllamaConfig != null)
            {
                if (!await OllamaConfig.Validate())
                {
                    AddError(nameof(OllamaConfig), "Invalid Ollama config");
                }
            }
            if (OnnxGenAIConfig == null && OllamaConfig == null)
            {
                AddError(nameof(OnnxGenAIConfig), "A chat completion runtime must be selected");
                AddError(nameof(OllamaConfig), "A chat completion runtime must be selected");
            }

            //
            // Most validation occurs in setters - when a value is changed to something
            // invalid, a form error is added.
            //
            return !HasErrors;
        }

        private void Command_LoadSettings()
        {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Multiselect = false;
            var result = dialog.ShowDialog();

            if (!result.HasValue || !result.Value)
            {
                return;
            }

            try
            {
                GlobalStateViewModel.Instance.Settings = Load(dialog.FileName);
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress(
                    $"Unable to load settings from {dialog.FileName}: {ex.Message}");
                return;
            }
            ProgressState.FinalizeProgress($"Successfully loaded settings from {dialog.FileName}");
        }

        private void Command_SaveSettings()
        {
            var sfd = new SaveFileDialog();
            sfd.Filter = "json files (*.json)|*.json";
            sfd.RestoreDirectory = true;
            var result = sfd.ShowDialog();

            if (!result.HasValue || !result.Value)
            {
                return;
            }

            Save(sfd.FileName);
        }

        private void Command_RemoveFormatter(Formatter? Formatter)
        {
            if (Formatter == null)
            {
                Debug.Assert(false);
                return;
            }
            Formatters.Remove(Formatter);
        }

        private void Command_AddFormatter(Formatter? Formatter)
        {
            if (Formatter == null)
            {
                Debug.Assert(false);
                return;
            }
            Formatters.Add(Formatter);
        }

        private void Command_AddDefaultFormatters()
        {
            if (!Formatters.ToList().Any(f => f.Id == s_Decimal2HexFormatterId))
            {
                AddFormatterCommand.Execute(m_DefaultFormatters[0]);
            }
            if (!Formatters.ToList().Any(f => f.Id == s_StackwalkAddressesToStringFormatterId))
            {
                AddFormatterCommand.Execute(m_DefaultFormatters[1]);
            }
        }

        private void Command_UpdateFormatter(Formatter? NewFormatter)
        {
            if (NewFormatter == null)
            {
                Debug.Assert(false);
                return;
            }
            //
            // Since we're updating the entry in place, the collectionChanged event
            // won't be raised. We have to manually note that this field was modified
            // so that it gets persisted and VMs get notified.
            //
            var errorPropertyName = NewFormatter.Id.ToString();
            if (!ChangedProperties.Contains(errorPropertyName))
            {
                ChangedProperties.Add(errorPropertyName);
            }
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
                ProgressState.FinalizeProgress($"Unable to save settings: {ex.Message}");
                return;
            }
            ProgressState.FinalizeProgress($"Successfully saved settings to {target}");
        }

        private void SaveInternal(string Target)
        {
            string json;
            try
            {
                //
                // Transfer DTO settings to OllamaConfig and OnnxGenAIConfig
                //
                if (OllamaConfig != null)
                {
                    OllamaConfig.PromptExecutionSettings = PromptExecutionSettingsDto.ToOllama();
                }
                if (OnnxGenAIConfig != null)
                {
                    OnnxGenAIConfig.PromptExecutionSettings = PromptExecutionSettingsDto.ToOnnx();
                }
                var settings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Formatting = Formatting.Indented,
                };
                json = JsonConvert.SerializeObject(this, settings);
                File.WriteAllText(Target, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not serialize settings to JSON: {ex.Message}");
            }
        }

        private static SettingsFormViewModel Load(string Location, bool Validate=true)
        {
            if (!File.Exists(Location))
            {
                throw new Exception("File does not exist");
            }

            try
            {
                var json = File.ReadAllText(Location);
                var settings = (SettingsFormViewModel)JsonConvert.DeserializeObject(
                    json, typeof(SettingsFormViewModel))!;
                //
                // Add a listener for any property change in these child classes.
                //
                if (settings.OnnxGenAIConfig != null)
                {
                    if (settings.OnnxGenAIConfig.PromptExecutionSettings != null)
                    {
                        settings.PromptExecutionSettingsDto =
                            PromptExecutionSettingsDto.FromOnnx(settings.OnnxGenAIConfig.PromptExecutionSettings);
                    }
                    settings.OnnxGenAIConfig.PropertyChanged += (obj, p) =>
                    {
                        if (!settings.ChangedProperties.Contains(p.PropertyName!))
                        {
                            settings.ChangedProperties.Add(p.PropertyName!);
                        }
                    };
                }
                if (settings.OllamaConfig != null)
                {
                    if (settings.OllamaConfig.PromptExecutionSettings != null)
                    {
                        settings.PromptExecutionSettingsDto =
                            PromptExecutionSettingsDto.FromOllama(settings.OllamaConfig.PromptExecutionSettings);
                    }
                    settings.OllamaConfig.PropertyChanged += (obj, p) =>
                    {
                        if (!settings.ChangedProperties.Contains(p.PropertyName!))
                        {
                            settings.ChangedProperties.Add(p.PropertyName!);
                        }
                    };
                }
                if (settings.Formatters.Count == 0)
                {
                    settings.AddDefaultFormattersCommand.Execute(null);
                }

                if (Validate)
                {
                    //
                    // Validate the deserialized object. Most form errors should have been
                    // populated at this point, but there are some checks that must be
                    // done after the settings object has been fully formed.
                    //
                    _ = settings.Validate();
                }
                return settings;
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not deserialize settings: {ex.Message}");
            }
        }
    }
}
