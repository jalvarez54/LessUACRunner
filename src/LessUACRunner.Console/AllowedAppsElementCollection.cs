using System.Configuration;

namespace LessUACRunner.Console
{
    public class AllowedAppsElementCollection: ConfigurationElementCollection
    {
        #region Constructor
        public AllowedAppsElementCollection()
        {
        }
        #endregion

        #region Properties
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }
        protected override string ElementName
        {
            get
            {
                return "allowedApp";
            }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return new ConfigurationPropertyCollection();
            }
        }
        #endregion

        #region Indexers
        public AllowedAppElement this[int index]
        {
            get
            {
                return (AllowedAppElement)base.BaseGet(index);
            }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }
                base.BaseAdd(index, value);
            }
        }
        
        new public AllowedAppElement this[string shortcut]
        {
            get
            {
                return (AllowedAppElement)base.BaseGet(shortcut);
            }
        }
        #endregion

        #region Methods
        public void Add(AllowedAppElement item)
        {
            base.BaseAdd(item);
        }

        public void Remove(AllowedAppElement item)
        {
            if (BaseIndexOf(item) >= 0)
                BaseRemove(item.Shortcut);
        }

        public void RemoveAt(int index)
        {
            base.BaseRemoveAt(index);
        }
        #endregion

        #region Overrides
        protected override ConfigurationElement CreateNewElement()
        {
            return new AllowedAppElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as AllowedAppElement).Shortcut;
        }


        #endregion
    }
}
