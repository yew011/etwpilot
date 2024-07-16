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
using System.ComponentModel;
using System.Collections.ObjectModel;
using EtwPilot.Model;
using etwlib;
using EtwPilot.Utilities;
using System.IO;

namespace EtwPilot.ViewModel
{
    internal class SessionFormViewModel : ViewModelBase
    {
        #region Properties - input form

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

        #region Properties - set by other VMs

        private ProviderFilterFormViewModel? m_CurrentProviderFilterForm;
        public ProviderFilterFormViewModel? CurrentProviderFilterForm
        {
            get => m_CurrentProviderFilterForm;
            set
            {
                if (m_CurrentProviderFilterForm != value)
                {
                    m_CurrentProviderFilterForm = value;
                    OnPropertyChanged("CurrentProviderFilterForm");
                }
            }
        }

        public List<ParsedEtwProvider>? InitialProviders { get; set; }
        #endregion

        #region Properties - set internally

        private int _NumProviderFilterForms;
        public int NumProviderFilterForms
        {
            get => _NumProviderFilterForms;
            private set
            {
                //
                // Visual validation feedback bound to "AddProvider" button
                //
                ClearErrors(nameof(NumProviderFilterForms));
                if (value == 0)
                {
                    AddError(nameof(NumProviderFilterForms), 
                        $"At least one provider must be added.");
                }
                else if (_NumProviderFilterForms != value)
                {
                    _NumProviderFilterForms = value;
                    OnPropertyChanged("NumProviderFilterForms");
                }
            }
        }

        private ObservableCollection<ProcessObject> _activeProcessList;
        public ObservableCollection<ProcessObject> ActiveProcessList
        {
            get => _activeProcessList;
            private set
            {
                if (_activeProcessList != value)
                {
                    _activeProcessList = value;
                    OnPropertyChanged("ActiveProcessList");
                }
            }
        }

        private ObservableCollection<string> _UniqueProcessNames;
        public ObservableCollection<string> UniqueProcessNames
        {
            get => _UniqueProcessNames;
            private set
            {
                if (_UniqueProcessNames != value)
                {
                    _UniqueProcessNames = value;
                    OnPropertyChanged("UniqueProcessNames");
                }
            }
        }

        private ObservableCollection<string> _UniqueExeNames;
        public ObservableCollection<string> UniqueExeNames
        {
            get => _UniqueExeNames;
            private set
            {
                if (_UniqueExeNames != value)
                {
                    _UniqueExeNames = value;
                    OnPropertyChanged("UniqueExeNames");
                }
            }
        }

        private ObservableCollection<string> _UniqueAppIds;
        public ObservableCollection<string> UniqueAppIds
        {
            get => _UniqueAppIds;
            private set
            {
                if (_UniqueAppIds != value)
                {
                    _UniqueAppIds = value;
                    OnPropertyChanged("UniqueAppIds");
                }
            }
        }

        private ObservableCollection<string> _UniquePackageIds;
        public ObservableCollection<string> UniquePackageIds
        {
            get => _UniquePackageIds;
            private set
            {
                if (_UniquePackageIds != value)
                {
                    _UniquePackageIds = value;
                    OnPropertyChanged("UniquePackageIds");
                }
            }
        }
        #endregion

        private Dictionary<string, ProviderFilterFormViewModel> m_ProviderFilterForms;        

        public SessionFormViewModel()
        {
            m_ProviderFilterForms = new Dictionary<string, ProviderFilterFormViewModel>();
            ActiveProcessList = new ObservableCollection<ProcessObject>();
            UniqueProcessNames = new ObservableCollection<string>();
            UniqueExeNames = new ObservableCollection<string>();
            UniqueAppIds = new ObservableCollection<string>();
            UniquePackageIds = new ObservableCollection<string>();
            Name = _Name = string.Empty;
            LogLocation = _LogLocation = string.Empty;
            IsRealTime = _IsRealTime = true;
            StopCondition = _StopCondition = StopCondition.None;
            StopConditionValue = _StopConditionValue = 0;
            ActiveProcessList = _activeProcessList = new ObservableCollection<ProcessObject>();
            UniqueProcessNames = _UniqueProcessNames = new ObservableCollection<string>();
            UniqueExeNames = _UniqueExeNames = new ObservableCollection<string>();
            UniqueAppIds = _UniqueAppIds = new ObservableCollection<string>();
            UniquePackageIds = _UniquePackageIds = new ObservableCollection<string>();
            NumProviderFilterForms = 0;
        }

