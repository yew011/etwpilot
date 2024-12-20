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
using System.Diagnostics;
using static EtwPilot.Utilities.DataExporter;

namespace EtwPilot.ViewModel
{
    public class ProviderManifestViewModel : ViewModelBase
    {
        #region observable properties

        private int _TabControlSelectedIndex;
        public int TabControlSelectedIndex
        {
            get => _TabControlSelectedIndex;
            set
            {
                if (_TabControlSelectedIndex != value)
                {
                    _TabControlSelectedIndex = value;
                    OnPropertyChanged("TabControlSelectedIndex");
                }
            }
        }

        #endregion

        public ParsedEtwManifest m_Manifest { get; set; }
        public string m_TabText { get; set; }

        public ProviderManifestViewModel(ParsedEtwManifest Manifest) : base()
        {
            m_Manifest = Manifest;
            m_TabText = string.IsNullOrEmpty(Manifest.Provider.Name) ? "<unnamed>" :
                Manifest.Provider.Name;
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
                        m_Manifest, Format, "ProviderManifest", Token);
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
    }
}
