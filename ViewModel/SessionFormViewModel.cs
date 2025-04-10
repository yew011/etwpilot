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
using EtwPilot.Model;
using etwlib;
using EtwPilot.Utilities;
using System.IO;
using static etwlib.NativeTraceConsumer;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Brushes = System.Windows.Media.Brushes;
using System.ComponentModel;
using Meziantou.Framework.WPF.Collections;

namespace EtwPilot.ViewModel
{
    using StopCondition = LiveSessionViewModel.StopCondition;

    public class SessionFormViewModel : ViewModelBase
    {
        #region observable properties - input form

        private string _Name;
        public string Name
        {
            get => _Name;
            set
            {
                ClearErrors(nameof(Name));
                if (string.IsNullOrEmpty(value))
                {
                    AddError(nameof(Name), $"Session name cannot be empty.");
                }
                if (_Name != value)
                {
                    _Name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        private bool _IsRealTime;
        public bool IsRealTime
        {
            get => _IsRealTime;
            set
            {
                if (_IsRealTime != value)
                {
                    _IsRealTime = value;
                    //
                    // When real-time checkbox is checked, the log location
                    // should be cleared, as they are mutually-exclusive.
                    //
                    LogLocation = string.Empty;
                    OnPropertyChanged("IsRealTime");
                }
            }
        }

        private string? _LogLocation;
        public string? LogLocation
        {
            get => _LogLocation;
            set
            {
                ClearErrors(nameof(LogLocation));
                if (!string.IsNullOrEmpty(value) && !Directory.Exists(value))
                {
                    AddError(nameof(LogLocation), $"Log location {value} does not exist.");
                }
                if (_LogLocation != value)
                {
                    _LogLocation = value;
                    OnPropertyChanged("LogLocation");
                }
            }
        }

        private StopCondition _StopCondition;
        public StopCondition StopCondition
        {
            get => _StopCondition;
            set
            {
                ClearErrors(nameof(StopCondition));
                switch (value)
                {
                    case StopCondition.None:
                        {
                            if (StopConditionValue > 0)
                            {
                                AddError(nameof(StopCondition),
                                    $"A stop condition must be selected.");
                            }
                            break;
                        }
                    case StopCondition.SizeMb:
                    case StopCondition.TimeSec:
                        {
                            if (StopConditionValue <= 0)
                            {
                                AddError(nameof(StopCondition),
                                    $"Stop condition value must be a positive number.");
                            }
                            break;
                        }
                    case StopCondition.Max:
                    default:
                        {
                            AddError(nameof(StopCondition),
                                $"Invalid stop condition.");
                            break;
                        }
                }
                if (_StopCondition != value)
                {
                    _StopCondition = value;
                    OnPropertyChanged("StopCondition");
                }
            }
        }

        private int _StopConditionValue;
        public int StopConditionValue
        {
            get => _StopConditionValue;
            set
            {
                ClearErrors(nameof(StopConditionValue));
                switch (StopCondition)
                {
                    case StopCondition.None:
                        {
                            if (value > 0)
                            {
                                AddError(nameof(StopConditionValue),
                                    $"A stop condition must be selected.");
                            }
                            break;
                        }
                    case StopCondition.SizeMb:
                    case StopCondition.TimeSec:
                        {
                            if (value <= 0)
                            {
                                AddError(nameof(StopConditionValue),
                                    $"Stop condition value must be a positive number.");
                            }
                            break;
                        }
                    case StopCondition.Max:
                    default:
                        {
                            AddError(nameof(StopConditionValue),
                                $"Invalid stop condition.");
                            break;
                        }
                }

                if (_StopConditionValue != value)
                {
                    _StopConditionValue = value;
                    OnPropertyChanged("StopConditionValue");
                }
            }
        }
        #endregion

        #region observable properties

        //
        // Used for error validation
        //
        private int _NumProviderFilterForms;
        public int NumProviderFilterForms
        {
            get => _NumProviderFilterForms;
            private set
            {
                _NumProviderFilterForms = value;
                //
                // Visual validation feedback bound to "AddProvider" button
                //
                ClearErrors(nameof(NumProviderFilterForms));
                if (_NumProviderFilterForms == 0)
                {
                    AddError(nameof(NumProviderFilterForms), 
                        $"At least one provider must be added.");
                }
                OnPropertyChanged("NumProviderFilterForms");
            }
        }

        #endregion

        #region commands

        public AsyncRelayCommand NewSessionFromProviderCommand { get; set; }
        public AsyncRelayCommand<ParsedEtwProvider> AddProviderToFormCommand { get; set; }
        public RelayCommand ShowFormPreviewCommand { get; set; }
        public ConcurrentObservableCollection<ProviderFilterFormViewModel> ProviderFilterForms { get; set; }
        public RelayCommand<ProviderFilterFormViewModel> CloseDynamicTab { get; set; }

        #endregion

        public SystemInfo SystemInfo { get; set; }

        public SessionFormViewModel() : base()
        {
            ProviderFilterForms = new ConcurrentObservableCollection<ProviderFilterFormViewModel>();
            ProviderFilterForms.AsObservable.CollectionChanged += (o, args) =>
            {
                //
                // Whenever a provider filter form is added, removed or changed in the collection,
                // update our subscription to its form errors.
                //
                HandleChildSubscriptionForCollectionChangedNotification<ProviderFilterFormViewModel>(args);
            };
            SystemInfo = new SystemInfo();

            Name = _Name = string.Empty;
            LogLocation = _LogLocation = string.Empty;
            IsRealTime = _IsRealTime = true;
            StopCondition = _StopCondition = StopCondition.None;
            StopConditionValue = _StopConditionValue = 0;
            NumProviderFilterForms = 0;

            NewSessionFromProviderCommand = new AsyncRelayCommand(
                Command_NewSessionFromProvider, () => { return true; });
            AddProviderToFormCommand = new AsyncRelayCommand<ParsedEtwProvider>(
                Command_AddProviderToForm, _ => true);
            CloseDynamicTab = new RelayCommand<ProviderFilterFormViewModel>(
                Command_CloseDynamicTab, _ => true);
            ShowFormPreviewCommand = new RelayCommand(
                Command_ShowFormPreview, () => { return true; });

            //
            // When our form becomes valid, the session control buttons and associated commands
            // also become available.
            //
            ErrorsChanged += delegate (object? sender, DataErrorsChangedEventArgs e)
            {
                GlobalStateViewModel.Instance.g_SessionViewModel.NotifyCanExecuteChanged();
            };
        }

        private void Command_ShowFormPreview()
        {
            var popup = new System.Windows.Controls.Primitives.Popup()
            {
                StaysOpen = false,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse
            };
            var popBorder = new Border()
            {
                BorderBrush = Brushes.Black,
                Background = Brushes.LightYellow,
                BorderThickness = new Thickness(1)
            };
            var popContent = new TextBlock()
            {
                Text = $"{this}",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
            };
            popBorder.Child = popContent;
            popup.IsOpen = true;
            popup.Child = popBorder;
        }
         
        private async Task Command_NewSessionFromProvider()
        {
            var vm = GlobalStateViewModel.Instance.g_ProviderViewModel;
            var providers = vm.SelectedProviders;
            if (providers.Count == 0)
            {
                return;
            }

            //
            // Switch the mainwindow tabcontrol to our the Sessions tab before adding the provider
            // to the form, because it's possible the VM backing the form hasn't been created yet.
            //
            GlobalStateViewModel.Instance.g_MainWindowViewModel.RibbonTabControlSelectedIndex = 1;

            foreach (var provider in providers)
            {
                await Command_AddProviderToForm(provider);
            }
        }

        private async Task Command_AddProviderToForm(ParsedEtwProvider? Provider)
        {
            if (Provider == null)
            {
                Debug.Assert(false);
                return;
            }
            ProgressState.InitializeProgress(2);

            if (SystemInfo.ActiveProcessList.Count == 0)
            {
                //
                // Lazy initialize on first "Add" button click
                //
                ProgressState.UpdateProgressMessage("Loading process list, please wait...");
                await SystemInfo.Refresh();
                ProgressState.UpdateProgressMessage($"Found {SystemInfo.ActiveProcessList.Count} processes.");
            }

            var manifest = await GlobalStateViewModel.Instance.g_ProviderViewModel.GetProviderManifest(Provider.Id);
            if (manifest == null)
            {
                ProgressState.FinalizeProgress($"Unable to locate manifest for provider {Provider}");
                return;
            }

            var providerForm = new ProviderFilterFormViewModel(manifest.m_Manifest);

            //
            // Create a UI tab for this provider form. If the form already exists, switch to it.
            //
            var tabName = UiHelper.GetUniqueTabName(Provider.Id, "ProviderFilterForm");
            if (!UiHelper.CreateTabControlTab(
                "ProviderFilterFormsTabControl",
                providerForm,
                tabName,
                providerForm))
            {
                Debug.Assert(false);
                return;
            }            
            ProviderFilterForms.Add(providerForm);
            NumProviderFilterForms++;
        }

        public void Command_CloseDynamicTab(ProviderFilterFormViewModel? Form)
        {
            if (Form == null)
            {
                Debug.Assert(false);
                return;
            }
            Form.FinalizeForm();
            NumProviderFilterForms--;
            Debug.Assert(ProviderFilterForms.Contains(Form));
            ProviderFilterForms.Remove(Form);
            //
            // Remove the tab from the tab control.
            //
            var tabName = UiHelper.GetUniqueTabName(Form.Manifest.Provider.Id, "ProviderFilterForm");
            if (!UiHelper.RemoveTab("ProviderFilterFormsTabControl",tabName))
            {
                return;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Name: {Name}");
            sb.AppendLine($"Real-time: {IsRealTime}");
            sb.AppendLine($"Log location: {LogLocation}");
            sb.AppendLine($"Stop condition: {StopCondition}");
            sb.AppendLine($"Stop condition value: {StopConditionValue}");
            ProviderFilterForms.ToList().ForEach(f => sb.AppendLine($"{f}"));
            return sb.ToString();
        }

        public SessionFormModel? GetFormData()
        {
            var model = new SessionFormModel();
            model.Name = Name;
            model.IsRealTime = IsRealTime;
            model.StopCondition = StopCondition;
            model.StopConditionValue = StopConditionValue;
            model.LogLocation = LogLocation;
            Debug.Assert(ProviderFilterForms.Count > 0);
            foreach (var form in ProviderFilterForms)
            {
                var enabledProvider = new EnabledProvider(
                    form.Manifest.Provider.Id,
                    form.Manifest.Provider.Name!,
                    (byte)NativeTraceControl.EventTraceLevel.Information,
                    0,
                    ulong.MaxValue);
                //
                // Scope filter
                //
                if (form.ScopeFilter!.Processes.Count > 0) // process
                {
                    var pids = form.ScopeFilter.Processes.Select(p => p.Pid).ToList();
                    enabledProvider.SetProcessFilter(pids);
                }
                if (form.ScopeFilter.ExeNames.Count > 0) // exe
                {
                    var str = EtwHelper.GetEtwStringList(form.ScopeFilter.ExeNames);
                    enabledProvider.SetFilteredExeName(str);
                }
                if (form.ScopeFilter.AppIds.Count > 0) // appId
                {
                    var str = EtwHelper.GetEtwStringList(form.ScopeFilter.AppIds);
                    enabledProvider.SetFilteredPackageAppId(str);
                }
                if (form.ScopeFilter.PackageIds.Count > 0) // packageId
                {
                    var str = EtwHelper.GetEtwStringList(form.ScopeFilter.PackageIds);
                    enabledProvider.SetFilteredPackageId(str);
                }
                //
                // Attribute filter
                //
                if (form.AttributeFilter!.Events.Count > 0)
                {
                    var eventIds = form.AttributeFilter.Events.Select(e => e.Id).ToList();
                    var eventIdIntegers = eventIds.Select(int.Parse).ToList();
                    var enable = form.AttributeFilter.IsEnable;
                    Debug.Assert(enable.HasValue);
                    enabledProvider.SetEventIdsFilter(eventIdIntegers, enable ?? false);
                }
                if (form.AttributeFilter.Level != SourceLevels.Off)
                {
                    enabledProvider.Level = (byte)form.AttributeFilter.Level;
                }
                if (form.AttributeFilter.AnyKeywords.Count > 0)
                {
                    ulong anyKeywords = 0;
                    form.AttributeFilter.AnyKeywords.ToList().ForEach(k => { anyKeywords |= k.Value; });
                    enabledProvider.AnyKeywords = anyKeywords;
                }
                if (form.AttributeFilter.AllKeywords.Count > 0)
                {
                    ulong allKeywords = 0;
                    form.AttributeFilter.AllKeywords.ToList().ForEach(k => { allKeywords |= k.Value; });
                    enabledProvider.AllKeywords = allKeywords;
                }
                //
                // Stackwalk filter
                //
                if (form.StackwalkFilter!.Events.Count > 0)
                {
                    var eventIds = form.StackwalkFilter.Events.Select(e => e.Id).ToList();
                    var eventIdIntegers = eventIds.Select(int.Parse).ToList();
                    var enable = form.StackwalkFilter.IsEnable;
                    Debug.Assert(enable.HasValue);
                    enabledProvider.SetStackwalkEventIdsFilter(eventIdIntegers, enable ?? false);
                }
                if (form.StackwalkFilter.LevelKeywordFilterLevel != SourceLevels.Off)
                {
                    ulong anyKeywords = 0;
                    ulong allKeywords = 0;
                    form.StackwalkFilter.LevelKeywordFilterAnyKeywords.ToList().ForEach(
                        k => { anyKeywords |= k.Value; });
                    form.StackwalkFilter.LevelKeywordFilterAllKeywords.ToList().ForEach(
                        k => { allKeywords |= k.Value; });
                    var enable = form.StackwalkFilter.LevelKeywordFilterIsEnable;
                    Debug.Assert(enable.HasValue);
                    enabledProvider.SetStackwalkLevelKw((byte)form.StackwalkFilter.LevelKeywordFilterLevel,
                        anyKeywords, allKeywords, enable ?? false);
                }
                //
                // Payload filter
                //
                // Condense into a single dictionary with key being <eventId>:<Version>
                // and value being a PayloadFilter structure containing all predicates.
                //
                var payloads = new Dictionary<string, PayloadFilter>();
                foreach (var predicate in form.PayloadFilterPredicates)
                {
                    var key = $"{predicate.Event!.Id}:{predicate.Event.Version}";
                    PayloadFilter payloadFilter = null;
                    if (payloads.ContainsKey(key))
                    {
                        payloadFilter = payloads[key];
                    }
                    else
                    {
                        var eventDescriptor = new EVENT_DESCRIPTOR();
                        eventDescriptor.Id = ushort.Parse(predicate.Event.Id);
                        eventDescriptor.Version = byte.Parse(predicate.Event.Version);
                        payloadFilter = new PayloadFilter(
                            form.Manifest.Provider.Id, eventDescriptor, true);
                        payloads.Add(key, payloadFilter);
                    }

                    payloadFilter.AddPredicate(predicate.Field.Name,
                        predicate.Operator!.Value,
                        predicate.FieldValue!);
                }

                //
                // Now put that into a tuple list and add to the provider
                //
                var payloadFilters = new List<Tuple<PayloadFilter, bool>>();
                var anyPredicate = form.MatchAnyPredicate;
                payloads.Values.ToList().ForEach(p =>
                    payloadFilters.Add(new Tuple<PayloadFilter, bool>(p, anyPredicate)));
                if (payloadFilters.Count > 0)
                {
                    enabledProvider.AddPayloadFilters(payloadFilters);
                }

                //
                // ETW columns to display in the data capture datagrid.
                // Store only the EtwColumnDisplay since we don't need its wrapping VM.
                //
                Debug.Assert(form.ChosenEtwColumns.Count > 0);
                var cols = form.ChosenEtwColumns.ToList();
                var configuredProvider = new ConfiguredProvider()
                {
                    _EnabledProvider = enabledProvider,
                    Columns = cols,
                };
                model.ConfiguredProviders.Add(configuredProvider);
            }
            return model;
        }
    }
}
