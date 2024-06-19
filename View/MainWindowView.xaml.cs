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
using EtwPilot.ViewModel;
using System.Windows.Controls;
using Fluent;

namespace EtwPilot
{
    using static EtwPilot.Utilities.TraceLogger;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            //
            // Always load settings first, to setup working dir.
            //
            /*g_Settings = Settings.LoadDefault();

            //
            // Initialize traces and set trace levels
            //
            etwlib.TraceLogger.Initialize();
            etwlib.TraceLogger.SetLevel(g_Settings.TraceLevelEtwlib);
            TraceLogger.Initialize();
            TraceLogger.SetLevel(g_Settings.TraceLevelApp);
            symbolresolver.TraceLogger.Initialize();
            symbolresolver.TraceLogger.SetLevel(g_Settings.TraceLevelSymbolresolver);
            */
            Trace(TraceLoggerType.MainWindow,
                  TraceEventType.Information,
                  "MainWindow opened");
        }

        private void MainWindowRibbon_SelectedTabChanged(object sender, SelectionChangedEventArgs e)
        {
            var tab = sender as RibbonTabItem;
            if (tab == null)
            {
                return;
            }
            
            var vm = tab.DataContext as MainWindowViewModel;

            if (tab.Name == "ProvidersTab")
            {
                vm!.ShowProviderViewModel();
            }
        }
    }
}