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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using static etwlib.NativeTraceConsumer;

namespace EtwPilot.ViewModel
{
    internal class ProviderFilterFormViewModel : ViewModelBase
    {
        public ParsedEtwManifest Manifest { get; set; }
        public ScopeFilterViewModel ScopeFilter { get; set; }
        public AttributeFilterViewModel AttributeFilter { get; set; }
        public StackwalkFilterViewModel StackwalkFilter { get; set; }
        public List<ParsedEtwManifestEvent> EventsWithTemplates { get; set; }
        public bool MatchAnyPredicate {get; set; }

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
        #endregion

        #region commands

        public AsyncRelayCommand AddPredicateCommand { get; set; }
        public AsyncRelayCommand UpdatePredicateCommand { get; set; }
        public AsyncRelayCommand CancelUpdatePredicateCommand { get; set; }
        public AsyncRelayCommand<IEnumerable<object>?> RemovePredicateCommand { get; set; }

        #endregion

        public ProviderFilterFormViewModel(ParsedEtwManifest Manifest2)
        {
            Manifest = Manifest2;
            ScopeFilter = new ScopeFilterViewModel();
            AttributeFilter = new AttributeFilterViewModel();
            StackwalkFilter = new StackwalkFilterViewModel();
            PayloadFilterPredicates = new ObservableCollection<PayloadFilterPredicateViewModel>();
            EventsWithTemplates = new List<ParsedEtwManifestEvent>();
            EventsWithTemplates.AddRange(Manifest2.Events.Where(
                e => !string.IsNullOrEmpty(e.Template)));
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

        private async Task Command_AddPredicate()
        {
            Debug.Assert(!SelectedPredicate.HasErrors);
            Debug.Assert(SelectedPredicate.Event != null);
            Debug.Assert(SelectedPredicate.Operator != null);
            Debug.Assert(SelectedPredicate.Field != null);
            Debug.Assert(SelectedPredicate.FieldValue != null);
            PayloadFilterPredicates.Add(SelectedPredicate);
            SelectedPredicate = new PayloadFilterPredicateViewModel();
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
    }

    internal class ScopeFilterViewModel : ViewModelBase
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
    }

    internal class AttributeFilterViewModel : ViewModelBase
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

    internal class StackwalkFilterViewModel : ViewModelBase
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

    internal class PayloadFilterPredicateViewModel : ViewModelBase
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
    }

}