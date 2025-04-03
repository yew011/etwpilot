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
using System.Diagnostics;
using System.Text;

namespace EtwPilot.ViewModel
{
    public class GlobalInitViewModel : ViewModelBase
    {
        public override Task ViewModelActivated()
        {
            GlobalStateViewModel.Instance.CurrentViewModel = this;
            return Task.CompletedTask;
        }

        public async Task ApplySettingsChanges()
        {
            ProgressState.InitializeProgress(4);
            ProgressState.UpdateProgressMessage("Applying settings changes...");
            ProgressState.UpdateProgressValue();

            //
            // This routine is invoked from MainWindowView code behind when the backstage
            // menu is closed, presumably after settings are changing. While VMs update
            // their views and other data sources, we hide views.
            //
            GlobalStateViewModel.Instance.PrimaryViewEnabled = false;

            Debug.Assert(GlobalStateViewModel.Instance.Settings.ChangedProperties.Count > 0);

            //
            // Validate the completed settings object.
            //
            if (!await GlobalStateViewModel.Instance.Settings.Validate())
            {
                GlobalStateViewModel.Instance.PrimaryViewEnabled = true;
                ProgressState.FinalizeProgress("");
                return;
            }
            ProgressState.UpdateProgressValue();

            GlobalStateViewModel.Instance.Settings.Save(null);

            //
            // Update global resources
            //
            if (GlobalStateViewModel.Instance.Settings.ChangedProperties.Contains(nameof(SettingsFormViewModel.DbghelpPath)) ||
                GlobalStateViewModel.Instance.Settings.ChangedProperties.Contains(nameof(SettingsFormViewModel.SymbolPath)))
            {
                GlobalStateViewModel.Instance.m_StackwalkHelper = new StackwalkHelper();
                await GlobalResourceInitialization();
                GlobalStateViewModel.Instance.PrimaryViewEnabled = false;
            }

            ProgressState.UpdateProgressValue();

            //
            // Notify all VMs
            //
            await GlobalStateViewModel.Instance.g_MainWindowViewModel.SettingsChangedCommand.ExecuteAsync(null);
            await GlobalStateViewModel.Instance.g_ProviderViewModel.SettingsChangedCommand.ExecuteAsync(null);
            await GlobalStateViewModel.Instance.g_SessionViewModel.SettingsChangedCommand.ExecuteAsync(null);
            await GlobalStateViewModel.Instance.g_SessionFormViewModel.SettingsChangedCommand.ExecuteAsync(null);
            await GlobalStateViewModel.Instance.g_InsightsViewModel.SettingsChangedCommand.ExecuteAsync(null);

            ProgressState.UpdateProgressValue();

            //
            // Clear the changed properties
            //
            GlobalStateViewModel.Instance.Settings.ChangedProperties.Clear();
            GlobalStateViewModel.Instance.PrimaryViewEnabled = true;
            ProgressState.FinalizeProgress("");
        }

        public async Task<bool> GlobalResourceInitialization()
        {
            ProgressState.InitializeProgress(3);
            ProgressState.UpdateProgressMessage("Initializing global resources...");
            ProgressState.UpdateProgressValue();

            GlobalStateViewModel.Instance.PrimaryViewEnabled = false;

            //
            // Now we can validate the settings object that was loaded in ctor path
            //
            if (!await GlobalStateViewModel.Instance.Settings.Validate())
            {
                GlobalStateViewModel.Instance.PrimaryViewEnabled = true;
                ProgressState.FinalizeProgress("");
                return false;
            }

            ProgressState.UpdateProgressValue();

            //
            // Global resources are initialized once when the MainWindowView is shown
            // and anytime thereafter when applicable settings are changed. This routine
            // should be relatively fast!
            //
            var sb = new StringBuilder();

            try
            {
                await GlobalStateViewModel.Instance.m_StackwalkHelper.Initialize();
            }
            catch (Exception)
            {
                sb.Append($"Unable to init symbol resolver. ");
            }

            ProgressState.UpdateProgressValue();

            if (sb.Length > 0)
            {
                ProgressState.EphemeralStatusText = sb.ToString();
            }
            GlobalStateViewModel.Instance.PrimaryViewEnabled = true;
            ProgressState.FinalizeProgress("");
            return true;
        }
    }
}
