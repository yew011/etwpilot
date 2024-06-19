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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace EtwPilot.ViewModel
{
    class MainWindowViewModel : ViewModelBase
    {
        #region Properties

        private ProgressState _m_ProgressState;
        public ProgressState m_ProgressState
        {
            get => _m_ProgressState;
            set
            {
                if (_m_ProgressState != value)
                {
                    _m_ProgressState = value;
                    OnPropertyChanged("m_ProgressState");
                }
            }
        }

        private ViewModelBase _CurrentViewModel;
        public ViewModelBase CurrentViewModel
        {
            get => _CurrentViewModel;
            set
            {
                if (_CurrentViewModel != value)
                {
                    _CurrentViewModel = value;
                    OnPropertyChanged("CurrentViewModel");
                }
            }
        }

        private ProviderViewModel _m_ProviderViewModel;
        public ProviderViewModel m_ProviderViewModel
        {
            get => _m_ProviderViewModel;
            set
            {
                if (_m_ProviderViewModel != value)
                {
                    _m_ProviderViewModel = value;
                    OnPropertyChanged("m_ProviderViewModel");
                }
            }
        }

        private ProviderManifestViewModel _m_ProviderManifestViewModel;
        public ProviderManifestViewModel m_ProviderManifestViewModel
        {
            get => _m_ProviderManifestViewModel;
            set
            {
                if (_m_ProviderManifestViewModel != value)
                {
                    _m_ProviderManifestViewModel = value;
                    OnPropertyChanged("m_ProviderManifestViewModel");
                }
            }
        }

        private Visibility _ProviderManifestVisible;
        public Visibility ProviderManifestVisible
        {
            get => _ProviderManifestVisible;
            set
            {
                if (_ProviderManifestVisible != value)
                {
                    _ProviderManifestVisible = value;
                    OnPropertyChanged("ProviderManifestVisible");
                }
            }
        }

        private ParsedEtwManifest _selectedProviderManifest;
        public ParsedEtwManifest SelectedProviderManifest
        {
            get => _selectedProviderManifest;
            set
            {
                if (_selectedProviderManifest != value)
                {
                    _selectedProviderManifest = value;
                    OnPropertyChanged("SelectedProviderManifest");
                }
            }
        }

        #endregion

        #region commands

        private ICommand _loadProvidersCommand;
        public ICommand LoadProvidersCommand
        {
            get => _loadProvidersCommand == null ? new AsyncRelayCommand(Command_LoadProviders) : _loadProvidersCommand;
            set
            {
                _loadProvidersCommand = value;
            }
        }

        private AsyncRelayCommand<Guid> _loadProviderManifestCommand;
        public AsyncRelayCommand<Guid> LoadProviderManifestCommand
        {
            get => _loadProviderManifestCommand == null ? new AsyncRelayCommand<Guid>(Command_LoadProviderManifest) : _loadProviderManifestCommand;
            set
            {
                _loadProviderManifestCommand = value;
            }
        }

        #endregion

        public MainWindowViewModel()
        {
            m_ProgressState = new ProgressState();
            m_ProviderViewModel = new ProviderViewModel(m_ProgressState);
            m_ProviderManifestViewModel = new ProviderManifestViewModel(m_ProgressState);
            CurrentViewModel = m_ProviderViewModel;
            ProviderManifestVisible = Visibility.Hidden;
        }

        public void ShowProviderViewModel()
        {
            CurrentViewModel = m_ProviderViewModel;
        }

        private async Task Command_LoadProviders()
        {
            if (_m_ProgressState.Visible == Visibility.Visible)
            {
                return;
            }
            m_ProgressState.Visible = Visibility.Visible;
            CurrentViewModel = m_ProviderViewModel;
            await m_ProviderViewModel.LoadProviders();
            m_ProgressState.Visible = Visibility.Hidden;
        }

        private async Task<ParsedEtwManifest?> Command_LoadProviderManifest(Guid Id)
        {
            if (m_ProgressState.Visible == Visibility.Visible)
            {
                return null;
            }
            ProviderManifestVisible = Visibility.Visible;
            m_ProgressState.Visible = Visibility.Visible;
            var provider = await m_ProviderManifestViewModel.LoadProviderManifest(Id);
            CurrentViewModel = m_ProviderManifestViewModel;
            m_ProgressState.Visible = Visibility.Hidden;
            return provider;
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
            FinalizeProgress();
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

        public void FinalizeProgress()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressMax = 0;
                StatusText = "";
                ProgressValue = 0;
                Visible = Visibility.Hidden;
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
