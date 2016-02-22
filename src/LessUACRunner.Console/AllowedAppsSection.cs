using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;

namespace LessUACRunner.Console
{
    public class AllowedAppsSection: ConfigurationSection
    {
        #region Constructors
        static AllowedAppsSection()
        {
            s_propName = new ConfigurationProperty(
                "name",
                typeof(string),
                null,
                ConfigurationPropertyOptions.IsRequired
                );

            s_propAllowedApps = new ConfigurationProperty(
                "",
                typeof(AllowedAppsElementCollection),
                null,
                ConfigurationPropertyOptions.IsRequired | ConfigurationPropertyOptions.IsDefaultCollection
                );

            s_properties = new ConfigurationPropertyCollection();

            s_properties.Add(s_propName);
            s_properties.Add(s_propAllowedApps);
        }
        #endregion

        #region Fields
        private static ConfigurationPropertyCollection s_properties;
        private static ConfigurationProperty s_propName;
        private static ConfigurationProperty s_propAllowedApps;
        #endregion

        #region Properties
        public string Name
        {
            get { return (string)base[s_propName]; }
            set { base[s_propName] = value; }
        }

        public AllowedAppsElementCollection AllowedApps
        {
            get { return (AllowedAppsElementCollection)base[s_propAllowedApps]; }
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