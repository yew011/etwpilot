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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;

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
                }
            }
        }

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
        }

        public void AddPredicate(ParsedEtwManifestEvent Event,
            ParsedEtwTemplateItem Field,
            NativeTraceConsumer.PAYLOAD_OPERATOR Operator,
            string Value)
        {
            var predicate = new PayloadFilterPredicateViewModel
            {
                Event = Event,
                Field = Field,
                Operator = Operator,
                FieldValue = Value
            };
            //
            // Bubble up all errors
            //
            predicate.ErrorsChanged += delegate (object? sender, DataErrorsChangedEventArgs args)
            {
                OnErrorsChanged(args.PropertyName!);
            };
            PayloadFilterPredicates.Add(predicate);
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

        }

        private List<ParsedEtwManifestEvent> _Events;
        public List<ParsedEtwManifestEvent> Events
        {
            get => _Events;
            set
            {
                ClearErrors(nameof(Events));
                if (value != null && value.Count > 0)
                {
                    if (IsEnable == null)
                    {
                        AddError(nameof(Events), $"Select enable or disable.");
                    }
                }
                else if (IsEnable.HasValue)
                {
                    AddError(nameof(Events),
                        $"When enable/disable is selected, select at least one event.");
                }

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
                ClearErrors(nameof(IsEnable));
                if (Events == null || Events.Count == 0)
                {
                    AddError(nameof(IsEnable), $"Select at least one event.");
                }
                if (_IsEnable != value)
                {
                    _IsEnable = value;
                    OnPropertyChanged("IsEnable");
                }
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


        private ObservableCollection<ParsedEtwManifestField>? _AnyKeywords;
        public ObservableCollection<ParsedEtwManifestField>? AnyKeywords
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

        private ObservableCollection<ParsedEtwManifestField>? _AllKeywords;
        public ObservableCollection<ParsedEtwManifestField>? AllKeywords
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
    }

    internal class StackwalkFilterViewModel : ViewModelBase
    {
        public StackwalkFilterViewModel() 
        {
            Events = new List<ParsedEtwManifestEvent>();
            LevelKewyordFilterAnyKeywords = new List<ParsedEtwManifestField>();
            LevelKeywordFilterAllKeywords = new List<ParsedEtwManifestField>();
        }
        public List<ParsedEtwManifestEvent> Events { get; set; }
        public bool IsEnable { get; set; }
        public SourceLevels LevelKeywordFilterLevel { get; set; }
        public bool LevelKeywordFilterIsEnable { get; set; }
        public List<ParsedEtwManifestField> LevelKewyordFilterAnyKeywords { get; set; }
        public List<ParsedEtwManifestField> LevelKeywordFilterAllKeywords { get; set; }
    }

    internal class PayloadFilterPredicateViewModel : ViewModelBase
    {
        public PayloadFilterPredicateViewModel() {}
        public ParsedEtwManifestEvent? Event { get; set; }
        public ParsedEtwTemplateItem? Field { get; set; }
        public NativeTraceConsumer.PAYLOAD_OPERATOR Operator { get; set; }
        public string? FieldValue { get; set; }
    }

}