using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Newtonsoft.Json;

namespace BeachballPoker
{
    class Frame
    {
        [JsonIgnore]
        public int Depth;

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public int Value { get; set; }

        [JsonProperty("children")]
        public List<Frame> Children { get; } = new List<Frame>();

        public int TotalValue { get; set; }
        public double Ratio { get; set; }

        public void Flatten()
        {
            while (Children.Count == 1 && Children[0] is { } child && Name == child.Name)
            {
                var grandchildren = child.Children;
                Children.Clear();
                Children.AddRange(grandchildren);
            }

            foreach (var item in Children)
            {
                item.Flatten();
            }
        }

        public void Compute()
        {
            int sum = Value;

            foreach (var item in Children)
            {
                item.Compute();
                sum += item.TotalValue;
            }

            TotalValue = sum;
            var childSum = sum - Value;
            if (childSum > 0)
            {
                foreach (var item in Children)
                {
                    item.Ratio = item.TotalValue / (double)childSum;
                }
            }
        }

        public static Frame OpenFile(string filePath)
        {
            var rx2 = new Regex(@"^([ +!:|]+)([0-9]+) (.*)", RegexOptions.Compiled);
            var stack = new Stack<Frame>();

            using (var sr = new StreamReader(filePath))
            {
                string line;
                Frame currentFrame = new Frame();
                var rootFrame = currentFrame;
                var maxDepth = 0;
                int previousDepth = 0;

                while ((line = sr.ReadLine()) != null)
                {
                    var match = rx2.Match(line);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var depth = match.Groups[1].Length;
                    var count = int.Parse(match.Groups[2].Value);
                    var txt = match.Groups[3].Value;
                    if (txt.StartsWith(" ", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Work around https://github.com/DavidKarlas/PokeTheBeachball/issues/5
                    if (previousDepth > 0 && depth == previousDepth + 4)
                    {
                        depth = previousDepth + 2;
                    }

                    previousDepth = depth;
                    while (depth <= currentFrame.Depth)
                    {
                        currentFrame = stack.Pop();
                    }

                    if (stack.Count != ((depth - 4) / 2))
                    {
                        throw new Exception();
                    }

                    stack.Push(currentFrame);

                    if (maxDepth < stack.Count)
                    {
                        maxDepth = stack.Count;
                    }

                    currentFrame.Value -= count;

                    if (currentFrame.Value < 0 && currentFrame.Name != null)
                    {
                        throw new Exception();
                    }

                    currentFrame.Children.Add(currentFrame = new Frame()
                    {
                        Depth = depth,
                        Value = count,
                        Name = txt
                    });
                }

                rootFrame.Flatten();
                rootFrame.Compute();
                return rootFrame;
            }
        }
    }

    public class Viewer
    {
        public Viewer(Window window)
        {
            Window = window;
            window.Loaded += Window_Loaded;
            window.Content = GetContent();
        }

        public TreeView tree;

        private object GetContent()
        {
            var grid = new Grid();

            tree = new TreeView();
            tree.Margin = new Thickness(11);

            var frameTemplate = new HierarchicalDataTemplate();
            frameTemplate.ItemsSource = new Binding(nameof(Frame.Children));
            frameTemplate.DataType = typeof(Frame);

            var panelFactory = new FrameworkElementFactory(typeof(Grid));

            var progressFactory = new FrameworkElementFactory(typeof(ProgressBar));
            progressFactory.SetValue(FrameworkElement.WidthProperty, 600.0);
            progressFactory.SetValue(ProgressBar.BackgroundProperty, Brushes.Transparent);
            progressFactory.SetValue(ProgressBar.BorderBrushProperty, Brushes.Transparent);
            progressFactory.SetValue(ProgressBar.MinimumProperty, 0.0);
            progressFactory.SetValue(ProgressBar.MaximumProperty, 1.0);
            progressFactory.SetValue(ProgressBar.OpacityProperty, 0.2);
            progressFactory.SetValue(ProgressBar.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            progressFactory.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(Frame.Ratio)));

            panelFactory.AppendChild(progressFactory);

            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(Frame.Name)));
            panelFactory.AppendChild(textBlockFactory);

            frameTemplate.VisualTree = panelFactory;

            tree.Resources.Add(typeof(Frame), frameTemplate);
            tree.ItemTemplate = frameTemplate;

            var treeViewItemStyle = new Style(typeof(TreeViewItem));
            treeViewItemStyle.Setters.Add(new EventSetter(TreeViewItem.ExpandedEvent, (RoutedEventHandler)OnTreeViewItemExpanded));

            tree.ItemContainerStyle = treeViewItemStyle;

            grid.Children.Add(tree);

            return grid;
        }

        private void OnTreeViewItemExpanded(object sender, RoutedEventArgs args)
        {
            if (sender is TreeViewItem item)
            {
                foreach (var child in item.Items.OfType<Frame>())
                {
                    if (child.Ratio > 0.5)
                    {
                        EventHandler handler = null;
                        handler = (s, e) =>
                        {
                            if (item.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                            {
                                return;
                            }

                            item.ItemContainerGenerator.StatusChanged -= handler;

                            var childContainer = item.ItemContainerGenerator.ContainerFromItem(child);
                            var childTreeViewItem = childContainer as TreeViewItem;
                            if (childTreeViewItem != null && !childTreeViewItem.IsExpanded)
                            {
                                childTreeViewItem.IsExpanded = true;
                            }
                        };
                        item.ItemContainerGenerator.StatusChanged += handler;
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (FilePath != null)
            {
                var frame = Frame.OpenFile(FilePath);
                tree.ItemsSource = frame.Children;
            }
        }

        public Window Window { get; }
        public string FilePath { get; internal set; }
    }
}