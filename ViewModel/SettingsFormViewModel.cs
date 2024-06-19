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
using System.IO;
using EtwPilot.Model;
using Newtonsoft.Json;

namespace EtwPilot.ViewModel
{
    internal class SettingsFormViewModel : ViewModelBase
    {
        private SettingsModel _m_SettingsModel;
        public SettingsModel m_SettingsModel
        {
            get => _m_SettingsModel;
            set
            {
                if (_m_SettingsModel != value)
                {
                    _m_SettingsModel = value;
                    StateManager.SettingsModel = value;
                    OnPropertyChanged("m_SettingsModel");
                }
            }
        }

        public SettingsFormViewModel()
        {
            StateManager.SettingsModel = new SettingsModel();
            StateManager.SettingsModel.SetDefaultEtwColumns();
            m_SettingsModel = StateManager.SettingsModel;
        }

        public void Save(string Target = null)
        {
            string target = Target;

            if (string.IsNullOrEmpty(target))
            {
                target = SettingsModel.DefaultSettingsFileLocation;
            }

            string json;
            try
            {
                m_SettingsModel.Validate();
                json = JsonConvert.SerializeObject(m_SettingsModel, Formatting.Indented);
                File.WriteAllText(target, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not serialize the Settings object " +
                    $"to JSON: {ex.Message}");
            }
        }

        public void Load(string Location)
        {
            if (!File.Exists(Location))
            {
                throw new Exception("File does not exist");
            }

            try
            {
                var json = File.ReadAllText(Location);
                var obj = (SettingsModel)JsonConvert.DeserializeObject(json, typeof(SettingsModel))!;
                obj.Validate();
                m_SettingsModel = obj; // fire handlers
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not deserialize settings: {ex.Message}");
            }
        }

        public void LoadDefault()
        {
            var target = Path.Combine(SettingsModel.DefaultWorkingDirectory, SettingsModel.DefaultSettingsFileName);
            if (File.Exists(target))
            {
                Load(target);
            }
        }
    }
}
