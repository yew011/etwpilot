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
using EtwPilot.Model;
using EtwPilot.Utilities;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace EtwPilot.ViewModel
{
    using static EtwPilot.Utilities.TraceLogger;

    public class ProviderViewModel : ViewModelBase
    {
        #region observable properties

        private ObservableCollection<ParsedEtwProvider> _providers;
        public ObservableCollection<ParsedEtwProvider> Providers
        {
            get => _providers;
            set
            {
                if (_providers != value)
                {
                    _providers = value;
                    OnPropertyChanged("Providers");
                }
            }
        }

        private ObservableCollection<ParsedEtwProvider> _ProvidersWithManifest;
        public ObservableCollection<ParsedEtwProvider> ProvidersWithManifest
        {
            get => _ProvidersWithManifest;
            set
            {
                if (_ProvidersWithManifest != value)
                {
                    _ProvidersWithManifest = value;
                    OnPropertyChanged("ProvidersWithManifest");
                }
            }
        }

        #endregion

        #region commands

        public AsyncRelayCommand LoadProvidersCommand { get; set; }
        public AsyncRelayCommand DumpProviderManifestsCommand { get; set; }
        public AsyncRelayCommand<ParsedEtwProvider> LoadProviderManifestCommand { get; set; }
        public RelayCommand<ProviderManifestViewModel> CloseDynamicTab { get; set; }

        #endregion

        public List<ParsedEtwProvider> SelectedProviders;
        private ProviderModel Model;
        private Dictionary<string, ProviderManifestViewModel> m_ManifestCache;

        public ProviderViewModel() : base()
        {
            Providers = new ObservableCollection<ParsedEtwProvider>();
            Model = new ProviderModel();
            m_ManifestCache = new Dictionary<string, ProviderManifestViewModel>();
            SelectedProviders = new List<ParsedEtwProvider>();
            ProvidersWithManifest = new ObservableCollection<ParsedEtwProvider>();

            LoadProvidersCommand = new AsyncRelayCommand(Command_LoadProviders, () => { return true; });
            DumpProviderManifestsCommand = new AsyncRelayCommand(
                Command_DumpProviderManifests, () => { return true; });
            LoadProviderManifestCommand = new AsyncRelayCommand<ParsedEtwProvider>(
                Command_LoadProviderManifest, _ => true);
            CloseDynamicTab = new RelayCommand<ProviderManifestViewModel>(
                Command_CloseDynamicTab, _ => true);
        }

        public override async Task ViewModelActivated()
        {
            if (Providers.Count == 0)
            {
                await LoadProviders();
            }
            GlobalStateViewModel.Instance.CurrentViewModel = this;
        }

        private async Task Command_LoadProviders()
        {
            await LoadProviders();
        }

        private async Task Command_DumpProviderManifests(CancellationToken Token)
        {
            var location = await DumpProviderManifests(Token);
            if (location == null)
            {
                return;
            }
            ProgressState.SetFollowupActionCommand.Execute(
                new FollowupAction()
                {
                    Title = "Open",
                    Callback = new Action<dynamic>((args) =>
                    {
                        var psi = new ProcessStartInfo();
                        psi.FileName = location;
                        psi.UseShellExecute = true;
                        Process.Start(psi);
                    }),
                    CallbackArgument = null
                });
        }

        private async Task Command_LoadProviderManifest(ParsedEtwProvider? Provider)
        {
            if (Provider == null)
            {
                Debug.Assert(false);
                return;
            }
            var manifestVm = await GetProviderManifest(Provider.Id);
            if (manifestVm == null)
            {
                return;
            }

            //
            // Create a UI contextual tab for this provider manifest.
            //
            // Note: Fluent:Ribbon contextual tabs are unique in that we have to create
            // them in code because Fluent developer refuses to implement MVVM template
            // support in the library - there is no way to dynamically create tabs via
            // MVVM templates. Other places in EtwPilot use .NET's builtin TabControl
            // which has full data/content/item template support to dynamically create tabs.
            //
            var tabName = UiHelper.GetUniqueTabName(Provider.Id, "Manifest");
            if (!UiHelper.CreateRibbonContextualTab(
                    tabName,
                    Provider.Name!,
                    0,
                    null,
                    manifestVm))
            {
                Trace(TraceLoggerType.MainWindow,
                      TraceEventType.Error,
                      $"Unable to create contextual tab {tabName}");
                return;
            }
            await manifestVm.ViewModelActivated();
        }

        public void Command_CloseDynamicTab(ProviderManifestViewModel? ViewModel)
        {
            if (ViewModel == null)
            {
                Debug.Assert(false);
                return;
            }
            //
            // Remove the tab from the tab control.
            //
            var tabName = UiHelper.GetUniqueTabName(ViewModel.m_Manifest.Provider.Id, "Manifest");
            if (!UiHelper.RemoveRibbonContextualTab(tabName))
            {
                return;
            }
        }

        public override async Task Command_SettingsChanged()
        {
            var changed = GlobalStateViewModel.Instance.Settings.ChangedProperties;
            if (changed.Contains(nameof(SettingsFormViewModel.HideProvidersWithoutManifest)) ||
                changed.Contains(nameof(SettingsFormViewModel.ProviderCacheLocation)))
            {
                //
                // Refresh the view with the new setting
                //
                await LoadProviders();
            }
        }

        private async Task LoadProviders()
        {
            ProgressState.InitializeProgress(2);
            var providers = await Model.GetProviders();
            ProgressState.UpdateProgressValue();
            if (providers == null)
            {
                ProgressState.FinalizeProgress($"No providers available");
                return;
            }
            Providers.Clear();
            m_ManifestCache.Clear();

            var filtered = providers.Where(p => p.HasManifest).Cast<ParsedEtwProvider>().ToList();
            filtered.ForEach(f => ProvidersWithManifest.Add(f));
            ProgressState.UpdateProgressValue();

            if (GlobalStateViewModel.Instance.Settings.HideProvidersWithoutManifest)
            {
                filtered.ForEach(f => Providers.Add(f));
                int hidden = providers.Count - filtered.Count;
                ProgressState.FinalizeProgress($"Loaded {Providers.Count} providers (hiding {hidden} with no manifest).");
            }
            else
            {
                providers.ForEach(f => Providers.Add(f));
                ProgressState.FinalizeProgress($"Loaded {Providers.Count} providers.");
            }
        }

        public async Task<ProviderManifestViewModel?> GetProviderManifest(Guid Id)
        {
            var name = UiHelper.GetUniqueTabName(Id, "Manifest");
            if (m_ManifestCache.ContainsKey(name))
            {
                //
                // This manifest has already been loaded once and bound to an existing tab.
                // The caller has to go find the tab.
                //
                return m_ManifestCache[name];
            }
            var manifest = await Model.GetProviderManifest(Id);
            if (manifest == null)
            {
                return null;
            }
            var vm = new ProviderManifestViewModel(manifest);
            m_ManifestCache.Add(name, vm);
            return vm;
        }

        private async Task<string?> DumpProviderManifests(CancellationToken CancelToken)
        {
            var providers = GetAvailableProviders();
            if (providers.Count == 0)
            {
                return null;
            }
            var browser = new OpenFolderDialog();
            browser.Title = "Select a location to save manifest data";
            browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return null;
            }

            var root = Path.Combine(browser.FolderName, "Provider Manifests");
            try
            {
                Directory.CreateDirectory(root);
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.Settings,
                      TraceEventType.Warning,
                      $"Unable to create root directory " +
                      $"'{root}': {ex.Message}");
                ProgressState.FinalizeProgress($"Unable to create root directory {root}: {ex.Message}");
                return null;
            }

            ProgressState.InitializeProgress(providers.Count);
            ProgressState.m_CurrentCommand = DumpProviderManifestsCommand;
            ProgressState.CancelCommandButtonVisibility = Visibility.Visible;

            var numErrors = 0;
            var i = 1;
            foreach (var provider in providers)
            {
                if (CancelToken.IsCancellationRequested)
                {
                    ProgressState.FinalizeProgress("Operation cancelled");
                    return null;
                }
                ProgressState.UpdateProgressMessage(
                    $"Dumping manifest for provider {provider.Name} ({i++} of {providers.Count})...");
                var target = Path.Combine(root, $"{provider.Id}.xml");
                var manifest = await GetProviderManifest(provider.Id);
                if (manifest == null)
                {
                    numErrors++;
                    ProgressState.UpdateProgressValue();
                    continue;
                }
                File.WriteAllText(target, manifest.m_Manifest.ToXml());
                ProgressState.UpdateProgressValue();
            }
            ProgressState.FinalizeProgress(
                $"Dumped {providers.Count} manifests to {root} ({numErrors} errors)");
            ProgressState.CancelCommandButtonVisibility = Visibility.Hidden;
            ProgressState.m_CurrentCommand = null;
            return root;
        }

        public async Task ActivateProviderManifestViewModel(string TabName)
        {
            Debug.Assert(m_ManifestCache.ContainsKey(TabName));
            if (m_ManifestCache.ContainsKey(TabName))
            {
                await m_ManifestCache[TabName].ViewModelActivated();
            }
        }

        protected override async Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
        {
            ProgressState.InitializeProgress(1);
            var list = GetAvailableProviders();
            if (list.Count == 0)
            {
                ProgressState.FinalizeProgress("No providers available for export.");
                return;
            }
            try
            {
                var result = await DataExporter.Export<List<ParsedEtwProvider>>(
                    list, Format, "Providers", Token);
                if (result.Item1 == 0 || result.Item2 == null)
                {
                    ProgressState.FinalizeProgress("");
                    return;
                }
                ProgressState.UpdateProgressValue();
                ProgressState.FinalizeProgress($"Exported {result.Item1} records to {result.Item2}");
                if (Format != DataExporter.ExportFormat.Clip)
                {
                    ProgressState.SetFollowupActionCommand.Execute(
                        new FollowupAction()
                        {
                            Title = "Open",
                            Callback = new Action<dynamic>((args) =>
                            {
                                var psi = new ProcessStartInfo();
                                psi.FileName = result.Item2;
                                psi.UseShellExecute = true;
                                Process.Start(psi);
                            }),
                            CallbackArgument = null
                        });
                }
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"ExportData failed: {ex.Message}");
            }
        }

        private List<ParsedEtwProvider> GetAvailableProviders()
        {
            if (SelectedProviders == null || SelectedProviders.Count == 0)
            {
                if (Providers == null || Providers.Count == 0)
                {
                    return new List<ParsedEtwProvider>();
                }
                return Providers.ToList();
            }
            return SelectedProviders;
        }
    }
}
