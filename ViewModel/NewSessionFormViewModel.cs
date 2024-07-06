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

namespace EtwPilot.ViewModel
{
    internal class NewSessionFormViewModel : ViewModelBase
    {
        public LiveSessionModel Model { get; set; }

        private Dictionary<string, NewProviderFilterFormViewModel> m_ProviderFilterForms;

        private NewProviderFilterFormViewModel m_CurrentProviderFilterForm;
        public NewProviderFilterFormViewModel CurrentProviderFilterForm
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

        private ObservableCollection<ProcessObject> _activeProcessList;
        public ObservableCollection<ProcessObject> ActiveProcessList
        {
            get => _activeProcessList;
            set
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
            set
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
            set
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
            set
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
            set
            {
                if (_UniquePackageIds != value)
                {
                    _UniquePackageIds = value;
                    OnPropertyChanged("UniquePackageIds");
                }
            }
        }

        public NewSessionFormViewModel()
        {
            Model = new LiveSessionModel()
            {
                IsRealTime = true
            };
            m_ProviderFilterForms = new Dictionary<string, NewProviderFilterFormViewModel>();
            ActiveProcessList = new ObservableCollection<ProcessObject>();
            ActiveProcessList.Add(new ProcessObject {
                Pid = 0,
                Name = "[None]",
                Exe ="[None]",
                AppId = "[None]",
                PackageId = "[None]"});
            UniqueProcessNames = new ObservableCollection<string>();
            UniqueProcessNames.Add("[None]");
            UniqueExeNames = new ObservableCollection<string>();
            UniqueExeNames.Add("[None]");
            UniqueAppIds = new ObservableCollection<string>();
            UniqueAppIds.Add("[None]");
            UniquePackageIds = new ObservableCollection<string>();
            UniquePackageIds.Add("[None]");
        }

        public void Initialize()
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
        }

        public async Task<NewProviderFilterFormViewModel?> LoadProviderFilterForm(Guid Id)
        {
            var name = UiHelper.GetUniqueTabName(Id, "ProviderFilter");
            if (m_ProviderFilterForms.ContainsKey(name))
            {
                //
                // This form has already been loaded once and bound to an existing tab.
                // The caller has to go find the tab.
                //
                return m_ProviderFilterForms[name];
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
            var vm = new NewProviderFilterFormViewModel(manifest.SelectedProviderManifest);
            m_ProviderFilterForms.Add(name, vm);
            CurrentProviderFilterForm = vm;
            return vm;
        }

    }
    internal class ProcessObject
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public string Exe { get; set; }
        public string AppId { get; set; }
        public string PackageId { get; set; }
    }
}
