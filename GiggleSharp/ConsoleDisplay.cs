using Giggletech.OSCRouter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GiggleSharp
{
    internal class ConsoleDisplay
    {
        private static ConsoleDisplay _instance = null;
        public static ConsoleDisplay Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConsoleDisplay();
                }

                return _instance;
            }
        }

        public StringBuilder builder = new StringBuilder();
        public string currentOutput = "";
        public Dictionary<string, float> channels = new Dictionary<string, float>();
        public Dictionary<string, float> scales = new Dictionary<string, float>();
        public string[] prefix;
        public string[] appendix;
        public int updateCount = 0;

        public void RegisterChannel(string label, float max)
        {
            channels.Add(label, 0f);
            scales.Add(label, max);
        }

        public void UpdateChannel(string label, float value)
        {
            channels[label] = value;
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            int cols = Math.Min(Console.WindowWidth - 5, 60);
            builder.Clear();
            foreach (string s in this.prefix)
            {
                builder.AppendLine(s);
            }
            builder.AppendLine();

            Interlocked.Increment(ref this.updateCount);
            builder.AppendLine($"Updated {this.updateCount} times");
            foreach (var label in channels.Keys)
            {
                builder.AppendLine(label);
                float rawValue = channels[label];
                float scale = scales[label];
                float value = Math.Clamp(rawValue / scale, 0f, 1f);
                builder.AppendLine($"[{new string('#', (int)Math.Floor(cols * value))}{ new string('-', (int)Math.Ceiling(cols * (1 - value)))}]: {rawValue} / {scale}");
            }
            builder.AppendLine();

            foreach (string s in this.appendix)
            {
                builder.AppendLine(s);
            }

            Console.SetCursorPosition(0, 0);
            Console.Clear();
            this.currentOutput = builder.ToString();
            Console.WriteLine(this.currentOutput);
        }
    }
}
