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
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using EtwPilot.Utilities;
using Newtonsoft.Json;
using static EtwPilot.Utilities.DataExporter;

namespace EtwPilot.ViewModel
{
    public abstract class ViewModelBase : NotifyPropertyAndErrorInfoBase
    {
        //
        // All derived VMs manage their own progress bar/messages
        //
        [JsonIgnore]
        public ProgressState ProgressState { get; set; }

        #region commands
        [JsonIgnore]
        public AsyncRelayCommand<ExportFormat> ExportDataCommand { get; set; }
        [JsonIgnore]
        public AsyncRelayCommand SettingsChangedCommand { get; set; }

        #endregion

        public ViewModelBase() : base()
        {
            ExportDataCommand = new AsyncRelayCommand<ExportFormat>(
                Command_ExportData, CanExecuteExportDataCommand);
            SettingsChangedCommand = new AsyncRelayCommand(
                Command_SettingsChanged, () => { return true; });
            ProgressState = new ProgressState();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected virtual async Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            throw new NotImplementedException();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public virtual async Task ViewModelActivated()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            throw new NotImplementedException();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public virtual async Task ViewModelDeactivated()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            throw new NotImplementedException();
        }

        public async Task Command_ExportData(ExportFormat Format, CancellationToken Token)
        {
            ProgressState.m_CurrentCommand = ExportDataCommand;
            ProgressState.CancelCommandButtonVisibility = Visibility.Visible;
            await ExportData(Format, Token);
            ProgressState.CancelCommandButtonVisibility = Visibility.Hidden;
            ProgressState.m_CurrentCommand = null;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public virtual async Task Command_SettingsChanged()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            //
            // Deriving classes must implement for insight to settings changes if they want
            //
        }

        protected virtual bool CanExecuteExportDataCommand(ExportFormat Format)
        {
            //
            // Deriving classes can override if other conditions should disable this command
            //
            return true;
        }
    }
}
