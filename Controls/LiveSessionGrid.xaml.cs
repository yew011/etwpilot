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
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace EtwPilot.Controls
{
    public partial class LiveSessionGrid : UserControl
    {
        public LiveSessionGrid()
        {
            InitializeComponent();
        }

        private void ProviderDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            //
            // Here is the UI control / Data context hierarchy:
            //
            //      Control        DataContext
            //      --------------------------------
            //      TabControl  -> LiveSession  (the trace session)
            //      TabItem     -> ProviderTraceData (a provider in the trace)
            //      DataGrid    -> ProviderTraceData (same as above)
            //      DataGridRow -> ParsedEtwEvent (a single event in the provider's trace data)
            //
            var datagrid = sender as DataGrid;
            if (datagrid == null)
            {
                Debug.Assert(false);
                return;
            }
            var tabcontrol = UiHelper.FindVisualParent<TabControl>(datagrid, "LiveSessionProviderDataTabControl");
            if (tabcontrol == null)
            {
                //
                // Sometimes this callback is invoked before View has initialized (bug?)
                // do not assert.
                //
                return;
            }
            var vm = tabcontrol.DataContext as LiveSessionViewModel;
            if (vm == null)
            {
                Debug.Assert(false);
                return;
            }
            var providerData = datagrid.DataContext as ProviderTraceData;
            if (providerData == null)
            {
                return;
            }

            //
            // Remove the column if not in the list specified for this provider.
            //
            var columnHeader = e.Column.Header.ToString();
            var matchedColumn = providerData.Columns.Where(c => c.UniqueName == columnHeader).FirstOrDefault();
            if (matchedColumn == default || matchedColumn == null)
            {
                e.Cancel = true;
                return;
            }

            //
            // Check for a custom formatter for this column's data.
            //
            if (matchedColumn.Formatter != null)
            {
                var textColumn = e.Column as DataGridTextColumn;
                if (textColumn == null)
                {
                    Debug.Assert(false);
                    return;
                }

                //
                // Create a new DataGridTemplateColumn to replace the DataGridTextColumn.
                // This provides us more flexibility in manipulating the cell's TextBlock.
                //
                var templateColumn = new DataGridTemplateColumn()
                {
                    Header = matchedColumn.Name,
                };

                //
                // Create an async formatter for this column. All cells in this column will use
                // this one formatter.
                //
                var asyncFormatter = vm.m_FormatterLibrary.GetAsyncFormatter(matchedColumn.Formatter);
                if (asyncFormatter == null)
                {
                    Debug.Assert(false);
                    return;
                }

                var cellTemplate = new DataTemplate();
                var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
                textBlockFactory.SetValue(TextBlock.TagProperty, asyncFormatter);
                textBlockFactory.SetBinding(TextBlock.TextProperty, textColumn.Binding);
                textBlockFactory.SetBinding(AsyncFormatter.ObservedTextProperty, textColumn.Binding);
                cellTemplate.VisualTree = textBlockFactory;
                templateColumn.CellTemplate = cellTemplate;
                e.Column = templateColumn;
            }
        }
    }
}
