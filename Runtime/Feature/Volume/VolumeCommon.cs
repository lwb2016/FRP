using System;

namespace UnityEngine.Rendering.FRP
{
    [AttributeUsage(AttributeTargets.Field)]
    public class SettingsGroup : Attribute
    {
        public bool isExpanded = true;
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class ParameterDisplayName : Attribute
    {
        public string name;

        public ParameterDisplayName(string name)
        {
            this.name = name;
        }
    }
    
    public class GeneralSettings : SettingsGroup
    {
        
    }
    
    public class PerformanceSetting : SettingsGroup
    {
            
    }
}