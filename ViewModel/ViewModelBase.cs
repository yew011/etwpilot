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
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

        #region observable properties

        private bool _IsViewEnabled;
        [JsonIgnore]
        public bool IsViewEnabled
        {
            get => _IsViewEnabled;
            set
            {
                if (_IsViewEnabled != value)
                {
                    _IsViewEnabled = value;
                    OnPropertyChanged("IsViewEnabled");
                }
            }
        }

        #endregion

        public ViewModelBase() : base()
        {
            ExportDataCommand = new AsyncRelayCommand<ExportFormat>(
                Command_ExportData, CanExecuteExportDataCommand);
            SettingsChangedCommand = new AsyncRelayCommand(
                Command_SettingsChanged, () => { return true; });
            ProgressState = new ProgressState();
            IsViewEnabled = true;
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

    public class FollowupAction
    {
        public Action<dynamic>? Callback { get; set; }
        public string? Title { get; set; }
        public dynamic? CallbackArgument { get; set; }
    }

    public class ProgressState : INotifyPropertyChanged
    {
        #region observable properties

        private Visibility _CancelCommandButtonVisibility;
        public Visibility CancelCommandButtonVisibility
        {
            get => _CancelCommandButtonVisibility;
            set
            {
                if (_CancelCommandButtonVisibility != value)
                {
                    _CancelCommandButtonVisibility = value;
                    OnPropertyChanged("CancelCommandButtonVisibility");
                }
            }
        }

        private Visibility _FollowupActionVisibility;
        public Visibility FollowupActionVisibility
        {
            get => _FollowupActionVisibility;
            set
            {
                if (_FollowupActionVisibility != value)
                {
                    _FollowupActionVisibility = value;
                    OnPropertyChanged("FollowupActionVisibility");
                }
            }
        }

        private string _FollowupActionTitle;
        public string FollowupActionTitle
        {
            get => _FollowupActionTitle;
            set
            {
                if (_FollowupActionTitle != value)
                {
                    _FollowupActionTitle = value;
                    OnPropertyChanged("FollowupActionTitle");
                }
            }
        }

        private Visibility _visible;
        public Visibility Visible
        {
            get => _visible;
            set { _visible = value; OnPropertyChanged("Visible"); }
        }

        private int _ProgressValue;
        public int ProgressValue
        {
            get => _ProgressValue;
            set { _ProgressValue = value; OnPropertyChanged("ProgressValue"); }
        }

        public string _StatusText;
        public string StatusText
        {
            get => _StatusText;
            set { _StatusText = value; OnPropertyChanged("StatusText"); }
        }

        public string _EphemeralStatusText;
        public string EphemeralStatusText
        {
            get => _EphemeralStatusText;
            set { _EphemeralStatusText = value; OnPropertyChanged("EphemeralStatusText"); }
        }

        public int _ProgressMax;
        public int ProgressMax
        {
            get => _ProgressMax;
            set { _ProgressMax = value; OnPropertyChanged("ProgressMax"); }
        }
        #endregion

        #region Commands

        public RelayCommand CancelCurrentCommandCommand { get; set; }
        public RelayCommand<FollowupAction> SetFollowupActionCommand { get; set; }
        public RelayCommand ExecuteFollowupActionCommand { get; set; }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        public dynamic m_CurrentCommand { get; set; }
        private FollowupAction m_FollowupAction { get; set; }

        public ProgressState()
        {
            FinalizeProgress(string.Empty);

            Visible = Visibility.Hidden;
            CancelCurrentCommandCommand = new RelayCommand(
                Command_CancelCurrentCommand, () => { return true; });
            SetFollowupActionCommand = new RelayCommand<FollowupAction>(
                Command_SetFollowupAction, _ => true);
            ExecuteFollowupActionCommand = new RelayCommand(
                Command_ExecuteFollowupAction, () => { return true; });
            CancelCommandButtonVisibility = Visibility.Hidden;
            m_CurrentCommand = null;
            FollowupActionVisibility = Visibility.Hidden;
            m_FollowupAction = null;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void InitializeProgress(int Max)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressMax = Max;
                StatusText = "";
                EphemeralStatusText = "";
                ProgressValue = 0;
                Visible = Visibility.Visible;
                CancelCommandButtonVisibility = Visibility.Hidden;
                FollowupActionVisibility = Visibility.Hidden;
            }));
        }

        public void FinalizeProgress(string FinalMessage)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressMax = 0;
                ProgressValue = 0;
                Visible = Visibility.Hidden;
                StatusText = FinalMessage;
            }));
        }

        public void UpdateProgressMessage(string Text)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                StatusText = Text;
            }));
        }

        public void UpdateEphemeralStatustext(string Text)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                EphemeralStatusText = Text;
            }));
        }

        public void UpdateProgressValue()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressValue++;
            }));
        }

        public void Command_CancelCurrentCommand()
        {
            CancelCommandButtonVisibility = Visibility.Hidden;
            if (m_CurrentCommand == null || !m_CurrentCommand?.CanBeCanceled)
            {
                return;
            }
            m_CurrentCommand!.Cancel();
        }

        public void Command_SetFollowupAction(FollowupAction? Arguments)
        {
            if (Arguments == null)
            {
                Debug.Assert(false);
                return;
            }
            //
            // Invoked by a deriving VM class when it's about to expose the "follow up action"
            // button in the statusbar to the user.
            //
            m_FollowupAction = Arguments;
            FollowupActionTitle = Arguments.Title!;
            FollowupActionVisibility = Visibility.Visible;
        }

        public void Command_ExecuteFollowupAction()
        {
            //
            // Invoked from XAML binding expression evaluation when user clicks the follow up
            // action button defined by deriving class.
            //
            if (m_FollowupAction == null || m_FollowupAction.Callback == null)
            {
                return;
            }
            m_FollowupAction.Callback(m_FollowupAction.CallbackArgument);
        }
    }
}
