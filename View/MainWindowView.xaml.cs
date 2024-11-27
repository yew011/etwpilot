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
using EtwPilot.ViewModel;
using Fluent;

namespace EtwPilot
{
    public partial class MainWindow : RibbonWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!GlobalStateViewModel.Instance.g_SessionViewModel.HasActiveLiveSessions())
            {
                return;
            }

            //
            // Abort window close always. Do live session shutdown from dispatcher.
            //
            e.Cancel = true;
            Dispatcher.InvokeAsync( async () =>
            {
                await GlobalStateViewModel.Instance.g_SessionViewModel.StopAllSessionsCommand.ExecuteAsync(null);
                Close();
            });
        }

        private void Backstage_IsOpenChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            var newvalue = (bool)e.NewValue;
            var oldvalue = (bool)e.OldValue;
            if (oldvalue && !newvalue)
            {
                if (!GlobalStateViewModel.Instance.Settings.HasErrors &&
                    GlobalStateViewModel.Instance.Settings.HasUnsavedChanges)
                {
                    GlobalStateViewModel.Instance.Settings.Save(null);
                }
            }
        }
    }
}