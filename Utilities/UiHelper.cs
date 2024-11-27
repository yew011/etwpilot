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


namespace EtwPilot.Utilities
{
    using static TraceLogger;
    using Ribbon = Fluent.Ribbon;
    using Button = System.Windows.Controls.Button;

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

        public static T? FindChild<T>(DependencyObject parent, string? ChildName) where T : DependencyObject
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

        public static T? FindControlFromDataTemplate<T>(DependencyObject Control, string ChildControlName)
        {
            //
            // Locates an instance of a control created as a result of a DataTemplate.
            //
            // See: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-find-datatemplate-generated-elements?view=netframeworkdesktop-4.8
            //
            var contentPresenter = FindChild<ContentPresenter>(Control, null);
            if (contentPresenter == null)
            {
                return default(T);
            }
            var dataTemplate = contentPresenter.ContentTemplate;
            return (T)dataTemplate.FindName(ChildControlName, contentPresenter);
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

        public static RibbonTabItem? CreateRibbonContextualTab(
            string TabName,
            string TabText,
            int ContextualGroupIndex,
            Dictionary<string, List<string>>? GroupButtons,
            string TabStyleTemplateName,
            string TabHeaderTextBlockName,
            string TabCloseButtonName,
            object? DataContext,
            Func<string, Task<bool>> TabClosedCallback)
        {
            var ribbon = FindChild<Ribbon>(
                    Application.Current.MainWindow, "MainWindowRibbon");
            if (ribbon == null)
            {
                Trace(TraceLoggerType.UiHelper,
                      TraceEventType.Error,
                      $"Unable to locate MainWindowRibbon");
                return null;
            }
            if (ribbon.Tabs.Any(tab => tab.Name == TabName))
            {
                Debug.Assert(false);
                Trace(TraceLoggerType.UiHelper,
                      TraceEventType.Error,
                      $"Cannot create tab, name {TabName} already exists.");
                return null;
            }

            //
            // Locate the style template to apply to the tab.
            //
            var style = ribbon.FindResource(TabStyleTemplateName) as Style;
            if (style == null)
            {
                Trace(TraceLoggerType.UiHelper,
                      TraceEventType.Error,
                      $"Unable to locate tab style {TabStyleTemplateName}");
                return null;
            }

            Debug.Assert(ContextualGroupIndex < ribbon.ContextualGroups.Count);
            var newTab = new RibbonTabItem
            {
                Name = TabName,
                Style = style,
                IsSelected = true,
                Group = ribbon.ContextualGroups[ContextualGroupIndex],
                //
                // Important: Ribbon tab controls contain no content.
                //
            };

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
                            Trace(TraceLoggerType.UiHelper,
                                  TraceEventType.Error,
                                  $"Unable to locate button style {buttonStyle}");
                            return null;
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

            if (DataContext != null)
            {
                newTab.DataContext = DataContext;
            }

            //
            // Note: we're not done, we need to override parts of the HeaderTemplate for
            // the tab title and plumb up the "X" close button, but these are UI element
            // operations that cannot be done until the template is applied. These are
            // handled from within the tab's Loaded callback.
            //
            newTab.Loaded += (s, e) =>
            {
                FixupDynamicTab(ribbon,
                    newTab,
                    TabText,
                    TabHeaderTextBlockName,
                    TabCloseButtonName,
                    TabClosedCallback);
            };

            ribbon.Tabs.Add(newTab);
            return newTab;
        }

        public static bool CreateTabControlContextualTab(
            TabControl TabControl,
            dynamic TabContent,
            string TabName,
            string TabText,
            string TabStyleTemplateName,
            string TabHeaderTextBlockName,
            string TabCloseButtonName,
            object? DataContext,
            Func<string, Task<bool>> TabClosedCallback)
        {
            var existingTabs = TabControl.Items.Cast<TabItem>().ToList();
            var tab = existingTabs.Where(tab => tab.Name == TabName).FirstOrDefault();
            if (tab != null)
            {
                tab.IsSelected = true;
                return true;
            }

            //
            // Locate the style template to apply to the tab.
            //
            var style = TabControl.FindResource(TabStyleTemplateName) as Style;
            if (style == null)
            {
                Trace(TraceLoggerType.UiHelper,
                      TraceEventType.Error,
                      $"Unable to locate tab style {TabStyleTemplateName}");
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
                Content = scrollViewer 
            };

            if (DataContext != null)
            {
                newTab.DataContext = DataContext;
            }

            //
            // Note: we're not done, we need to override parts of the HeaderTemplate for
            // the tab title and plumb up the "X" close button, but these are UI element
            // operations that cannot be done until the template is applied. These are
            // handled from within the tab's Loaded callback.
            //
            newTab.Loaded += (s, e) =>
            {
                FixupDynamicTab(TabControl,
                    newTab,
                    TabText,
                    TabHeaderTextBlockName,
                    TabCloseButtonName,
                    TabClosedCallback);
            };

            TabControl.Items.Add(newTab);
            return true;
        }

        private static void FixupDynamicTab(
            dynamic TabControl,
            dynamic NewTab,
            string TabTitle,
            string TabHeaderTextBlockName,
            string TabCloseButtonName,
            Func<string,Task<bool>> TabClosedCallback)
        {
            var headerTextBlock = (TextBlock)FindControlFromDataTemplate<TextBlock>(NewTab, TabHeaderTextBlockName);
            var headerCloseButton = (Button)FindControlFromDataTemplate<Button>(NewTab, TabCloseButtonName);

            if (headerTextBlock == null || headerCloseButton == null)
            {
                Trace(TraceLoggerType.UiHelper,
                      TraceEventType.Error,
                      $"Unable to locate headertextblock or closeButton");
                Debug.Assert(false);
                return;
            }

            //
            // Add a handler for this tab close button and assign unique tag to the tab
            // itself, so we can easily remove it.
            //
            headerCloseButton.Tag = NewTab.Name;
            headerCloseButton.Click += async (s, e) =>
            {
                //
                // Locate the button, corresponding tab name, and the containing tab control.
                //
                var button = s as Button;
                if (button == null)
                {
                    Trace(TraceLoggerType.UiHelper,
                          TraceEventType.Error,
                          $"Unable to locate button");
                    return;
                }

                var tabName = button.Tag as string;
                if (tabName == null)
                {
                    Trace(TraceLoggerType.UiHelper,
                          TraceEventType.Error,
                          $"Unable to locate tab name from tag");
                    return;
                }

                if (TabControl is Ribbon)
                {
                    var ribbon = TabControl as Ribbon;
                    var existingTab = ribbon!.Tabs.Where(
                        tab => tab.Name == tabName).FirstOrDefault();
                    if (existingTab == null)
                    {
                        Trace(TraceLoggerType.UiHelper,
                              TraceEventType.Error,
                              $"Tab {tabName} not found!");
                        return;
                    }

                    var result = await TabClosedCallback.Invoke(tabName);
                    if (!result)
                    {
                        Trace(TraceLoggerType.UiHelper,
                              TraceEventType.Warning,
                              $"TabClosedCallback returned false, not removing tab");
                        return;
                    }

                    existingTab.Template = null; // see https://github.com/dotnet/wpf/issues/6440
                    ribbon.Tabs.Remove(existingTab);

                    //
                    // If the context tab group contains no tabs after this removal,
                    // switch back to its "parent" Ribbon tab.
                    //
                    if (ribbon.Tabs.Count == 3)
                    {
                        if (tabName.StartsWith("Manifest"))
                        {
                            ribbon.SelectedTabIndex = 0; // Providers tab
                        }
                        else if (tabName.StartsWith("LiveSession"))
                        {
                            ribbon.SelectedTabIndex = 1; // Sessions tab
                        }
                    }
                }
                else if (TabControl is TabControl)
                {
                    var tc = TabControl as TabControl;
                    var existingTab = tc!.Items.Cast<TabItem>().ToList().Where(
                        tab => tab.Name == tabName).FirstOrDefault();
                    if (existingTab == null)
                    {
                        Trace(TraceLoggerType.UiHelper,
                              TraceEventType.Error,
                              $"Tab {tabName} not found!");
                        return;
                    }

                    var result = await TabClosedCallback.Invoke(tabName);
                    if (!result)
                    {
                        Trace(TraceLoggerType.UiHelper,
                              TraceEventType.Warning,
                              $"TabClosedCallback returned false, not removing tab");
                        return;
                    }

                    existingTab.Template = null; // see https://github.com/dotnet/wpf/issues/6440
                    tc.Items.Remove(existingTab);
                }
            };

            var title = $"{TabTitle}";
            var header = title;
            if (title.Length > 50)
            {
                int start = title.Length - 15;
                header = $"{title.Substring(0, 15)}...{title.Substring(start - 1, 15)}";
            }

            headerTextBlock.Text = header;
        }
    }
}
