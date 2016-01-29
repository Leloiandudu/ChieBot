using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot.JE
{
    class Template
    {
        private static readonly Regex ArgRegex = new Regex(@"^\s*(\w+)\s*=(.*)$");

        public Template()
        {
            Args = new List<Argument>();
        }

        public static Template Parse(string wiki)
        {
            wiki = wiki.Trim();

            if (!wiki.StartsWith("{{") || !wiki.EndsWith("}}"))
                throw new FormatException("Template should be surrounded by {{}}.");
            wiki = wiki.Substring(2, wiki.Length - 4);

            // TODO: nested templates / template args
            var parts = wiki.Split('|');

            var template = new Template { Name = parts[0] };

            for (var i = 1; i < parts.Length; i++)
            {
                var part = parts[i];
                var match = ArgRegex.Match(part);

                var arg = match.Success
                    ? new Argument { Name = match.Groups[1].Value, Value = match.Groups[2].Value }
                    : new Argument { Value = part };

                template.Args.Add(arg);
            }

            return template;
        }

        public string Name { get; set; }
        public IList<Argument> Args { get; private set; }

        public class Argument
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        public string this[string name]
        {
            get { return Args.Where(a => a.Name == name).Select(a => a.Value).FirstOrDefault(); }
            set
            {
                var arg = Args.Where(a => a.Name == name).FirstOrDefault();
                if (arg == null)
                {
                    arg = new Argument { Name = name };
                    Args.Add(arg);
                }
                arg.Value = value;
            }
        }

        public override string ToString()
        {
            var result = "{{" + Name;

            if (Args.Count > 0)
                result += "|" + string.Join("|", Args.Select(a => a.Name == null ? a.Value : string.Format("{0}={1}", a.Name, a.Value)));

            return result + "}}";
        }
    }
}
