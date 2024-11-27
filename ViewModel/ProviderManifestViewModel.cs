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
using EtwPilot.Utilities;
using static EtwPilot.Utilities.DataExporter;

namespace EtwPilot.ViewModel
{
    public class ProviderManifestViewModel : ViewModelBase
    {
        #region observable properties

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

        public ProviderManifestViewModel(ParsedEtwManifest Manifest) : base()
        {
            SelectedProviderManifest = Manifest;
        }

        public override Task ViewModelActivated()
        {
            GlobalStateViewModel.Instance.CurrentViewModel = this;
            return Task.CompletedTask;
        }

        protected override async Task ExportData(DataExporter.ExportFormat Format, CancellationToken Token)
        {
            ProgressState.InitializeProgress(1);
            try
            {
                if (Format == ExportFormat.Csv)
                {
                    //
                    // TODO: ParsedEtwProviderManifest has sub-structures with different
                    // formats that must be written to different CSVs.
                    //
                    throw new Exception("Exporting a provider manifest to CSV isn't currently supported.");
                }
                else if (Format == ExportFormat.Xml)
                {
                    //
                    // TODO: ParsedEtwProviderManifest has several dictionaries embedded
                    // inside it - these cannot be serialized natively. They should either be
                    // replaced with a custom serializable dictionary or annotated with
                    // DataContract (see https://stackoverflow.com/questions/495647)
                    //
                    throw new Exception("Exporting a provider manifest to XML isn't currently supported.");
                }
                var result = await DataExporter.Export<ParsedEtwManifest>(
                        SelectedProviderManifest, Format, "ProviderManifest", Token);
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
