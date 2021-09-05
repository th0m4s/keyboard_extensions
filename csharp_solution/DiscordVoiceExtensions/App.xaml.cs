using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace KeyboardExtensions
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            MainWindow wnd = new MainWindow();
            bool silent = false;
            if (e.Args.Length > 0 && e.Args[0].Trim().ToLower() == "silent")
                silent = true;

            if (silent)
            {
                wnd.Minimize();
            }

            wnd.Show();
        }
    }
}
