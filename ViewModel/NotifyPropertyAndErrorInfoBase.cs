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

namespace EtwPilot.ViewModel
{
    public class NotifyPropertyAndErrorInfoBase : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private Guid Id;

        public NotifyPropertyAndErrorInfoBase()
        {
            Id = Guid.NewGuid();
            Errors = new Dictionary<string, IList<object>>();
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

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

        #endregion

        #region INotifyDataErrorInfo

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

        protected void AddErrorRange(string propertyName, IEnumerable<string> newErrors, bool isWarning = false)
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
                foreach (string error in newErrors)
                {
                    propertyErrors.Add(error);
                }
            }
            else
            {
                foreach (string error in newErrors)
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

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public System.Collections.IEnumerable GetErrors(string propertyName)
          => string.IsNullOrWhiteSpace(propertyName)
            ? Errors.SelectMany(entry => entry.Value)
            : Errors.TryGetValue(propertyName, out IList<object> errors)
              ? (IEnumerable<object>)errors
              : new List<object>();

        public bool HasErrors => Errors.Any();

        protected virtual void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        public void SetParentFormNotifyErrorsChanged(ViewModelBase Parent)
        {
            //
            // Subscribe the parent form to any changes in the error state of the child.
            //
            ErrorsChanged += (sender, args) =>
            {
                //
                // If we (the child) have experienced a change in any error on any property, we
                // will notify the parent of that updated state. We store an error for a single
                // imaginary property that doesn't exist on the parent's form but is used to
                // indicate the fact that we (the child form) have an error somewhere.
                //
                var imaginaryPropertyName = $"{Id}";
                if (HasErrors)
                {
                    if (!Parent.PropertyHasErrors(imaginaryPropertyName))
                    {
                        Parent.AddError($"{imaginaryPropertyName}",
                            $"Sub-form {imaginaryPropertyName} has error(s).");
                    }
                }
                else
                {
                    Parent.ClearErrors($"{imaginaryPropertyName}");
                }
                Parent.OnErrorsChanged($"{imaginaryPropertyName}");
            };
        }
        private Dictionary<string, IList<object>> Errors { get; }

        #endregion
    }
}
