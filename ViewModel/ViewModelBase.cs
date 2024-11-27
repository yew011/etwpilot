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
using System.Runtime.CompilerServices;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using EtwPilot.Utilities;
using static EtwPilot.Utilities.DataExporter;

namespace EtwPilot.ViewModel
{
    public abstract class ViewModelBase : NotifyPropertyAndErrorInfoBase
    {
        //
        // All derived VMs manage their own progress bar/messages
        //
        public ProgressState ProgressState { get; set; }
        public dynamic m_CurrentCommand { get; set; }

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

        #region commands

        public AsyncRelayCommand CancelCurrentCommandCommand { get; set; }
        public AsyncRelayCommand<ExportFormat> ExportDataCommand { get; set; }

        #endregion

        public ViewModelBase() : base()
        {
            CancelCurrentCommandCommand = new AsyncRelayCommand(
                Command_CancelCurrentCommand, () => { return true; });
            ExportDataCommand = new AsyncRelayCommand<ExportFormat>(
                Command_ExportData, _ => true);
            CancelCommandButtonVisibility = Visibility.Hidden;
            m_CurrentCommand = null;
            ProgressState = new ProgressState();
        }

        protected virtual Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
        {
            throw new NotImplementedException();
        }

        public virtual Task ViewModelActivated()
        {
            throw new NotImplementedException();
        }

        public virtual Task ViewModelDeactivated()
        {
            throw new NotImplementedException();
        }

        public async Task Command_CancelCurrentCommand()
        {
            if (m_CurrentCommand == null || !m_CurrentCommand?.CanBeCanceled)
            {
                return;
            }
            m_CurrentCommand!.Cancel();
        }

        public async Task Command_ExportData(ExportFormat Format, CancellationToken Token)
        {
            m_CurrentCommand = ExportDataCommand;
            CancelCommandButtonVisibility = Visibility.Visible;
            await ExportData(Format, Token);
            CancelCommandButtonVisibility = Visibility.Hidden;
            m_CurrentCommand = null;
        }
    }

    public class ProgressState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

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

        public int _ProgressMax;
        public int ProgressMax
        {
            get => _ProgressMax;
            set { _ProgressMax = value; OnPropertyChanged("ProgressMax"); }
        }

        public ProgressState()
        {
            FinalizeProgress(string.Empty);
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
                ProgressValue = 0;
                Visible = Visibility.Visible;
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

        public void UpdateProgressValue()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressValue++;
            }));
        }
    }
}
