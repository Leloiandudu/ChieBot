using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ChieBot.Modules
{
    class Modules
    {
        public delegate void Binder(MediaWiki wiki, string[] commandLine);

        private readonly IDictionary<string, Binder> _binders = new Dictionary<string, Binder>();

        public Modules(params Assembly[] assemblies)
            : this(assemblies.AsEnumerable())
        {
        }

        public Modules(IEnumerable<Assembly> assemblies)
        {
            _binders = (
                from ass in assemblies
                from type in ass.GetTypes()
                where typeof(IModule).IsAssignableFrom(type) && !type.IsAbstract
                select type
            ).ToDictionary(GetName, Bind);
        }

        private static string GetName(Type type)
        {
            const string suffix = "Module";

            var name = type.Name;
            if (name.EndsWith(suffix))
                name = name.Substring(0, name.Length - suffix.Length);

            return name;
        }

        private static Binder Bind(Type type)
        {
            return (wiki, args) =>
            {
                var module = (IModule)Activator.CreateInstance(type);
                module.Execute(wiki, args);
            };
        }

        public Binder Get(string name)
        {
            Binder binder;
            _binders.TryGetValue(name, out binder);
            return binder;
        }
    }
}
