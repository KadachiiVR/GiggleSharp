using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration.Ini;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;

namespace GiggleSharp
{
    internal class Config
    {
        private IConfiguration cfg;
        private Dictionary<string, string> defaults;
        public HashSet<string> Sections { get; private set; }
        public Config(string filename)
        {
            this.cfg = new ConfigurationBuilder().AddIniFile(filename).Build();
            this.Sections = cfg.GetChildren().Select(s => s.Key).ToHashSet();
            this.Sections.Remove("Defaults");
            //this.defaults = new Dictionary<string, string>(this.cfg.GetSection("Defaults").GetChildren().Select(c => new KeyValuePair<string, string>(c.Key, c.Value)));
            this.defaults = new Dictionary<string, string>();
            foreach (var child in this.cfg.GetSection("Defaults").GetChildren())
            {
                string key = child.Key.Split(":").Last();
                string val = child.Value;
                if (!string.IsNullOrWhiteSpace(val))
                {
                    this.defaults.Add(key, val);
                }
            }
            //this.defaults = new Dictionary<string, string>(cfg.GetSection("Defaults").AsEnumerable());
        }

        public Dictionary<string, string> GetSection(string sectionName)
        {
            var section = new Dictionary<string, string>();
            foreach (var child in this.cfg.GetSection(sectionName).GetChildren())
            {
                string key = child.Key.Split(":").Last();
                string val = child.Value;
                if (!string.IsNullOrWhiteSpace(val))
                {
                    section.Add(key, val);
                }
            }
            foreach (var key in this.defaults.Keys)
            {
                if (!section.ContainsKey(key))
                {
                    section[key] = this.defaults[key];
                }
            }
            section["name"] = sectionName;

            return section;
        }
    }
}
