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
using System.Security.Principal;

namespace EtwPilot.ViewModel
{
    public class GlobalStateViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region observable properties

        //
        // The CurrentViewModel is bound to the main ContentControl in MainWindowView.xaml
        // and is the primary means by which views are swapped in the viewing area.
        //
        private ViewModelBase _CurrentViewModel;
        public ViewModelBase CurrentViewModel
        {
            get => _CurrentViewModel;
            set
            {
                _CurrentViewModel = value;
                OnPropertyChanged(nameof(CurrentViewModel));
            }
        }

        private SettingsFormViewModel _Settings;
        public SettingsFormViewModel Settings
        {
            get { return _Settings; }
            set { _Settings = value; OnPropertyChanged(nameof(Settings)); }
        }

        //
        // No view can be displayed in MainWindowView.xaml's ContenControl until global
        // init is completed, asychronously. Only app-wide critical init is done here.
        //
        private bool _PrimaryViewEnabled;
        public bool PrimaryViewEnabled
        {
            get => _PrimaryViewEnabled;
            set {
                _PrimaryViewEnabled = value;
                OnPropertyChanged(nameof(PrimaryViewEnabled));
            }
        }

        private bool _IsAdmin;
        public bool IsAdmin
        {
            get => _IsAdmin;
            set
            {
                _IsAdmin = value;
                OnPropertyChanged(nameof(IsAdmin));
            }
        }

        #endregion

        #region global resources

        //
        // Global singleton.
        //
        private static GlobalStateViewModel _Instance = new GlobalStateViewModel();
        public static GlobalStateViewModel Instance
        {
            get => _Instance;
        }

        //
        // These are late-initialized from MainWindowViewModel.
        //
        public StackwalkHelper m_StackwalkHelper;

        #endregion

        #region View Model instances

        public GlobalInitViewModel g_InitViewModel { get; set; }
        public ProviderViewModel g_ProviderViewModel { get; set; }
        public SessionViewModel g_SessionViewModel { get; set; }
        public SessionFormViewModel g_SessionFormViewModel { get; set; }
        public InsightsViewModel g_InsightsViewModel { get; set; }
        public MainWindowViewModel g_MainWindowViewModel { get; set; }

        #endregion

        public GlobalStateViewModel()
        {
            //
            // Early initialize our own tracelogger to catch settings errors.
            // The level will be adjusted to whatever is in the settings later.
            //
            EtwPilot.Utilities.TraceLogger.Initialize();
            EtwPilot.Utilities.TraceLogger.SetLevel(System.Diagnostics.SourceLevels.Error);
            //
            // This ctor is invoked as a result of the binding expression in MainWindowView.xaml.
            // It is invoked before any other binding expressions in that view are evaluated,
            // and as such, it is important that the viewmodels relied upon in those expressions
            // are instantiated now, otherwise, no bindings will work.
            //
            // Note: ctors of VMs below _cannot_ access Settings from this context
            //
            _Settings = SettingsFormViewModel.LoadDefault();
            g_MainWindowViewModel = UiHelper.GetGlobalResource<MainWindowViewModel>(
                "g_MainWindowViewModel")!; // from App.xaml
            g_ProviderViewModel = new ProviderViewModel();
            g_SessionViewModel = new SessionViewModel();
            g_SessionFormViewModel = new SessionFormViewModel(); // lazy init
            g_InsightsViewModel = new InsightsViewModel();
            g_InitViewModel = new GlobalInitViewModel();

            m_StackwalkHelper = new StackwalkHelper();

            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                IsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
