using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Text;

namespace LessUACRunner.Console
{

    public class AllowedAppElement: ConfigurationElement
    {
        #region Constructors
        static AllowedAppElement()
        {
            s_propShortcut = new ConfigurationProperty(
                "shortcut",
                typeof(string),
                null,
                ConfigurationPropertyOptions.IsRequired
                );

            s_propPath = new ConfigurationProperty(
                "path",
                typeof(string),
                null,
                ConfigurationPropertyOptions.None
                );

            s_propArgs = new ConfigurationProperty(
                "args",
                typeof(string),
                null,
                ConfigurationPropertyOptions.None
                );

            s_propConsole = new ConfigurationProperty(
                "console",
                typeof(bool),
                false,
                ConfigurationPropertyOptions.IsRequired
                );

            s_properties = new ConfigurationPropertyCollection();

            s_properties.Add(s_propShortcut);
            s_properties.Add(s_propPath);
            s_properties.Add(s_propArgs);
            s_properties.Add(s_propConsole);
        }
        #endregion

        #region Fields
        private static ConfigurationPropertyCollection s_properties;
        private static ConfigurationProperty s_propShortcut;
        private static ConfigurationProperty s_propPath;
        private static ConfigurationProperty s_propArgs;
        private static ConfigurationProperty s_propConsole;
        #endregion

        #region Properties
        public string Shortcut
        {
            get { return (string)base[s_propShortcut]; }
            set { base[s_propShortcut] = value; }
        }

        public string Path
        {
            get { return (string)base[s_propPath]; }
            set { base[s_propPath] = value; }
        }

        public string Args
        {
            get { return (string)base[s_propArgs]; }
            set { base[s_propArgs] = value; }
        }
        public bool Console
        {
            get { return (bool)base[s_propConsole]; }
            set { base[s_propConsole] = value; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return s_properties;
            }
        }
        #endregion
    }
}
