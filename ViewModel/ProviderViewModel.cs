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

        #endregion

        public List<ParsedEtwProvider> SelectedProviders;
        private ProviderModel Model;
        private Dictionary<string, ProviderManifestViewModel> m_ManifestCache;

        public ProviderViewModel() : base()
        {
            Providers = new ObservableCollection<ParsedEtwProvider>();
            Model = new ProviderModel(GlobalStateViewModel.Instance.Settings.ProviderCacheLocation);
            m_ManifestCache = new Dictionary<string, ProviderManifestViewModel>();
            SelectedProviders = new List<ParsedEtwProvider>();
            ProvidersWithManifest = new ObservableCollection<ParsedEtwProvider>();

            LoadProvidersCommand = new AsyncRelayCommand(Command_LoadProviders, () => { return true; });
            DumpProviderManifestsCommand = new AsyncRelayCommand(
                Command_DumpProviderManifests, () => { return true; });
            LoadProviderManifestCommand = new AsyncRelayCommand<ParsedEtwProvider>(
                Command_LoadProviderManifest, _ => true);
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
            m_CurrentCommand = DumpProviderManifestsCommand;
            CancelCommandButtonVisibility = Visibility.Visible;
            await DumpProviderManifests(Token);
            CancelCommandButtonVisibility = Visibility.Hidden;
            m_CurrentCommand = null;
        }

        private async Task Command_LoadProviderManifest(ParsedEtwProvider Provider)
        {
            var manifestVm = await GetProviderManifest(Provider.Id);
            if (manifestVm == null)
            {
                return;
            }

            //
            // Create the tab if it doesn't exist; otherwise simply switch to it.
            //
            var tabName = UiHelper.GetUniqueTabName(Provider.Id, "Manifest");
            Func<string, Task<bool>> tabClosedCallback = async delegate (string tabName)
            {
                return true;
            };

            var tab = UiHelper.CreateRibbonContextualTab(
                    tabName,
                    Provider.Name!,
                    0,
                    new Dictionary<string, List<string>>() {
                        { "Export", new List<string> {
                            "ExportJSONButtonStyle",
                            "ExportCSVButtonStyle",
                            "ExportXMLButtonStyle",
                            "ExportClipboardButtonStyle",
                        } },
                    },
                    "ProviderContextTabStyle",
                    "ProviderContextTabText",
                    "ProviderContextTabCloseButton",
                    manifestVm,
                    tabClosedCallback);
            if (tab == null)
            {
                Trace(TraceLoggerType.MainWindow,
                      TraceEventType.Error,
                      $"Unable to create contextual tab {tabName}");
                return;
            }
            GlobalStateViewModel.Instance.CurrentViewModel = manifestVm;
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
            ProgressState.InitializeProgress(1);
            var name = UiHelper.GetUniqueTabName(Id, "Manifest");
            if (m_ManifestCache.ContainsKey(name))
            {
                //
                // This manifest has already been loaded once and bound to an existing tab.
                // The caller has to go find the tab.
                //
                ProgressState.UpdateProgressValue();
                ProgressState.FinalizeProgress($"Loaded cached manifest for provider {Id}");
                return m_ManifestCache[name];
            }
            ProgressState.UpdateProgressMessage("Parsing manifest...");
            var manifest = await Model.GetProviderManifest(Id);
            ProgressState.UpdateProgressValue();
            if (manifest == null)
            {
                ProgressState.FinalizeProgress($"Unable to load manifest for provider {Id}");
                return null;
            }
            ProgressState.FinalizeProgress($"Loaded manifest for provider {Id}");
            var vm = new ProviderManifestViewModel(manifest);
            m_ManifestCache.Add(name, vm);
            return vm;
        }

        private async Task DumpProviderManifests(CancellationToken CancelToken)
        {
            var providers = GetSelectedProviders();
            if (providers.Count == 0)
            {
                return;
            }
            var browser = new System.Windows.Forms.FolderBrowserDialog();
            browser.Description = "Select a location to save manifest data";
            browser.RootFolder = Environment.SpecialFolder.MyComputer;
            var result = browser.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var root = Path.Combine(browser.SelectedPath, "Provider Manifests");
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
                return;
            }

            ProgressState.InitializeProgress(providers.Count);

            var numErrors = 0;
            var i = 1;
            foreach (var provider in providers)
            {
                if (CancelToken.IsCancellationRequested)
                {
                    ProgressState.FinalizeProgress("Operation cancelled");
                    return;
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
                File.WriteAllText(target, manifest.SelectedProviderManifest.ToXml());
                ProgressState.UpdateProgressValue();
            }
            ProgressState.FinalizeProgress(
                $"Dumped {providers.Count} manifests to {root} ({numErrors} errors)");
        }

        public async Task ActivateProviderManifestViewModel(string TabName)
        {
            Debug.Assert(m_ManifestCache.ContainsKey(TabName));
            if (m_ManifestCache.ContainsKey(TabName))
            {
                await m_ManifestCache[TabName].ViewModelActivated();
            }
        }

        public List<ParsedEtwProvider> GetSelectedProviders()
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

        protected override async Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
        {
            ProgressState.InitializeProgress(1);
            var list = GetSelectedProviders();
            if (list.Count == 0)
            {
                ProgressState.FinalizeProgress("No providers available for export.");
                return;
            }
            try
            {
                var result = await DataExporter.Export<List<ParsedEtwProvider>>(
                    list, Format, "Providers", Token);
                ProgressState.UpdateProgressValue();
                ProgressState.FinalizeProgress($"Exported {result.Item1} records to {result.Item2}");
            }
            catch (Exception ex)
            {
                ProgressState.FinalizeProgress($"ExportData failed: {ex.Message}");
            }
        }
    }
}
