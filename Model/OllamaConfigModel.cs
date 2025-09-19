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
using System.Diagnostics;
using System.IO;
using Microsoft.SemanticKernel.Connectors.Ollama;
using EtwPilot.Utilities;
using Newtonsoft.Json;
using OllamaSharp;
using EtwPilot.ViewModel;
using System.Collections.Concurrent;
using Meziantou.Framework.WPF.Collections;
using System.Windows;

namespace EtwPilot.Model
{
    using static EtwPilot.Utilities.TraceLogger;

    public class OllamaModelInfo
    {
        public OllamaSharp.Models.Model ModelObject { get; set; }
        public OllamaSharp.Models.ShowModelResponse ShowModelResponse { get; set; }
    }

    public class OllamaConfigModel : NotifyPropertyAndErrorInfoBase
    {
        #region observable properties

        private string? _EndpointUri;
        public string? EndpointUri
        {
            get => _EndpointUri;
            set
            {
                _EndpointUri = value;
                ClearErrors(nameof(EndpointUri));
                //
                // Note: Further validation on the endpoint is done async from
                // PropertyChanged callback defined in ctor
                //
                AddError(nameof(EndpointUri), "Ollama endpoint awaiting validation...");
                OnPropertyChanged(nameof(EndpointUri));
            }
        }

        private bool _IsEndpointValid;
        [JsonIgnore] // UI only
        public bool IsEndpointValid
        {
            get => _IsEndpointValid;
            set
            {
                _IsEndpointValid = value;
                OnPropertyChanged(nameof(IsEndpointValid));
            }
        }

        private string? _ModelName;
        public string? ModelName
        {
            get => _ModelName;
            set
            {
                _ModelName = value;
                ValidateModelName();
                OnPropertyChanged(nameof(ModelName));
            }
        }

        private string? _TextEmbeddingModelName;
        public string? TextEmbeddingModelName
        {
            get => _TextEmbeddingModelName;
            set
            {
                _TextEmbeddingModelName = value;                
                ValidateTextEmbeddingModelName();
                OnPropertyChanged("TextEmbeddingModelName");
            }
        }

        private Visibility _CancelModelDownloadButtonVisibility;
        [JsonIgnore] // UI Only
        public Visibility CancelModelDownloadButtonVisibility
        {
            get => _CancelModelDownloadButtonVisibility;
            set
            {
                _CancelModelDownloadButtonVisibility = value;
                OnPropertyChanged(nameof(CancelModelDownloadButtonVisibility));
            }
        }

        #endregion

        public OllamaPromptExecutionSettings? PromptExecutionSettings { get; set; }

        [JsonIgnore] // only used by UI
        public ConcurrentObservableCollection<string> ModelNames { get; set; }
        [JsonIgnore] // convenience list
        public ConcurrentDictionary<string, OllamaModelInfo> ModelInfo { get; set; }
        [JsonIgnore] // only used by SettingsFormViewModel.cs
        public ManualResetEvent ValidationCompleteEvent { get; private set; }

        private CancellationTokenSource m_CancellationSource;

        public OllamaConfigModel()
        {
            m_CancellationSource = new CancellationTokenSource();
            ModelNames = new ConcurrentObservableCollection<string>();
            ModelInfo = new ConcurrentDictionary<string, OllamaModelInfo>();
            CancelModelDownloadButtonVisibility = Visibility.Hidden;
            ValidationCompleteEvent = new ManualResetEvent(false);

            //
            // Force initial validation
            //
            EndpointUri = null;
            IsEndpointValid = false;
            ModelName = null;
            TextEmbeddingModelName = null;

            //
            // Add a PropertyChanged listener for Endpoint URI, because it must be validated
            // asynchronously and info needs to be pulled from the ollama endpoint.
            //
            PropertyChanged += async (obj, args) => {
                if (args.PropertyName != nameof(EndpointUri))
                {
                    return;
                }
                ValidationCompleteEvent.Reset();
                if (string.IsNullOrEmpty(EndpointUri))
                {
                    //
                    // ValidationCompleteEvent remains unsignaled because no value has been
                    // provided to attempt validation. This special condition exists to account
                    // for initial null value set in ctor which provides an initial error value
                    // for user to clear.
                    //
                    ResetForm();
                    return;
                }
                ClearErrors(nameof(EndpointUri));
                IsEndpointValid = false;
                if (!await ValidateEndpointUri())
                {
                    AddError(nameof(EndpointUri), "Endpoint URI is invalid");
                    ResetForm();
                    ValidationCompleteEvent.Set();
                    return;
                }
                if (!await LoadModelList())
                {
                    AddError(nameof(EndpointUri), "Unable to load model list");
                    ResetForm();
                    ValidationCompleteEvent.Set();
                    return;
                }
                IsEndpointValid = true;
                //
                // The endpoint is valid, now all settings related to endpoint
                // must be re-evaluated.
                //
                ValidateModelName();
                ValidateTextEmbeddingModelName();
                ValidationCompleteEvent.Set();
            };
        }

