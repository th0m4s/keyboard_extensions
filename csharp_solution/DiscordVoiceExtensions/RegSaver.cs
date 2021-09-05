using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardExtensions
{
    class RegSaver
    {
        private static RegistryKey GetSettingsKey()
        {
            return Registry.CurrentUser.CreateSubKey("SOFTWARE\\th0m4s\\KeyboardExtensions", true);
        }

        public static T GetSetting<T>(string name, T defaultValue)
        {
            return (T)GetSettingsKey().GetValue(name, defaultValue);
        }

        public static void SetSetting(string name, object value)
        {
            GetSettingsKey().SetValue(name, value);
        }

        public static void DeleteSetting(string name)
        {
            GetSettingsKey().DeleteValue(name, false);
        }
    }
}
