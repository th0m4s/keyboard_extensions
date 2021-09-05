using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardExtensions
{
    class Keys
    {
        private static List<string> _list = null;
        public static List<string> KEYS_LIST
        {
            get
            {
                if (_list == null)
                    _list = new List<string>(new string[] { "None", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "F13", "F14", "F15", "F16", "F17", "F18", "F19", "F20", "F21", "F22", "F23", "F24" });

                return _list;
            }
        }
    }
}
