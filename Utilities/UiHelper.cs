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

namespace EtwPilot.Utilities
{
    public static class UiHelper
    {
        public static T? GetGlobalResource<T>(string Name)
        {
            var window = Application.Current.MainWindow;
            return (T)window.FindResource(Name);
        }

        public static T? FindChild<T>(DependencyObject parent, string ChildName) where T : DependencyObject
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
                    foundChild = FindChild<T>(child, ChildName);
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
                    foundChild = FindChild<T>(child, ChildName);
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

        public static IEnumerable<T> FindLogicalChildren<T>(DependencyObject depObj, string ChildName) where T : DependencyObject
        {
            if (depObj != null)
            {
                foreach (object rawChild in LogicalTreeHelper.GetChildren(depObj))
                {
                    if (rawChild is DependencyObject)
                    {
                        DependencyObject child = (DependencyObject)rawChild;
                        if (child is T)
                        {
                            var frameworkElement = child as FrameworkElement;
                            if (frameworkElement != null && frameworkElement.Name == ChildName)
                            {
                                yield return (T)child;
                            }
                        }

                        foreach (T childOfChild in FindLogicalChildren<T>(child, ChildName))
                        {
                            yield return childOfChild;
                        }
                    }
                }
            }
        }

        public static T? FindLogicalParent<T>(DependencyObject depObj, string ParentName) where T : DependencyObject
        {
            if (depObj != null)
            {
                var parent = LogicalTreeHelper.GetParent(depObj);
                if (parent is null)
                {
                    return null;
                }
                if (parent is DependencyObject)
                {
                    if (parent is T)
                    {
                        var frameworkElement = parent as FrameworkElement;
                        if (frameworkElement != null && frameworkElement.Name == ParentName)
                        {
                            return (T)parent;
                        }
                    }
                }
                return FindLogicalParent<T>(parent, ParentName);
            }
            return null;
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
                    row.DetailsVisibility = Visibility.Collapsed;
                    break;
                }
            }
        }

        public static string GetUniqueTabName(Guid Id, string Prefix)
        {
            return $"{Prefix}_{Id}".Replace("-", "_");
        }

        public static void FixupDynamicTab(
            TabControl TabControl,
            TabItem NewTab,
            string TabTitle,
            string TabHeaderTextBlockName,
            string TabCloseButtonName,
            Action? TabClosedCallback)
        {
            var headerTextbox = FindChild<TextBlock>(NewTab, TabHeaderTextBlockName);
            var headerCloseButton = FindChild<Button>(NewTab, TabCloseButtonName);
            if (headerTextbox == null || headerCloseButton == null)
            {
                return;
            }

            //
            // Add a handler for this tab close button and assign unique tag to the tab
            // itself, so we can easily remove it.
            //
            headerCloseButton.Tag = NewTab.Name;
            headerCloseButton.Click += (s, e) =>
            {
                //
                // Locate the button, corresponding tab name, and the containing tab control.
                //
                var button = s as Button;
                if (button == null)
                {
                    return;
                }
                var tabName = button.Tag as string;
                if (tabName == null)
                {
                    return;
                }

                //
                // Find and remove the tab, but keep the VM around in our cache to make
                // it faster next time.
                //
                var existingTabs = TabControl.Items.Cast<TabItem>().ToList();
                var tab = existingTabs.Where(t => t.Name == tabName).FirstOrDefault();
                if (tab == null)
                {
                    return;
                }
                TabControl.Items.Remove(tab);
                tab.Template = null; // see https://github.com/dotnet/wpf/issues/6440
                TabClosedCallback?.Invoke();
            };

            var title = $"{TabTitle}";
            var header = title;
            if (title.Length > 50)
            {
                int start = title.Length - 15;
                header = $"{title.Substring(0, 15)}...{title.Substring(start - 1, 15)}";
            }

            headerTextbox.Text = header;
        }

        public static void FixupDynamicRibbonTab(
            Fluent.Ribbon TabControl,
            Fluent.RibbonTabItem NewTab,
            string TabTitle,
            string TabHeaderTextBlockName,
            string TabCloseButtonName,
            Action? TabClosedCallback)
        {
            var headerTextbox = FindChild<TextBlock>(NewTab, TabHeaderTextBlockName);
            var headerCloseButton = FindChild<Button>(NewTab, TabCloseButtonName);
            if (headerTextbox == null || headerCloseButton == null)
            {
                return;
            }

            //
            // Add a handler for this tab close button and assign unique tag to the tab
            // itself, so we can easily remove it.
            //
            headerCloseButton.Tag = NewTab.Name;
            headerCloseButton.Click += (s, e) =>
            {
                //
                // Locate the button, corresponding tab name, and the containing tab control.
                //
                var button = s as Button;
                if (button == null)
                {
                    return;
                }
                var tabName = button.Tag as string;
                if (tabName == null)
                {
                    return;
                }

                //
                // Find and remove the tab, but keep the VM around in our cache to make
                // it faster next time.
                //
                var tab = TabControl.Tabs.Where(t => t.Name == tabName).FirstOrDefault();
                if (tab == null)
                {
                    return;
                }
                TabControl.Tabs.Remove(tab);
                tab.Template = null; // see https://github.com/dotnet/wpf/issues/6440
                TabClosedCallback?.Invoke();
            };

            var title = $"{TabTitle}";
            var header = title;
            if (title.Length > 50)
            {
                int start = title.Length - 15;
                header = $"{title.Substring(0, 15)}...{title.Substring(start - 1, 15)}";
            }

            headerTextbox.Text = header;
        }
    }
}
