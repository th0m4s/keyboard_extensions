using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardExtensions
{
    class StartupUtils
    {
        private static string VALUE_NAME
        {
            get
            {
                return "KeyboardExtensions";
            }
        }

        private static string PATH
        {
            get
            {
                return Assembly.GetExecutingAssembly().Location;
            }
        }

        public static void SetStartup(bool startup)
        {
            RegistryKey rk = GetStartupKey();

            if (startup)
                rk.SetValue(VALUE_NAME, PATH + " silent");
            else rk.DeleteValue(VALUE_NAME, false);
        }

        public static bool IsStartup()
        {
            return GetStartupKey().GetValue(VALUE_NAME) != null;
        }

        private static RegistryKey GetStartupKey()
        {
            return Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        }
    }
}
