using System;
using System.IO;
using System.Windows;

namespace BeachballPoker
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var app = new Application();
            var window = new Window()
            {
                Title = "Sample trace viewer"
            };

            var viewer = new Viewer(window);

            if (args.Length > 0)
            {
                var arg = args[0];
                if (File.Exists(arg))
                {
                    arg = Path.GetFullPath(arg);
                    viewer.FilePath = arg;
                }
            }

            app.Run(window);
        }
    }
}