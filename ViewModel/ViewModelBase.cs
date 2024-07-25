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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace EtwPilot.ViewModel
{
    internal abstract class ViewModelBase : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ViewModelBase()
        {
            Errors = new Dictionary<string, IList<object>>();
            ValidationRules = new Dictionary<string, HashSet<ValidationRule>>();
        }

        private static StateManager _stateManager = new StateManager();
        public StateManager StateManager
        {
            get { return _stateManager; }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected void AddError(string propertyName, object newError)
        {
            if (!Errors.TryGetValue(propertyName, out IList<object> propertyErrors))
            {
                propertyErrors = new List<object>();
                Errors.Add(propertyName, propertyErrors);
            }
            propertyErrors.Insert(0, newError);
            OnErrorsChanged(propertyName);
        }

        protected void AddErrorRange(string propertyName, IEnumerable<object> newErrors, bool isWarning = false)
        {
            if (!newErrors.Any())
            {
                return;
            }

            if (!Errors.TryGetValue(propertyName, out IList<object> propertyErrors))
            {
                propertyErrors = new List<object>();
                Errors.Add(propertyName, propertyErrors);
            }

            if (isWarning)
            {
                foreach (object error in newErrors)
                {
                    propertyErrors.Add(error);
                }
            }
            else
            {
                foreach (object error in newErrors)
                {
                    propertyErrors.Insert(0, error);
                }
            }

            OnErrorsChanged(propertyName);
        }

        protected bool ClearErrors(string propertyName = "")
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                Errors.Clear();
                OnErrorsChanged(propertyName);
                return true;
            }
            if (Errors.Remove(propertyName))
            {
                OnErrorsChanged(propertyName);
                return true;
            }
            return false;
        }

        public bool PropertyHasErrors(string propertyName) =>
            Errors.TryGetValue(propertyName, out IList<object> propertyErrors) && propertyErrors.Any();

        #region INotifyDataErrorInfo implementation

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public System.Collections.IEnumerable GetErrors(string propertyName)
          => string.IsNullOrWhiteSpace(propertyName)
            ? Errors.SelectMany(entry => entry.Value)
            : Errors.TryGetValue(propertyName, out IList<object> errors)
              ? (IEnumerable<object>)errors
              : new List<object>();

        public bool HasErrors => Errors.Any();

        #endregion

        protected virtual void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private Dictionary<string, IList<object>> Errors { get; }
        private Dictionary<string, HashSet<ValidationRule>> ValidationRules { get; }
    }

    internal class StateManager
    {
        public ProgressState ProgressState { get; set; }
        public Model.SettingsModel SettingsModel { get; set; }
        public StateManager() { }
    }

    public class ProgressState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private Visibility _visible;
        public Visibility Visible
        {
            get => _visible;
            set { _visible = value; OnPropertyChanged("Visible"); }
        }

        private int _ProgressValue;
        public int ProgressValue
        {
            get => _ProgressValue;
            set { _ProgressValue = value; OnPropertyChanged("ProgressValue"); }
        }

        public string _StatusText;
        public string StatusText
        {
            get => _StatusText;
            set { _StatusText = value; OnPropertyChanged("StatusText"); }
        }

        public int _ProgressMax;
        public int ProgressMax
        {
            get => _ProgressMax;
            set { _ProgressMax = value; OnPropertyChanged("ProgressMax"); }
        }

        public ProgressState()
        {
            FinalizeProgress();
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void InitializeProgress(int Max)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressMax = Max;
                StatusText = "";
                ProgressValue = 0;
                Visible = Visibility.Visible;
            }));
        }

        public void FinalizeProgress()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressMax = 0;
                ProgressValue = 0;
                Visible = Visibility.Hidden;
            }));
        }

        public void UpdateProgressMessage(string Text)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                StatusText = Text;
            }));
        }

        public void UpdateProgressValue()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressValue++;
            }));
        }

        public bool TaskInProgress()
        {
            return Visible == Visibility.Visible;
        }
    }
}
