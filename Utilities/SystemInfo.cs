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
using System.ComponentModel;
using System.Diagnostics;
using Meziantou.Framework.WPF.Collections;

namespace EtwPilot.Utilities
{
    public class ProcessObject
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public string Exe { get; set; }
        public string? AppId { get; set; }
        public string? PackageId { get; set; }

        public ProcessObject()
        {
            Pid = -1;
        }
    }

    public class SystemInfo
    {
        public ConcurrentObservableCollection<ProcessObject> ActiveProcessList { get; set; }
        public ConcurrentObservableCollection<string> UniqueProcessNames { get; set; }
        public ConcurrentObservableCollection<string> UniqueExeNames { get; set; }
        public ConcurrentObservableCollection<string> UniqueAppIds { get; set; }
        public ConcurrentObservableCollection<string> UniquePackageIds { get; set; }

        public SystemInfo()
        {
            ActiveProcessList = new ConcurrentObservableCollection<ProcessObject>();
            UniqueProcessNames = new ConcurrentObservableCollection<string>();
            UniqueExeNames = new ConcurrentObservableCollection<string>();
            UniqueAppIds = new ConcurrentObservableCollection<string>();
            UniquePackageIds = new ConcurrentObservableCollection<string>();
        }

        public async Task Refresh()
        {
            ActiveProcessList.Clear();
            UniqueExeNames.Clear();
            UniqueAppIds.Clear();
            UniquePackageIds.Clear();

            ActiveProcessList.Add(new ProcessObject()
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
                    string? packageId = null;
                    string? appId = null;

                    if (package != null)
                    {
                        (packageId, appId) = package;
                    }

                    ActiveProcessList.Add(new ProcessObject()
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