        public async Task RefreshProcessList()
        {
            ActiveProcessList.Clear();
            UniqueExeNames.Clear();
            UniqueAppIds.Clear();
            UniquePackageIds.Clear();

            ActiveProcessList.Add(new ProcessObject
            {
                Pid = 0,
                Name = "[None]",
                Exe = "[None]",
                AppId = "[None]",
                PackageId = "[None]"
            });
            UniqueProcessNames.Add("[None]");
            UniqueExeNames.Add("[None]");
            UniqueAppIds.Add("[None]");
            UniquePackageIds.Add("[None]");

            await Task.Run(() =>
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        if (process.Id == 0 || process.Id == 4 || process.Handle == nint.Zero ||
                            process.MainModule == null)
                        {
                            continue;
                        }
                    }
                    catch (Win32Exception)
                    {
                        continue; // probably access is denied
                    }
                    catch (InvalidOperationException)
                    {
                        continue; // probably the process died
                    }

                    //
                    // If this process is an MS Store/UWP app, save it
                    //
                    var package = MSStoreAppPackageHelper.GetPackage(process.Handle);
                    string packageId = null;
                    string appId = null;

                    if (package != null)
                    {
                        (packageId, appId) = package;
                    }

                    ActiveProcessList.Add(new ProcessObject
                    {
                        Pid = process.Id,
                        Name = process.ProcessName,
                        Exe = process.MainModule!.FileName,
                        PackageId = packageId,
                        AppId = appId,
                    });

                    if (!UniqueProcessNames.Contains(process.ProcessName))
                    {
                        UniqueProcessNames.Add(process.ProcessName);
                    }
                    if (!UniqueExeNames.Contains(process.MainModule!.FileName))
                    {
                        UniqueExeNames.Add(process.MainModule!.FileName);
                    }
                    if (appId != null && !UniqueAppIds.Contains(appId))
                    {
                        UniqueAppIds.Add(appId);
                    }
                    if (packageId != null && !UniquePackageIds.Contains(packageId))
                    {
                        UniquePackageIds.Add(packageId);
                    }
                }
            });
        }

        public async Task<ProviderFilterFormViewModel?> LoadProviderFilterForm(string TabName, Guid Id)
        {
            if (m_ProviderFilterForms.ContainsKey(TabName))
            {
                //
                // This form has already been loaded once and bound to an existing tab.
                // The caller has to go find the tab.
                //
                return m_ProviderFilterForms[TabName];
            }
            var g_MainWindowVm = UiHelper.GetGlobalResource<MainWindowViewModel>("g_MainWindowViewModel");
            if (g_MainWindowVm == null)
            {
                return null;
            }
            var manifest = await g_MainWindowVm.m_ProviderViewModel.LoadProviderManifest(Id);
            if (manifest == null)
            {
                return null;
            }
            NumProviderFilterForms++;
            var vm = new ProviderFilterFormViewModel(manifest.SelectedProviderManifest);
            m_ProviderFilterForms.Add(TabName, vm);
            CurrentProviderFilterForm = vm;
            return vm;
        }

        public void RemoveProviderFilterForm(string TabName)
        {
            if (m_ProviderFilterForms.ContainsKey(TabName))
            {
                NumProviderFilterForms--;
                m_ProviderFilterForms.Remove(TabName);
            }
        }

        public SessionFormModel? GetFormData()
        {
            var model = new SessionFormModel();
            model.Name = Name;
            model.IsRealTime = IsRealTime;
            model.StopCondition = StopCondition;
            model.StopConditionValue = StopConditionValue;
            model.LogLocation = LogLocation;
            Debug.Assert(m_ProviderFilterForms.Count > 0);
            foreach (var item in m_ProviderFilterForms)
            {
                var form = item.Value;
                var enabledProvider = new EnabledProvider(
                    form.Manifest.Provider.Id,
                    form.Manifest.Provider.Name!,
                    (byte)NativeTraceControl.EventTraceLevel.Information,
                    0,
                    ulong.MaxValue);
                //
                // Scope filter
                //
                if (form.ScopeFilter.Processes.Count > 0) // process
                {
                    var pids = form.ScopeFilter.Processes.Select(p => p.Pid).ToList();
                    enabledProvider.SetProcessFilter(pids);
                }
                if (form.ScopeFilter.ExeNames.Count > 0) // exe
                {
                    var str = ScopeFilterViewModel.GetEtwString(form.ScopeFilter.ExeNames);
                    enabledProvider.SetFilteredExeName(str);
                }
                if (form.ScopeFilter.AppIds.Count > 0) // appId
                {
                    var str = ScopeFilterViewModel.GetEtwString(form.ScopeFilter.AppIds);
                    enabledProvider.SetFilteredPackageAppId(str);
                }
                if (form.ScopeFilter.PackageIds.Count > 0) // packageId
                {
                    var str = ScopeFilterViewModel.GetEtwString(form.ScopeFilter.PackageIds);
                    enabledProvider.SetFilteredPackageId(str);
                }
                //
                // Attribute filter
                //
                if (form.AttributeFilter.Events.Count > 0)
                {
                    var eventIds = form.AttributeFilter.Events.Select(e => e.Id).ToList();
                    var eventIdIntegers = eventIds.Select(int.Parse).ToList();
                    var enable = form.AttributeFilter.IsEnable;
                    Debug.Assert(enable.HasValue);
                    enabledProvider.SetEventIdsFilter(eventIdIntegers, form.AttributeFilter.IsEnable ?? false);
                }
                if (form.AttributeFilter.Level != SourceLevels.Off)
                {
                    enabledProvider.Level = (byte)form.AttributeFilter.Level;
                }
                if (form.AttributeFilter.AnyKeywords.Count > 0)
                {
                    ulong anyKeywords = 0;
                    //form.AttributeFilter.AnyKeywords.ForEach(k => { anyKeywords |= k.Value; });
                    enabledProvider.AnyKeywords = anyKeywords;
                }
                if (form.AttributeFilter.AllKeywords.Count > 0)
                {
                    ulong allKeywords = 0;
                    //form.AttributeFilter.AllKeywords.ForEach(k => { allKeywords |= k.Value; });
                    enabledProvider.AllKeywords = allKeywords;
                }
                //
                // Stackwalk filter
                //
                if (form.StackwalkFilter.Events.Count > 0)
                {
                    var eventIds = form.StackwalkFilter.Events.Select(e => e.Id).ToList();
                    var eventIdIntegers = eventIds.Select(int.Parse).ToList();
                    enabledProvider.SetStackwalkEventIdsFilter(eventIdIntegers, form.StackwalkFilter.IsEnable);
                }
                if (form.StackwalkFilter.LevelKeywordFilterLevel != SourceLevels.Off)
                {
                    ulong anyKeywords = 0;
                    ulong allKeywords = 0;
                    form.StackwalkFilter.LevelKewyordFilterAnyKeywords.ForEach(k => { anyKeywords |= k.Value; });
                    form.StackwalkFilter.LevelKeywordFilterAllKeywords.ForEach(k => { allKeywords |= k.Value; });
                    enabledProvider.SetStackwalkLevelKw((byte)form.StackwalkFilter.LevelKeywordFilterLevel,
                        anyKeywords, allKeywords, form.StackwalkFilter.IsEnable);
                }
                //
                // Payload filter
                //
                var payloadFilters = new List<Tuple<PayloadFilter, bool>>();
                var anyPredicate = form.MatchAnyPredicate;
                foreach (var predicate in form.PayloadFilterPredicates)
                {

                }

                model.EnabledProviders.Add(enabledProvider);
            }
            return model;
        }
    }

    public class ProcessObject
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public string Exe { get; set; }
        public string AppId { get; set; }
        public string PackageId { get; set; }
    }
}
