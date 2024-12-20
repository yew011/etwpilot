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
using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace EtwPilot.ViewModel
{
    public class GlobalStateViewModel : INotifyPropertyChanged
    {
        #region observable properties

        //
        // The CurrentViewModel is bound to the main ContentControl in MainWindowView.xaml
        // and is the primary means by which views are swapped in the viewing area.
        //
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

        //
        // No view can be displayed in MainWindowView.xaml's ContenControl until global
        // init is completed, asychronously. Only app-wide critical init is done here.
        //
        private static bool _PrimaryViewEnabled;
        public bool PrimaryViewEnabled
        {
            get { return _PrimaryViewEnabled; }
            set {
                _PrimaryViewEnabled = value;
                OnPropertyChanged(nameof(PrimaryViewEnabled));
            }
        }

        private static bool _IsAdmin;
        public bool IsAdmin
        {
            get { return _IsAdmin; }
            set
            {
                _IsAdmin = value;
                OnPropertyChanged(nameof(IsAdmin));
            }
        }

        #endregion

        private static GlobalStateViewModel _Instance = new GlobalStateViewModel();
        public static GlobalStateViewModel Instance
        {
            get { return _Instance; }
        }

        //
        // Global resources used by multiple VMs. These are late-initialized from MainWindowViewModel.
        //
        public StackwalkHelper m_StackwalkHelper;

        public event PropertyChangedEventHandler? PropertyChanged;

        #region View Model instances

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

            //
            // Global resources
            //
            m_StackwalkHelper = new StackwalkHelper();
            PrimaryViewEnabled = false;

            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                IsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public async Task ApplySettingsChanges()
        {
            Debug.Assert(Settings.ChangedProperties.Count > 0);

            //
            // Validate the completed settings object.
            //
            if (!await Settings.Validate())
            {
                return;
            }

            //
            // This routine is invoked from MainWindowView code behind when the backstage
            // menu is closed, presumably after settings are changing. While VMs update
            // their views and other data sources, we hide views.
            //
            PrimaryViewEnabled = false;
            Settings.Save(null);

            //
            // Update global resources
            //
            if (Settings.ChangedProperties.Contains(nameof(SettingsFormViewModel.DbghelpPath)) ||
                Settings.ChangedProperties.Contains(nameof(SettingsFormViewModel.SymbolPath)))
            {
                Instance.m_StackwalkHelper = new StackwalkHelper();
                await GlobalResourceInitialization();
                PrimaryViewEnabled = false;
            }

            //
            // Notify all VMs
            //
            await g_MainWindowViewModel.SettingsChangedCommand.ExecuteAsync(null);
            await g_ProviderViewModel.SettingsChangedCommand.ExecuteAsync(null);
            await g_SessionViewModel.SettingsChangedCommand.ExecuteAsync(null);
            await g_SessionFormViewModel.SettingsChangedCommand.ExecuteAsync(null);
            await g_InsightsViewModel.SettingsChangedCommand.ExecuteAsync(null);

            //
            // Clear the changed properties
            //
            Settings.ChangedProperties.Clear();
            PrimaryViewEnabled = true;
        }

        public async Task<bool> GlobalResourceInitialization()
        {
            PrimaryViewEnabled = false;

            //
            // Now we can validate the settings object that was loaded in ctor path
            //
            if (!await Settings.Validate())
            {
                return false;
            }

            //
            // Global resources are initialized once when the MainWindowView is shown
            // and anytime thereafter when applicable settings are changed. This routine
            // should be relatively fast!
            //
            var sb = new StringBuilder();

            try
            {
                await m_StackwalkHelper.Initialize();
            }
            catch (Exception)
            {
                sb.Append($"Unable to init symbol resolver. ");
            }

            if (sb.Length > 0)
            {
                CurrentViewModel.ProgressState.EphemeralStatusText = sb.ToString();
            }
            PrimaryViewEnabled = true;
            return true;
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
