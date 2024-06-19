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
using System.Collections.ObjectModel;

namespace EtwPilot.ViewModel
{
    class ProviderViewModel : ViewModelBase
    {
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

        public ProviderViewModel(ProgressState ProgressState)
        {
            _providers = new ObservableCollection<ParsedEtwProvider>();
            Model = new ProviderModel(ProgressState);
        }

        public async Task LoadProviders()
        {
            var providers = await Model.GetProviders();
            if (providers == null)
            {
                return;
            }
            Providers.Clear();
            //if (g_Settings.HideProvidersWithoutManifest)
            if (true)
            {
                var filtered = providers.Where(p => p.HasManifest).Cast<ParsedEtwProvider>().ToList();
                filtered.ForEach(f => Providers.Add(f));
            }
            else
            {
                providers.ForEach(f => Providers.Add(f));
            }
        }
    }
}
