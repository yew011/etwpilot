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

using EtwPilot.Utilities;
using EtwPilot.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace EtwPilot.View
{
    public partial class InsightsView : UserControl
    {
        public InsightsView()
        {
            InitializeComponent();
        }

        private async void PromptTextbox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var vm = UiHelper.GetViewModelFromFrameworkElement<
                MainWindowViewModel>(sender as FrameworkElement);
            if (vm == null)
            {
                return;
            }
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                await vm.m_InsightsViewModel.GenerateCommand.ExecuteAsync(null);
                return;
            }
            vm.m_InsightsViewModel.Prompt += e.Key;
        }

        private async void InsightsControl_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = UiHelper.GetViewModelFromFrameworkElement<
                MainWindowViewModel>(sender as FrameworkElement);
            if (vm == null)
            {
                return;
            }
            await vm.m_InsightsViewModel.Initialize();
        }
    }
}
