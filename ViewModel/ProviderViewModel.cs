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
using etwlib;
using EtwPilot.Model;
using EtwPilot.Utilities;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace EtwPilot.ViewModel
{
    internal class ProviderViewModel : ViewModelBase
    {
        public List<ParsedEtwProvider> SelectedProviders;
        private ProviderModel Model;
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

        private Dictionary<string, ProviderManifestViewModel> m_ManifestCache;

        public ProviderViewModel()
        {
            Providers = new ObservableCollection<ParsedEtwProvider>();
            Model = new ProviderModel(StateManager);
            m_ManifestCache = new Dictionary<string, ProviderManifestViewModel>();
            SelectedProviders = new List<ParsedEtwProvider>();
        }

        public async Task LoadProviders()
        {
            var providers = await Model.GetProviders();
            if (providers == null)
            {
                return;
            }
            Providers.Clear();
            m_ManifestCache.Clear();
            if (StateManager.SettingsModel.HideProvidersWithoutManifest)
            {
                var filtered = providers.Where(p => p.HasManifest).Cast<ParsedEtwProvider>().ToList();
                filtered.ForEach(f => Providers.Add(f));
            }
            else
            {
                providers.ForEach(f => Providers.Add(f));
            }
        }

        public async Task<ProviderManifestViewModel?> LoadProviderManifest(Guid Id)
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

        public ProviderManifestViewModel? GetVmForTab(string TabName)
        {
            Debug.Assert(m_ManifestCache.ContainsKey(TabName));
            if (m_ManifestCache.ContainsKey(TabName))
            {
                return m_ManifestCache[TabName];
            }
            return null;
        }

    }
}