        public async Task<bool> LoadModelList()
        {
            if (string.IsNullOrEmpty(EndpointUri))
            {
                Trace(TraceLoggerType.Settings,
                      TraceEventType.Error,
                      $"Endpoint URI {EndpointUri} is not valid");
                return false;
            }
            ModelNames.Clear();
            ModelInfo.Clear();

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                GlobalStateViewModel.Instance.Settings.OperationInProgressVisibility = Visibility.Visible;
                GlobalStateViewModel.Instance.Settings.OperationInProgressMessage =
                    $"Loading model list from endpoint {EndpointUri}...";
            }));

            try
            {
                var uri = new Uri(EndpointUri);
                using (var ollama = new OllamaApiClient(uri))
                {
                    var models = await ollama.ListLocalModelsAsync();
                    foreach (var m in models)
                    {
                        ModelNames.Add(m.Name);
                        var r = await ollama.ShowModelAsync(m.Name);
                        if (!ModelInfo.TryAdd(m.Name, new OllamaModelInfo()
                        {
                            ModelObject = m,
                            ShowModelResponse = r
                        }))
                        {
                            throw new Exception($"Unable to add model named {m.Name}, key already exists");
                        }
                    }
                }
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    GlobalStateViewModel.Instance.Settings.OperationInProgressMessage =
                    $"Loaded {ModelNames.Count} models";
                }));
                return true;
            }
            catch (Exception ex)
            {
                var err = $"Exception loading model list: {ex.Message}";
                Trace(TraceLoggerType.Settings, TraceEventType.Error, err);
                GlobalStateViewModel.Instance.Settings.OperationInProgressMessage = err;
                return false;
            }
        }

        public async Task<bool> DownloadNewModel(string ModelName)
        {
            if (!IsEndpointValid || string.IsNullOrEmpty(EndpointUri))
            {
                Trace(TraceLoggerType.Settings,
                      TraceEventType.Error,
                      $"Endpoint URI {EndpointUri} is not valid");
                return false;
            }
            if (ModelNames.Contains(ModelName))
            {
                return true;
            }

            CancelModelDownloadButtonVisibility = Visibility.Visible;
            GlobalStateViewModel.Instance.Settings.OperationInProgressVisibility = Visibility.Visible;
            GlobalStateViewModel.Instance.Settings.OperationInProgressMessage =
                $"Downloading model {ModelName}...";

            return await Task.Run(async () =>
            {
                long total = 0;
                try
                {
                    var uri = new Uri(EndpointUri);
                    using (var ollama = new OllamaApiClient(uri))
                    {
                        await foreach (var status in ollama.PullModelAsync(ModelName, m_CancellationSource.Token))
                        {
                            if (status == null || m_CancellationSource.IsCancellationRequested)
                            {
                                break;
                            }
                            Application.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                GlobalStateViewModel.Instance.Settings.OperationInProgressMessage =
                                $"{status.Percent:F2}% {status.Status}";
                            }));

                            total += status.Completed;
                        }
                    }
                    //
                    // save current selections
                    //
                    var currentModelName = ModelName;
                    var currentTextEmbeddingModelName = TextEmbeddingModelName;
                    _ = await LoadModelList();
                    this.ModelName = currentModelName;
                    this.TextEmbeddingModelName = currentTextEmbeddingModelName;
                }
                catch (OperationCanceledException)
                {
                    GlobalStateViewModel.Instance.Settings.OperationInProgressMessage = "Download canceled.";
                    return false;
                }
                catch (Exception ex)
                {
                    Trace(TraceLoggerType.Settings,
                          TraceEventType.Error,
                          $"Exception downloading model: {ex.Message}");
                    return false;
                }
                finally
                {
                    CancelModelDownloadButtonVisibility = Visibility.Hidden;
                }
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    GlobalStateViewModel.Instance.Settings.OperationInProgressMessage =
                        $"Model downloaded ({MiscHelper.FormatByteSizeString(total)})";
                }));
                return true;
            });
        }

        public void CancelNewModelDownload()
        {
            if (!m_CancellationSource.Token.CanBeCanceled)
            {
                return;
            }
            GlobalStateViewModel.Instance.Settings.OperationInProgressMessage = "Download cancellation requested...";
            m_CancellationSource.Cancel();
            CancelModelDownloadButtonVisibility = Visibility.Hidden;
        }

        public string? GetModelFileLocation()
        {
            if (string.IsNullOrEmpty(ModelName))
            {
                return null;
            }
            return ParseModelFileLocation(ModelInfo[ModelName].ShowModelResponse.Modelfile);
        }

        #region validation routines

        public async Task<bool> Validate()
        {
            //
            // A special contract exists with EndpointUri - if it is null, no validation
            // was attempted, thus the unset state of the event doesn't matter. We immediately
            // return the error state of the form.
            //
            if (string.IsNullOrEmpty(EndpointUri))
            {
                return !HasErrors;
            }
            //
            // Wait for validation to complete - we are guaranteed this completes.
            //
            return await Task.Run(() =>
            {
                ValidationCompleteEvent.WaitOne(Timeout.Infinite);
                return !HasErrors;
            });
        }

        private async Task<bool> ValidateEndpointUri()
        {
            if (string.IsNullOrEmpty(EndpointUri))
            {
                return false;
            }

            try
            {
                var uri = new Uri(EndpointUri);
                using (var ollama = new OllamaApiClient(uri))
                {
                    return await ollama.IsRunningAsync();
                }
            }
            catch (Exception ex)
            {
                Trace(TraceLoggerType.Settings,
                      TraceEventType.Error,
                      $"Exception validating endpoint URI {EndpointUri}: {ex.Message}");
                return false;
            }
        }

        private void ValidateModelName()
        {
            //
            // Because the runtime config file is tied to the model name being valid,
            // if this validation fails, config file is cleared
            //
            ClearErrors(nameof(ModelName));
            if (string.IsNullOrEmpty(ModelName) || !ModelNames.Contains(ModelName) || !ModelInfo.ContainsKey(ModelName))
            {
                AddError(nameof(ModelName), "Model name is invalid");
                return;
            }

            if (string.IsNullOrEmpty(ParseModelFileLocation(
                ModelInfo[ModelName].ShowModelResponse.Modelfile)))
            {
                AddError(nameof(ModelName), "Model file not found");
                return;
            }
        }

        private void ValidateTextEmbeddingModelName()
        {
            ClearErrors(nameof(TextEmbeddingModelName));
            if (string.IsNullOrEmpty(TextEmbeddingModelName) || !ModelNames.Contains(TextEmbeddingModelName) ||
                !ModelInfo.ContainsKey(TextEmbeddingModelName))
            {
                AddError(nameof(TextEmbeddingModelName), "Text embedding model name is invalid");
                return;
            }
            if (string.IsNullOrEmpty(ParseModelFileLocation(
                ModelInfo[TextEmbeddingModelName].ShowModelResponse.Modelfile)))
            {
                AddError(nameof(TextEmbeddingModelName), "Text embedding model file not found");
                return;
            }
        }

        #endregion


        private string? ParseModelFileLocation(string? Contents)
        {
            //
            // Parse the MODELFILE .. this might be brittle..
            //
            if (string.IsNullOrEmpty(Contents))
            {
                return null;
            }
            var lines = Contents.Split('\n'); // NB: Unix line ending!
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                if (line[0] == '#') // skip comment
                {
                    continue;
                }
                if (line.StartsWith("FROM "))
                {
                    var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 2)
                    {
                        var fullpath = parts[1];
                        if (!File.Exists(fullpath))
                        {
                            return null;
                        }
                        return fullpath;
                    }
                }
            }

            //
            // MODELFILE format has changed. See https://github.com/ollama/ollama/blob/main/docs/modelfile.md
            //
            Debug.Assert(false);
            return null;
        }

        private void ResetForm()
        {
            ModelNames.Clear();
            ModelName = null;
            TextEmbeddingModelName = null;
            IsEndpointValid = false;
            GlobalStateViewModel.Instance.Settings.OperationInProgressMessage = "";
            GlobalStateViewModel.Instance.Settings.OperationInProgressVisibility = Visibility.Hidden;
        }
    }
}
