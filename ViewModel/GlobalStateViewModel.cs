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

using EtwPilot.Utilities;
using System.ComponentModel;

namespace EtwPilot.ViewModel
{
    public class GlobalStateViewModel : INotifyPropertyChanged
    {
        #region observable properties

        private static ViewModelBase _CurrentViewModel;
        public ViewModelBase CurrentViewModel
        {
            get => _CurrentViewModel;
            set
            {
                _CurrentViewModel = value;
                OnPropertyChanged(nameof(CurrentViewModel));
            }
        }

        private static SettingsFormViewModel _Settings;
        public SettingsFormViewModel Settings
        {
            get { return _Settings; }
            set { _Settings = value; OnPropertyChanged(nameof(Settings)); }
        }

        #endregion

        private static GlobalStateViewModel _Instance = new GlobalStateViewModel();
        public static GlobalStateViewModel Instance
        {
            get { return _Instance; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region View Model instances

        public ProviderViewModel g_ProviderViewModel { get; set; }
        public SessionViewModel g_SessionViewModel { get; set; }
        public SessionFormViewModel g_SessionFormViewModel { get; set; }
        public InsightsViewModel g_InsightsViewModel { get; set; }
        public MainWindowViewModel g_MainWindowViewModel { get; set; }

        #endregion

        public ConverterLibrary g_ConverterLibrary;

        public GlobalStateViewModel()
        {
            //
            // Settings and g_MainWindowViewModel must be initialized in ctor
            // g_MainWindowViewModel will call Initialize on window load
            //
            _Settings = SettingsFormViewModel.LoadDefault();
            g_MainWindowViewModel = UiHelper.GetGlobalResource<MainWindowViewModel>(
                "g_MainWindowViewModel")!; // from App.xaml
        }

        public void Initialize()
        {
            //
            // Some View models access settings so they cannot be instantiated in ctor
            //
            g_ProviderViewModel = new ProviderViewModel();
            g_SessionViewModel = new SessionViewModel();
            g_SessionFormViewModel = new SessionFormViewModel(); // lazy init
            g_InsightsViewModel = new InsightsViewModel();

            g_ConverterLibrary = new ConverterLibrary();

            //
            // Subscribe to changes to the settings instance, so that autosave kicks in.
            // Note that the internal Save() routine in this class resets this value.
            //
            Settings.PropertyChanged += (obj, p) =>
            {
                if (p.PropertyName == "HasUnsavedChanges")
                {
                    return;
                }
                Settings.HasUnsavedChanges = true;
                if (p.PropertyName == "ModelPath" ||
                    p.PropertyName == "EmbeddingsModelFile" ||
                    p.PropertyName == "ModelConfig")
                {
                    Settings.HasModelRelatedUnsavedChanges = true;
                }
            };

            //
            // Subscribe to property change events in session form, so when the form becomes valid,
            // the session control buttons and associated commands are available.
            //
            g_SessionFormViewModel.ErrorsChanged += delegate (object? sender, DataErrorsChangedEventArgs e)
            {
                g_SessionViewModel.NotifyCanExecuteChanged();
            };
        }

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
