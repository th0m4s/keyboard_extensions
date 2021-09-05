using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KeyboardExtensions
{
    /// <summary>
    /// Interaction logic for DiscordSettings.xaml
    /// </summary>
    public partial class DiscordSettings : Window
    {
        DiscordCom discordCom;

        public DiscordSettings(DiscordCom discordCom)
        {
            this.discordCom = discordCom;
            InitializeComponent();
        }

        public void RunOnUiThread(Action action)
        {
            Dispatcher.Invoke(action);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            discordCom.OnStatusChanged += DiscordStatusChanged;
            DiscordStatusChanged(discordCom, discordCom.CurrentStatus);
        }

        private void CloseButton_MouseEnter(object sender, MouseEventArgs e)
        {
            CloseButton_Background.Fill = Brushes.Red;
        }

        private void CloseButton_MouseLeave(object sender, MouseEventArgs e)
        {
            Brush barColor = TopBar_Background.Fill;
            CloseButton_Background.Fill = barColor;
        }

        private void CloseButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void DiscordStatusChanged(object sender, DiscordCom.ConnStatus status)
        {
            RunOnUiThread(() =>
            {
                currentStatus_label.Content = "Current status: " + status;
                discordActionButton.IsEnabled = status == DiscordCom.ConnStatus.Disconnected;
            });
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            discordCom.OnStatusChanged -= DiscordStatusChanged;
        }

        private void discordActionButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TopBar_Background_MouseDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }
}
