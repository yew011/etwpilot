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
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using Fluent;
using System.Diagnostics;
using System.Windows.Data;
using EtwPilot.ViewModel;
using etwlib;

namespace EtwPilot.Utilities
{
    public static class UiHelper
    {
        public static T? GetGlobalResource<T>(string Name)
        {
            var window = Application.Current.MainWindow;
            if (window == null)
            {
                return default;
            }
            return (T)window.FindResource(Name);
        }

        public static T? GetViewModelFromFrameworkElement<T>(FrameworkElement? Element)
        {
            if (Element == null)
            {
                return default;
            }
            if (Element.DataContext is not T vm)
            {
                return default;
            }
            return vm;
        }

        public static void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            for (var vis = sender as Visual; vis != null; vis = VisualTreeHelper.GetParent(vis) as Visual)
            {
                if (vis is DataGridRow)
                {
                    var row = (DataGridRow)vis;
                    row.DetailsVisibility = Visibility.Visible;
                    break;
                }
            }
        }

        public static void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            for (var vis = sender as Visual; vis != null; vis = VisualTreeHelper.GetParent(vis) as Visual)
            {
                if (vis is DataGridRow)
                {
                    var row = (DataGridRow)vis;
                    row.DetailsVisibility = Visibility.Hidden;
                    break;
                }
            }
        }

        public static string GetUniqueTabName(Guid Id, string Prefix)
        {
            return $"{Prefix}_{Id}".Replace("-", "_");
        }

        public static bool CreateTabControlTab(
            string TabControlName,
            dynamic TabContent,
            string TabName,
            object? DataContext,
            bool HasCloseButton = true
            )
        {
            var tabControl = FindVisualChild<TabControl>(Application.Current.MainWindow, TabControlName);
            if (tabControl == null)
            {
                Debug.Assert(false);
                return false;
            }

            var existingTabs = tabControl.Items.Cast<TabItem>().ToList();
            var tab = existingTabs.Where(tab => tab.Name == TabName).FirstOrDefault();
            if (tab != null)
            {
                Debug.Assert(false); // this is likely unexpected
                return false;
            }

            //
            // Locate the dynamic contextual tab header style template to apply to the tab.
            //
            var ribbon = FindVisualChild<Ribbon>(
                    Application.Current.MainWindow, "MainWindowRibbon");
            if (ribbon == null)
            {
                Debug.Assert(false);
                return false;
            }

            var styleName = "DynamicTabItemHeaderStyle";
            if (!HasCloseButton)
            {
                styleName = "DynamicTabItemNoCloseButtonHeaderStyle";
            }
            var style = ribbon.FindResource(styleName) as Style;
            if (style == null)
            {
                Debug.Assert(false);
                return false;
            }

            //
            // The TabItem content is wrapped in a ScrollViewer.
            //
            var scrollViewer = new ScrollViewer()
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = TabContent // likely a viewmodel
            };
            var newTab = new TabItem
            {
                Name = TabName,
                Style = style,
                IsSelected = true,
                Content = scrollViewer,
                DataContext = DataContext
            };

            tabControl.Items.Add(newTab);
            return true;
        }

        public static TabItem? CreateEmptyTab(string TabName, string Header, object DataContext)
        {
            //
            // Locate the dynamic contextual tab header style template to apply to the tab.
            //
            var ribbon = FindVisualChild<Ribbon>(
                    Application.Current.MainWindow, "MainWindowRibbon");
            if (ribbon == null)
            {
                Debug.Assert(false);
                return null;
            }

            return new TabItem
            {
                Name = TabName,
                Header = Header,
                DataContext = DataContext
            };
        }

        public static bool CreateRibbonContextualTab(
            string TabName,
            string TabText,
            int ContextualGroupIndex,
            Dictionary<string, List<string>>? GroupButtons,
            object? DataContext)
        {
            var ribbon = FindVisualChild<Ribbon>(
                    Application.Current.MainWindow, "MainWindowRibbon");
            if (ribbon == null)
            {
                Debug.Assert(false);
                return false;
            }
            var existingTab = ribbon.Tabs.FirstOrDefault( t => t.Name == TabName );
            if (existingTab != default)
            {
                existingTab.IsSelected = true;
                return true;
            }

            //
            // Locate the dynamic contextual tab header style template to apply to the tab.
            //
            var style = ribbon.FindResource("DynamicContextualRibbonTabItemHeaderStyle") as Style;
            if (style == null)
            {
                Debug.Assert(false);
                return false;
            }

            Debug.Assert(ContextualGroupIndex < ribbon.ContextualGroups.Count);
            var newTab = new RibbonTabItem
            {
                Name = TabName,
                Style = style,
                IsSelected = true,
                Group = ribbon.ContextualGroups[ContextualGroupIndex],
                DataContext = DataContext
                //
                // Important: Ribbon tab controls contain no content.
                //
            };

            //
            // Bind the tab's visibility property to global state for the contextual tab group.
            // Also note that we disable notify on data errors to remove red box adorner.
            //
            string visibilityParameter;
            if (ContextualGroupIndex == 0)
            {
                visibilityParameter = "ProviderManifestVisible";
            }
            else if (ContextualGroupIndex == 1)
            {
                visibilityParameter = "LiveSessionsVisible";
            }
            else
            {
                Debug.Assert(false);
                return false;
            }

            var binding = new Binding(visibilityParameter);
            binding.Source = GlobalStateViewModel.Instance.g_MainWindowViewModel;
            binding.ValidatesOnNotifyDataErrors = false;
            newTab.SetBinding(RibbonTabItem.VisibilityProperty, binding);

            //
            // Add group buttons if any
            //
            if (GroupButtons != null)
            {
                foreach (var kvp in GroupButtons)
                {
                    var groupName = kvp.Key;
                    var buttonStyles = kvp.Value;
                    var group = new RibbonGroupBox
                    {
                        Header = groupName
                    };
                    foreach (var buttonStyle in buttonStyles)
                    {
                        style = ribbon.FindResource(buttonStyle) as Style;
                        if (style == null)
                        {
                            return false;
                        }
                        var button = new Fluent.Button
                        {
                            Style = style
                        };
                        group.Items.Add(button);
                    }
                    newTab.Groups.Add(group);
                }
            }

            //
            // Add export button groupbox - if underlying VM doesn't implement export
            // data capabilities, these will just be disabled/grayed out.
            //
            newTab.Groups.Add(new Controls.ExportButtonGroup());

            ribbon.Tabs.Add(newTab);
            return true;
        }

        public static bool RemoveRibbonContextualTab(string TabName)
        {
            var ribbon = FindVisualChild<Ribbon>(
                    Application.Current.MainWindow, "MainWindowRibbon");
            if (ribbon == null)
            {
                Debug.Assert(false);
                return false;
            }
            var existingTab = ribbon.Tabs.Where(tab => tab.Name == TabName).FirstOrDefault();
            if (existingTab == default)
            {
                Debug.Assert(false);
                return false;
            }
            existingTab.Template = null; // see https://github.com/dotnet/wpf/issues/6440

            //
            // Before removing the tab, switch off this tab. We do this because otherwise
            // the tabcontrol will switch to the next prior tab, which might not be what
            // we want, and when that tab is activated, it might fire off an async init
            // function for that tab's VM.
            // The number of permanent tabs is 3, or 4 before we remove.
            //
            if (ribbon.Tabs.Count == 4)
            {
                if (TabName.StartsWith("Manifest"))
                {
                    GlobalStateViewModel.Instance.g_MainWindowViewModel.RibbonTabControlSelectedIndex = 0;
                }
                else if (TabName.StartsWith("LiveSession"))
                {
                    GlobalStateViewModel.Instance.g_MainWindowViewModel.RibbonTabControlSelectedIndex = 1;
                }
                else
                {
                    GlobalStateViewModel.Instance.g_MainWindowViewModel.RibbonTabControlSelectedIndex = 0;
                }
            }

            ribbon.Tabs.Remove(existingTab);
            return true;
        }

        public static bool RemoveTab(string TabControlName, string TabName)
        {
            var tabControl = FindVisualChild<TabControl>(Application.Current.MainWindow, TabControlName);
            if (tabControl == null)
            {
                Debug.Assert(false);
                return false;
            }
            var tabs = tabControl.Items.Cast<TabItem>().ToList();
            var existingTab = tabs.Where(tab => tab.Name == TabName).FirstOrDefault();
            if (existingTab == default)
            {
                Debug.Assert(false);
                return false;
            }
            existingTab.Template = null; // see https://github.com/dotnet/wpf/issues/6440
            tabControl.Items.Remove(existingTab);
            return true;
        }

        private static T? FindVisualChild<T>(DependencyObject parent, string? ChildName) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            T? foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                T? childType = child as T;
                if (childType == null)
                {
                    foundChild = FindVisualChild<T>(child, ChildName);
                    if (foundChild != null)
                    {
                        break;
                    }
                }
                else if (!string.IsNullOrEmpty(ChildName))
                {
                    var frameworkElement = child as FrameworkElement;
                    if (frameworkElement != null && frameworkElement.Name == ChildName)
                    {
                        foundChild = (T)child;
                        break;
                    }
                    foundChild = FindVisualChild<T>(child, ChildName);
                    if (foundChild != null)
                    {
                        break;
                    }
                }
                else
                {
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }

        public static T? FindVisualParent<T>(DependencyObject current, string? ParentName) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    if (!string.IsNullOrEmpty(ParentName))
                    {
                        var frameworkElement = current as FrameworkElement;
                        if (frameworkElement != null && frameworkElement.Name == ParentName)
                        {
                            return current as T;
                        }
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
    }
}
