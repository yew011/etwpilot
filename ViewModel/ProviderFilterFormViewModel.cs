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
using CommunityToolkit.Mvvm.Input;
using etwlib;
using EtwPilot.Utilities.Converters;
using EtwPilot.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Text;
using static etwlib.NativeTraceConsumer;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;

    public class ProviderFilterFormViewModel : ViewModelBase
    {
        public Guid Id { get; set; }
        public ParsedEtwManifest Manifest { get; set; }
        public ScopeFilterViewModel ScopeFilter { get; set; }
        public AttributeFilterViewModel AttributeFilter { get; set; }
        public StackwalkFilterViewModel StackwalkFilter { get; set; }
        public List<ParsedEtwManifestEvent> EventsWithTemplates { get; set; }
        public bool MatchAnyPredicate {get; set; }
        private static readonly int s_MaxColCount = 20;

        #region default etw column defs
        private static readonly List<EtwColumnViewModel> s_DefaultEtwColumns = new List<EtwColumnViewModel>()
        {
            new EtwColumnViewModel()
            {
                Name = "Provider",
            },
            new EtwColumnViewModel()
            {
                Name = "EventId",
            },
            new EtwColumnViewModel()
            {
                Name = "Version",
            },
            new EtwColumnViewModel()
            {
                Name = "Level",
            },
            new EtwColumnViewModel()
            {
                Name = "Channel",
            },
            new EtwColumnViewModel()
            {
                Name = "Keywords",
            },
            new EtwColumnViewModel()
            {
                Name = "Task",
            },
            new EtwColumnViewModel()
            {
                Name = "Opcode",
            },
            new EtwColumnViewModel()
            {
                Name = "ProcessId",
                IConverterCode = IConverterCode.s_DecimalToHexIConverterCode,
            },
            new EtwColumnViewModel()
            {
                Name = "ThreadId",
                IConverterCode = IConverterCode.s_DecimalToHexIConverterCode,
            },
            new EtwColumnViewModel()
            {
                Name = "UserSid",
            },
            new EtwColumnViewModel()
            {
                Name = "ActivityId",
            },
            new EtwColumnViewModel()
            {
                Name = "Timestamp",
            },
        };
        #endregion

        #region observable properties
        private ObservableCollection<PayloadFilterPredicateViewModel> _PayloadFilterPredicates;
        public ObservableCollection<PayloadFilterPredicateViewModel> PayloadFilterPredicates
        {
            get => _PayloadFilterPredicates;
            set
            {
                if (_PayloadFilterPredicates != value)
                {
                    _PayloadFilterPredicates = value;
                    OnPropertyChanged("PayloadFilterPredicates");
                }
            }
        }

        private PayloadFilterPredicateViewModel _SelectedPredicate;
        public PayloadFilterPredicateViewModel SelectedPredicate
        {
            get => _SelectedPredicate;
            set
            {
                if (_SelectedPredicate != value)
                {
                    _SelectedPredicate = value;
                    OnPropertyChanged("SelectedPredicate");

                    //
                    // Subscribe to property change events in selected predicate form, so when the form becomes valid,
                    // the control buttons and associated commands are available.
                    //
                    _SelectedPredicate.PropertyChanged += (object? sender, PropertyChangedEventArgs Args) =>
                    {
                        AddPredicateCommand.NotifyCanExecuteChanged();
                        UpdatePredicateCommand.NotifyCanExecuteChanged();
                        CancelUpdatePredicateCommand.NotifyCanExecuteChanged();
                        RemovePredicateCommand.NotifyCanExecuteChanged();
                    };
                }
            }
        }

        private ObservableCollection<EtwColumnViewModel> _AvailableEtwColumns;
        public ObservableCollection<EtwColumnViewModel> AvailableEtwColumns
        {
            get => _AvailableEtwColumns;
            set
            {
                if (_AvailableEtwColumns != value)
                {
                    _AvailableEtwColumns = value;
                    OnPropertyChanged("AvailableEtwColumns");
                }
            }
        }

        private ObservableCollection<EtwColumnViewModel> _ChosenEtwColumns;
        public ObservableCollection<EtwColumnViewModel> ChosenEtwColumns
        {
            get => _ChosenEtwColumns;
            set
            {
                if (_ChosenEtwColumns != value)
                {
                    _ChosenEtwColumns = value;
                    OnPropertyChanged("ChosenEtwColumns");
                }
            }
        }

        private bool _EditingEtwColumn;
        public bool EditingEtwColumn
        {
            get => _EditingEtwColumn;
            set
            {
                if (_EditingEtwColumn != value)
                {
                    _EditingEtwColumn = value;
                    OnPropertyChanged("EditingEtwColumn");
                }
            }
        }
        #endregion

        #region commands

        //
        // Predicate filters
        //
        public AsyncRelayCommand AddPredicateCommand { get; set; }
        public AsyncRelayCommand UpdatePredicateCommand { get; set; }
        public AsyncRelayCommand CancelUpdatePredicateCommand { get; set; }
        public AsyncRelayCommand<IEnumerable<object>?> RemovePredicateCommand { get; set; }
        //
        // Chosen etw columns for display
        //
        public AsyncRelayCommand AddDefaultEtwColumnsCommand { get; set; }
        public AsyncRelayCommand<IEnumerable<object>?> AddEtwColumnsCommand { get; set; }
        public AsyncRelayCommand ClearEtwColumnCommand { get; set; }
        public AsyncRelayCommand<IEnumerable<object>?> RemoveEtwColumnCommand { get; set; }
        #endregion

        public ProviderFilterFormViewModel(ParsedEtwManifest Manifest2) : base()
        {
            Manifest = Manifest2;
            ScopeFilter = new ScopeFilterViewModel();
            AttributeFilter = new AttributeFilterViewModel();
            StackwalkFilter = new StackwalkFilterViewModel();

            //
            // Setup a list of available ETW events
            //
            EventsWithTemplates = new List<ParsedEtwManifestEvent>();
            EventsWithTemplates.AddRange(Manifest2.Events.Where(
                e => !string.IsNullOrEmpty(e.Template)));

            //
            // Bubble up any errors in sub-forms to parent form
            //
            ScopeFilter.SetParentFormNotifyErrorsChanged(this);
            AttributeFilter.SetParentFormNotifyErrorsChanged(this);
            StackwalkFilter.SetParentFormNotifyErrorsChanged(this);

            //
            // There are two column lists - one for available columns which changes as other
            // parts of the form are completed, repopulated whenever the tab is activated -
            // and one for the chosen columns which can be edited by the user.
            //
            AvailableEtwColumns = new ObservableCollection<EtwColumnViewModel>();
            ChosenEtwColumns = new ObservableCollection<EtwColumnViewModel>();
            ChosenEtwColumns.CollectionChanged += ChosenEtwColumns_CollectionChanged;
            AddEtwColumnsCommand = new AsyncRelayCommand<IEnumerable<object>?>(
                Command_AddEtwColumns, (columns) => { return columns != null && columns.Count() > 0 && !EditingEtwColumn; });
            ClearEtwColumnCommand = new AsyncRelayCommand(
                Command_ClearEtwColumns, () => { return !EditingEtwColumn; });
            RemoveEtwColumnCommand = new AsyncRelayCommand<IEnumerable<object>?>(
                Command_RemoveEtwColumn, (columns) => { return columns != null && columns.Count() > 0 && !EditingEtwColumn; });
            AddDefaultEtwColumnsCommand = new AsyncRelayCommand(
                Command_AddDefaultEtwColumns, () => { return !EditingEtwColumn; });

            //
            // Payload filter predicates utilize a command model and a "selected" predicate
            // to allow updating a previously defined predicate.
            //
            PayloadFilterPredicates = new ObservableCollection<PayloadFilterPredicateViewModel>();
            SelectedPredicate = new PayloadFilterPredicateViewModel();
            AddPredicateCommand = new AsyncRelayCommand(
                Command_AddPredicate, () => { return !SelectedPredicate.HasErrors && !SelectedPredicate.IsUpdateMode; });
            UpdatePredicateCommand = new AsyncRelayCommand(
                Command_UpdatePredicate, () => { return !SelectedPredicate.HasErrors && SelectedPredicate.IsUpdateMode; });
            CancelUpdatePredicateCommand = new AsyncRelayCommand(
                Command_CancelUpdatePredicate, () => { return !SelectedPredicate.HasErrors && SelectedPredicate.IsUpdateMode; });
            RemovePredicateCommand = new AsyncRelayCommand<IEnumerable<object>?>(
                Command_RemovePredicate, (predicates) => { return predicates != null && predicates.Count() > 0; });
        }

        public void Initialize()
        {
            //
            // When the form loads, the user has not had an opportunity to select ETW columns
            // yet, and this is required in the form.
            //
            AddError(nameof(ChosenEtwColumns), $"Select at least one column");
            SetInitialChosenEtwColumns();
        }

        public void FinalizeForm()
        {
            //
            // When the form unloads, it's important to clear all outstanding form errors,
            // since our parent form would otherwise retain them and the next sub-form
            // would appear to have errors.
            //
            ClearErrors();
        }

        protected override Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
        {
            throw new NotImplementedException();
        }

        private void ChosenEtwColumns_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ClearErrors(nameof(ChosenEtwColumns));
            if (ChosenEtwColumns.Count == 0)
            {
                AddError(nameof(ChosenEtwColumns), $"Select at least one column");
            }
            else if (ChosenEtwColumns.Count > s_MaxColCount)
            {
                AddError(nameof(ChosenEtwColumns), $"Too many columns (max={s_MaxColCount})");
            }
        }

        private void SetInitialChosenEtwColumns()
        {
            if (!GlobalStateViewModel.Instance.Settings.UseDefaultEtwColumns)
            {
                return;
            }
            AddDefaultColumnsToChosenEtwColumns();
        }

        public void SetAvailableEtwColumnsFromUniqueEvents(List<ParsedEtwManifestEvent> Events)
        {
            //
            // This routine is called from the corresponding view's code-behind
            // when the "choose ETW columns" tab is loaded. Its purpose is to
            // populate our view model's AvailableEtwColumns list to allow the
            // user to select what ETW columns should be displayed during a
            // LiveSession. This function could be implemented as a command but
            // because the caller already has our VM instance, it seemed overkill.
            //
            // The input events are the unique set of events selected across all
            // filter tabs (scope, attribute, stackwalk, predicate). This routine
            // creates an ETW display column for all default columns and each
            // template field defined in any event with templates. If no events
            // are selected, the user will have a display column for all unique
            // fields across all templates, plus the defaults.
            //
            AvailableEtwColumns.Clear();
            s_DefaultEtwColumns.ForEach(dc =>
            {
                if (!AvailableEtwColumns.Any(c => c.Name == dc.Name))
                {
                    AvailableEtwColumns.Add(new EtwColumnViewModel()
                    {
                        Name = dc.Name,
                        UniqueName = dc.Name,
                        IConverterCode = dc.IConverterCode,
                    });
                }
            });

            foreach (var _event in Events)
            {
                if (string.IsNullOrEmpty(_event.Template))
                {
                    continue;
                }

                //
                // Fetch the template field names from the corresponding provider manifest
                //
                if (!Manifest.Templates.ContainsKey(_event.Template))
                {
                    Debug.Assert(false);
                    Trace(TraceLoggerType.Sessions,
                          TraceEventType.Error,
                          $"Unable to locate template named {_event.Template} for provider {Manifest.Provider}");
                    continue;
                }
                var template = Manifest.Templates[_event.Template];
                foreach (var field in template)
                {
                    var uniqueName = $"{_event.Template}.{field.Name}";
                    if (AvailableEtwColumns.Any(c => c.UniqueName == uniqueName))
                    {
                        Debug.Assert(false);
                        Trace(TraceLoggerType.Sessions,
                              TraceEventType.Error,
                              $"Column named {uniqueName} already exists!");
                        continue;
                    }
                    
                    AvailableEtwColumns.Add(new EtwColumnViewModel()
                    {
                        Name = field.Name,
                        UniqueName = uniqueName,
                        TemplateSourceEvent = _event.Id,
                        TemplateFieldType = $"{field.InType}",
                    });
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Provider {Manifest.Provider}:");
            sb.AppendLine($"   Match any predicate: {MatchAnyPredicate}");
            var scopeFilterStr = $"{ScopeFilter}";
            var attribFilterStr = $"{AttributeFilter}";
            var stackwalkFilterStr = $"{StackwalkFilter}";

            if (!string.IsNullOrEmpty(scopeFilterStr))
            {
                sb.AppendLine($"{scopeFilterStr}");
            }
            if (!string.IsNullOrEmpty(attribFilterStr))
            {
                sb.AppendLine($"{attribFilterStr}");
            }
            if (!string.IsNullOrEmpty(stackwalkFilterStr))
            {
                sb.AppendLine($"{stackwalkFilterStr}");
            }
            if (PayloadFilterPredicates.Count > 0)
            {
                PayloadFilterPredicates.ToList().ForEach(p => sb.AppendLine($"{p}"));
            }
            if (ChosenEtwColumns.Count > 0)
            {
                ChosenEtwColumns.ToList().ForEach(c => sb.AppendLine($"{c}"));
            }
            return sb.ToString();
        }

        private void AddDefaultColumnsToChosenEtwColumns()
        {
            s_DefaultEtwColumns.ForEach(dc =>
            {
                if (!ChosenEtwColumns.Any(c => c.Name == dc.Name))
                {
                    var newCol = new EtwColumnViewModel();
                    //
                    // Bubble up any errors to parent form
                    //
                    newCol.SetParentFormNotifyErrorsChanged(this);
                    //
                    // Set the fields/validate
                    //
                    newCol.Name = dc.Name;
                    newCol.UniqueName = dc.Name;
                    newCol.IConverterCode = dc.IConverterCode;
                    ChosenEtwColumns.Add(newCol);
                }
            });
        }

        #region command processing

        private async Task Command_AddPredicate()
        {
            Debug.Assert(!SelectedPredicate.HasErrors);
            Debug.Assert(SelectedPredicate.Event != null);
            Debug.Assert(SelectedPredicate.Operator != null);
            Debug.Assert(SelectedPredicate.Field != null);
            Debug.Assert(SelectedPredicate.FieldValue != null);
            PayloadFilterPredicates.Add(SelectedPredicate);
            SelectedPredicate = new PayloadFilterPredicateViewModel();
            //
            // Bubble up any errors in sub-forms to parent form
            //
            SelectedPredicate.SetParentFormNotifyErrorsChanged(this);
        }

        private async Task Command_UpdatePredicate()
        {
            Debug.Assert(SelectedPredicate.IsUpdateMode);
            SelectedPredicate.IsUpdateMode = false;
            SelectedPredicate = new PayloadFilterPredicateViewModel();
        }

        private async Task Command_CancelUpdatePredicate()
        {
            Debug.Assert(SelectedPredicate.IsUpdateMode);
            SelectedPredicate.IsUpdateMode = false;
            SelectedPredicate = new PayloadFilterPredicateViewModel();
        }

        private async Task Command_RemovePredicate(IEnumerable<object> Predicates)
        {
            Debug.Assert(Predicates != null && Predicates.Count() > 0);
            var items = Predicates.OfType<PayloadFilterPredicateViewModel>().ToList();
            items.ForEach(s => PayloadFilterPredicates.Remove(s));
            SelectedPredicate = new PayloadFilterPredicateViewModel();
        }

        private async Task Command_AddDefaultEtwColumns()
        {
            AddDefaultColumnsToChosenEtwColumns();
        }

        private async Task Command_ClearEtwColumns()
        {
            ChosenEtwColumns.Clear();
        }

        private async Task Command_AddEtwColumns(IEnumerable<object> Columns)
        {
            Debug.Assert(Columns != null && Columns.Count() > 0);
            var items = Columns.OfType<EtwColumnViewModel>().ToList();
            items.ForEach(cc =>
            {
                if (!ChosenEtwColumns.Any(c => c.Name == cc.Name))
                {
                    var newCol = new EtwColumnViewModel();
                    //
                    // Bubble up any errors to parent form
                    //
                    newCol.SetParentFormNotifyErrorsChanged(this);
                    //
                    // Set the fields/validate
                    //
                    newCol.Name = cc.Name;
                    newCol.IConverterCode = cc.IConverterCode;
                    ChosenEtwColumns.Add(newCol);
                }
            });
        }

        private async Task Command_RemoveEtwColumn(IEnumerable<object> Columns)
        {
            Debug.Assert(Columns != null && Columns.Count() > 0);
            var items = Columns.OfType<EtwColumnViewModel>().ToList();
            items.ForEach(c => ChosenEtwColumns.Remove(c));
        }

        #endregion
    }

    public class ScopeFilterViewModel : NotifyPropertyAndErrorInfoBase
    {
        public ScopeFilterViewModel()
        {
            Processes = new List<ProcessObject>();
            ExeNames = new List<string>();
            AppIds = new List<string>();
            PackageIds = new List<string>();
        }
        public List<ProcessObject> Processes { get; set; }
        public List<string> ExeNames { get; set; }
        public List<string> AppIds { get; set; }
        public List<string> PackageIds { get; set; }

        public static string GetEtwString(List<string> Values)
        {
            var exes = string.Join(';', Values);
            var length = (exes.Length + 1) * 2;
            if (length > 1024)
            {
                //
                // ETW filtering allows a maximum length of 1024 bytes for these parameters
                // This should already have been validated by the form validation.
                //
                Debug.Assert(false);
            }
            return exes;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var header = $"   Scope filter:{Environment.NewLine}";
            if (Processes.Count > 0)
            {
                sb.AppendLine($"     Pids={string.Join(",", Processes.Select(p => p.Pid).ToList())}");
            }
            if (ExeNames.Count > 0)
            {
                sb.AppendLine($"     ExeNames={string.Join(",", ExeNames.ToList())}");
            }
            if (AppIds.Count > 0)
            {
                sb.AppendLine($"     AppIds={string.Join(",", AppIds.ToList())}");
            }
            if (PackageIds.Count > 0)
            {
                sb.AppendLine($"     PackageIds={string.Join(",", PackageIds.ToList())}");
            }
            if (sb.Length > 0)
            {
                sb.Insert(0, header);
                return sb.ToString();
            }
            return string.Empty;
        }
    }

    public class AttributeFilterViewModel : NotifyPropertyAndErrorInfoBase
    {
        public AttributeFilterViewModel()
        {
            Events = new ObservableCollection<ParsedEtwManifestEvent>();
            Events.CollectionChanged += (sender, args) =>
            {
                switch (args.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        {
                            ValidateEvents(nameof(Events));
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            };

            AnyKeywords = new ObservableCollection<ParsedEtwManifestField>();
            AnyKeywords.CollectionChanged += (sender, args) =>
            {
                switch (args.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        {
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            };

            AllKeywords = new ObservableCollection<ParsedEtwManifestField>();
            AllKeywords.CollectionChanged += (sender, args) =>
            {
                switch (args.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        {
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var header = $"   Attribute filter:{Environment.NewLine}";
            if (Events.Count > 0)
            {
                sb.AppendLine($"     EventIds={string.Join(
                    ",", Events.Select(e => e.Id).ToList())}({IsEnable})");
            }
            if (Level != SourceLevels.Off)
            {
                sb.AppendLine($"     Level={Level}");
            }
            if (AnyKeywords.Count > 0)
            {
                ulong anyKeywords = 0;
                AnyKeywords.ToList().ForEach(k => { anyKeywords |= k.Value; });
                sb.AppendLine($"     AnyKeywords=0x{anyKeywords:X}");
            }
            if (AllKeywords.Count > 0)
            {
                ulong allKeywords = 0;
                AllKeywords.ToList().ForEach(k => { allKeywords |= k.Value; });
                sb.AppendLine($"     AllKeywords=0x{allKeywords:X}");
            }
            if (sb.Length > 0)
            {
                sb.Insert(0, header);
                return sb.ToString();
            }
            return string.Empty;
        }

        private ObservableCollection<ParsedEtwManifestEvent> _Events;
        public ObservableCollection<ParsedEtwManifestEvent> Events
        {
            get => _Events;
            set
            {
                if (_Events != value)
                {
                    _Events = value;
                    OnPropertyChanged("Events");
                }
            }
        }

        private bool? _IsEnable;
        public bool? IsEnable
        {
            get => _IsEnable;
            set
            {
                _IsEnable = value;
                ValidateEvents(nameof(IsEnable));
                OnPropertyChanged("IsEnable");
            }
        }

        private SourceLevels _Level;
        public SourceLevels Level
        {
            get => _Level;
            set
            {
                if (_Level != value)
                {
                    _Level = value;
                    OnPropertyChanged("Level");
                }
            }
        }

        private ObservableCollection<ParsedEtwManifestField> _AnyKeywords;
        public ObservableCollection<ParsedEtwManifestField> AnyKeywords
        {
            get => _AnyKeywords;
            set
            {
                if (_AnyKeywords != value)
                {
                    _AnyKeywords = value;
                    OnPropertyChanged("AnyKeywords");
                }
            }
        }

        private ObservableCollection<ParsedEtwManifestField> _AllKeywords;
        public ObservableCollection<ParsedEtwManifestField> AllKeywords
        {
            get => _AllKeywords;
            set
            {
                if (_AllKeywords != value)
                {
                    _AllKeywords = value;
                    OnPropertyChanged("AllKeywords");
                }
            }
        }

        private void ValidateEvents(string PropertyName)
        {
            ClearErrors(nameof(Events));
            ClearErrors(nameof(IsEnable));
            if (Events.Count > 0)
            {
                if (IsEnable == null || !IsEnable.HasValue)
                {
                    AddError(PropertyName, $"Select enable or disable.");
                }
            }
            else if (IsEnable != null && IsEnable.HasValue)
            {
                AddError(PropertyName,
                    $"When enable/disable is selected, select at least one event.");
            }
        }
    }

    public class StackwalkFilterViewModel : NotifyPropertyAndErrorInfoBase
    {
        public StackwalkFilterViewModel() 
        {
            Events = new ObservableCollection<ParsedEtwManifestEvent>();
            Events.CollectionChanged += (sender, args) =>
            {
                switch (args.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        {
                            ValidateEvents(nameof(Events));
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            };

            LevelKeywordFilterAnyKeywords = new ObservableCollection<ParsedEtwManifestField>();
            LevelKeywordFilterAnyKeywords.CollectionChanged += (sender, args) =>
            {
                switch (args.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        {
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            };

            LevelKeywordFilterAllKeywords = new ObservableCollection<ParsedEtwManifestField>();
            LevelKeywordFilterAllKeywords.CollectionChanged += (sender, args) =>
            {
                switch (args.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        {
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var header = $"   Stackwalk filter:{Environment.NewLine}";
            if (Events.Count > 0)
            {
                sb.AppendLine($"     EventIds={string.Join(
                    ",", Events.Select(e => e.Id).ToList())}({IsEnable})");
            }
            if (LevelKeywordFilterLevel != SourceLevels.Off)
            {
                sb.AppendLine($"     LevelKeywordFilterLevel={LevelKeywordFilterLevel}");
            }
            if (LevelKeywordFilterAnyKeywords.Count > 0)
            {
                ulong anyKeywords = 0;
                LevelKeywordFilterAnyKeywords.ToList().ForEach(k => { anyKeywords |= k.Value; });
                sb.AppendLine($"     LevelAnyKeywords=0x{anyKeywords:X}");
            }
            if (LevelKeywordFilterAllKeywords.Count > 0)
            {
                ulong allKeywords = 0;
                LevelKeywordFilterAllKeywords.ToList().ForEach(k => { allKeywords |= k.Value; });
                sb.AppendLine($"     LevelAllKeywords=0x{allKeywords:X}");
            }
            if (sb.Length > 0)
            {
                sb.Insert(0, header);
                return sb.ToString();
            }
            return string.Empty;
        }

        private ObservableCollection<ParsedEtwManifestEvent> _Events;
        public ObservableCollection<ParsedEtwManifestEvent> Events
        {
            get => _Events;
            set
            {
                if (_Events != value)
                {
                    _Events = value;
                    OnPropertyChanged("Events");
                }
            }
        }

        private bool? _IsEnable;
        public bool? IsEnable
        {
            get => _IsEnable;
            set
            {
                _IsEnable = value;
                ValidateEvents(nameof(IsEnable));
                OnPropertyChanged("IsEnable");
            }
        }

        private SourceLevels _LevelKeywordFilterLevel;
        public SourceLevels LevelKeywordFilterLevel
        {
            get => _LevelKeywordFilterLevel;
            set
            {
                _LevelKeywordFilterLevel = value;
                ValidateLevel(nameof(LevelKeywordFilterLevel));
                OnPropertyChanged("LevelKeywordFilterLevel");
            }
        }

        private bool? _LevelKeywordFilterIsEnable;
        public bool? LevelKeywordFilterIsEnable
        {
            get => _LevelKeywordFilterIsEnable;
            set
            {
                _LevelKeywordFilterIsEnable = value;
                ValidateLevel(nameof(LevelKeywordFilterIsEnable));
                OnPropertyChanged("LevelKeywordFilterIsEnable");
            }
        }

        private ObservableCollection<ParsedEtwManifestField> _LevelKeywordFilterAnyKeywords;
        public ObservableCollection<ParsedEtwManifestField> LevelKeywordFilterAnyKeywords
        {
            get => _LevelKeywordFilterAnyKeywords;
            set
            {
                if (_LevelKeywordFilterAnyKeywords != value)
                {
                    _LevelKeywordFilterAnyKeywords = value;
                    OnPropertyChanged("LevelKeywordFilterAnyKeywords");
                }
            }
        }

        private ObservableCollection<ParsedEtwManifestField> _LevelKeywordFilterAllKeywords;
        public ObservableCollection<ParsedEtwManifestField> LevelKeywordFilterAllKeywords
        {
            get => _LevelKeywordFilterAllKeywords;
            set
            {
                if (_LevelKeywordFilterAllKeywords != value)
                {
                    _LevelKeywordFilterAllKeywords = value;
                    OnPropertyChanged("LevelKeywordFilterAllKeywords");
                }
            }
        }

        private void ValidateEvents(string PropertyName)
        {
            ClearErrors(nameof(Events));
            ClearErrors(nameof(IsEnable));
            if (Events.Count > 0)
            {
                if (IsEnable == null || !IsEnable.HasValue)
                {
                    AddError(PropertyName, $"Select enable or disable.");
                }
            }
            else if (IsEnable != null && IsEnable.HasValue)
            {
                AddError(PropertyName,
                    $"When enable/disable is selected, select at least one event.");
            }
        }

        private void ValidateLevel(string PropertyName)
        {
            ClearErrors(nameof(LevelKeywordFilterLevel));
            ClearErrors(nameof(LevelKeywordFilterIsEnable));
            if (LevelKeywordFilterLevel != SourceLevels.Off)
            {
                if (LevelKeywordFilterIsEnable == null || !LevelKeywordFilterIsEnable.HasValue)
                {
                    AddError(PropertyName, $"Select enable or disable.");
                }
            }
            else if (LevelKeywordFilterIsEnable != null && LevelKeywordFilterIsEnable.HasValue)
            {
                AddError(PropertyName,
                    $"When enable/disable is selected, select a level.");
            }
        }
    }

    public class PayloadFilterPredicateViewModel : NotifyPropertyAndErrorInfoBase
    {
        public PayloadFilterPredicateViewModel() {
            Event = null; // force initial error state
        }

        private ParsedEtwManifestEvent _Event;
        public ParsedEtwManifestEvent Event
        {
            get => _Event;
            set
            {
                _Event = value;
                ValidatePredicate();
                OnPropertyChanged("Event");
            }
        }

        private ParsedEtwTemplateItem _Field;
        public ParsedEtwTemplateItem Field
        {
            get => _Field;
            set
            {
                _Field = value;
                ValidatePredicate();
                OnPropertyChanged("Field");
            }
        }

        private PAYLOAD_OPERATOR? _Operator;
        public PAYLOAD_OPERATOR? Operator
        {
            get => _Operator;
            set
            {
                _Operator = value;
                ValidatePredicate();
                OnPropertyChanged("Operator");
            }
        }

        private string? _FieldValue;
        public string? FieldValue
        {
            get => _FieldValue;
            set
            {
                _FieldValue = value;
                ValidatePredicate();
                OnPropertyChanged("FieldValue");
            }
        }

        private bool _IsUpdateMode;
        public bool IsUpdateMode
        {
            get => _IsUpdateMode;
            set
            {
                if (_IsUpdateMode != value)
                {
                    _IsUpdateMode = value;
                    OnPropertyChanged("IsUpdateMode");
                }
            }
        }

        private void ValidatePredicate()
        {
            ClearErrors(nameof(Field));
            ClearErrors(nameof(FieldValue));
            ClearErrors(nameof(Operator));
            if (Field == null)
            {
                AddError(nameof(Field), $"Select a field");
                return;
            }
            if (Operator == null || !Operator.HasValue)
            {
                AddError(nameof(Operator), $"Select an operator");
                return;
            }
            if (string.IsNullOrEmpty(FieldValue))
            {
                AddError(nameof(FieldValue), $"Specify a field value");
                return;
            }
            try
            {
                PayloadFilter.ValidatePredicate(Field.Name, Operator.Value, FieldValue);
            }
            catch (Exception ex)
            {
                AddError(nameof(Operator), ex.Message);
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"   Event: {Event.Id}:{Event.Version}");
            sb.AppendLine($"     {Field.Name} {Operator!.Value} {FieldValue}");
            return sb.ToString();
        }
    }

    public class EtwColumnViewModel : NotifyPropertyAndErrorInfoBase
    {
        #region observable properties

        private string _Name;
        public string Name
        {
            get => _Name;
            set
            {
                if (_Name != value)
                {
                    _Name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        private string _UniqueName;
        public string UniqueName
        {
            get => _UniqueName;
            set
            {
                if (_UniqueName != value)
                {
                    _UniqueName = value;
                    OnPropertyChanged("UniqueName");
                }
            }
        }

        private string _TemplateSourceEvent;
        public string TemplateSourceEvent
        {
            get => _TemplateSourceEvent;
            set
            {
                if (_TemplateSourceEvent != value)
                {
                    _TemplateSourceEvent = value;
                    OnPropertyChanged("TemplateSourceEvent");
                }
            }
        }

        private string _TemplateFieldType;
        public string TemplateFieldType
        {
            get => _TemplateFieldType;
            set
            {
                if (_TemplateFieldType != value)
                {
                    _TemplateFieldType = value;
                    OnPropertyChanged("TemplateFieldType");
                }
            }
        }

        private string _IConverterCode;
        public string IConverterCode
        {
            get => _IConverterCode;
            set
            {
                if (_IConverterCode != value)
                {
                    _IConverterCode = value;
                    OnPropertyChanged("IConverterCode");
                    ClearErrors(nameof(IConverterCode));
                    if (!string.IsNullOrEmpty(IConverterCode))
                    {
                        using (var library = GetIConverterLibrary())
                        {
                            var result = library!.TryCompile(out string err);
                            if (!result)
                            {
                                AddError(nameof(IConverterCode), err);
                            }
                        }
                    }
                }
            }
        }

        private bool _IsUpdateMode;
        public bool IsUpdateMode
        {
            get => _IsUpdateMode;
            set
            {
                if (_IsUpdateMode != value)
                {
                    _IsUpdateMode = value;
                    OnPropertyChanged("IsUpdateMode");
                }
            }
        }
        #endregion

        public EtwColumnViewModel()
        {
            IsUpdateMode = false;
        }

        public DynamicRuntimeLibrary GetIConverterLibrary()
        {
            return new DynamicRuntimeLibrary(
                "using System; using System.Windows.Data;",
                "EtwPilot.Utilities.Converters",
                "CustomConverter",
                "IValueConverter",
                IConverterCode);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}