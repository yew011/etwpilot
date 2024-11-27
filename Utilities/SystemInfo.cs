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
using EtwPilot.ViewModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace EtwPilot.Utilities
{
    public class SystemInfo : INotifyPropertyChanged
    {
        #region observable properties

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

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        #endregion

        public SystemInfo()
        {
            ActiveProcessList = new ObservableCollection<ProcessObject>();
            UniqueProcessNames = new ObservableCollection<string>();
            UniqueExeNames = new ObservableCollection<string>();
            UniqueAppIds = new ObservableCollection<string>();
            UniquePackageIds = new ObservableCollection<string>();
        }

        public async Task Refresh()
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
    }
}
