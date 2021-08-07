using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(Frame.Name)));
            stackPanelFactory.AppendChild(textBlockFactory);
            frameTemplate.VisualTree = stackPanelFactory;

            tree.Resources.Add(typeof(Frame), frameTemplate);
            tree.ItemTemplate = frameTemplate;

            grid.Children.Add(tree);

            return grid;
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