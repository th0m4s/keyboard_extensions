using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WindowsInput;
using WindowsInput.Native;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Image = System.Windows.Controls.Image;

namespace KeyboardExtensions
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();

        InputSimulator inputSimulator;
        ArduinoCom arduinoCom;
        DiscordCom discordCom;

        Action<string> ConnectCallback = null;

        Label[] keyLabels = new Label[7];
        ComboBox[] modeComboBoxes = new ComboBox[7];
        ComboBox[] valComboBoxes = new ComboBox[7];

        byte[] specialKeys = null;
        byte[] keyModes = null;
        byte[] keyValues = null;

        byte settingDebounce = 10;
        byte settingTrigger = Consts.TRIGGER_DOWN;

        public MainWindow()
        {
            InitializeComponent();

            notifyIcon.Icon = new Icon(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/kb_ext_white.ico")).Stream);
            notifyIcon.Text = "Keyboard Extensions";
            notifyIcon.Visible = false;
            notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            ShowFromTaskbar();
        }

        public void Minimize()
        {
            ShowInTaskbar = false;
            WindowState = WindowState.Minimized;
            Visibility = Visibility.Hidden;
            notifyIcon.Visible = true;
        }

        public void ShowFromTaskbar()
        {
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Visibility = Visibility.Visible;
            notifyIcon.Visible = false;
        }

        private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
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

        public void RunOnUiThread(Action action)
        {
            Dispatcher.Invoke(action);
        }

        private SelectionChangedEventHandler GetModeChangeHandler(int i)
        {
            return delegate (object comboBox, SelectionChangedEventArgs selectEvent)
            {
                if (updatingSettings) return;

                byte mode = (byte)modeComboBoxes[i].SelectedIndex;
                keyModes[i] = mode;

                byte value = mode == 0 ? (byte)(18 + i) : (byte)0;
                keyValues[i] = value;

                RunOnUiThread(() =>
                {
                    UpdateValComboBox(valComboBoxes[i], i);
                });

                arduinoCom.SendCommandAsync(new Command(Consts.TYPE_NORMAL, new byte[] { Consts.CMD_SET_KEY_SETTING, (byte)i, 0, mode }));
            };
        }

        private SelectionChangedEventHandler GetValChangeHandler(int i)
        {
            return delegate (object comboBox, SelectionChangedEventArgs selectEvent)
            {
                if (updatingSettings) return;

                byte value = (byte)valComboBoxes[i].SelectedIndex;
                keyValues[i] = value;
                arduinoCom.SendCommandAsync(new Command(Consts.TYPE_NORMAL, new byte[] { Consts.CMD_SET_KEY_SETTING, (byte)i, 1, value }));
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ShowInTaskbar = true;

            autorunCheckbox.IsChecked = StartupUtils.IsStartup();
            autorunCheckbox.Checked += delegate (object clickSender, RoutedEventArgs checkedEvent)
            {
                StartupUtils.SetStartup(true);
            };
            autorunCheckbox.Unchecked += delegate (object clickSender, RoutedEventArgs uncheckedEvent)
            {
                StartupUtils.SetStartup(false);
            };

            for (int i = 0; i < 7; i++)
            {
                keyLabels[i] = (Label)FindName("label_key_" + i);

                modeComboBoxes[i] = (ComboBox)FindName("mode_key_" + i);
                modeComboBoxes[i].SelectionChanged += GetModeChangeHandler(i);

                valComboBoxes[i] = (ComboBox)FindName("val_key_" + i);
                valComboBoxes[i].SelectionChanged += GetValChangeHandler(i);
            }

            SettingsGrid.Visibility = Visibility.Hidden;

            inputSimulator = new InputSimulator();
            arduinoCom = new ArduinoCom();
            // discordCom = new DiscordCom(arduinoCom);

            arduinoCom.OnConnected = (string portName) =>
            {
                arduinoCom.SendCommandAsync(new Command(Consts.TYPE_NORMAL, new byte[] { Consts.CMD_REQ_SETTINGS }));

                RunOnUiThread(() =>
                {
                    connectButton.IsEnabled = true;
                    connectButton.Content = "Disconnect";

                    dropdownPort.ItemsSource = new string[] { portName };
                    dropdownPort.SelectedIndex = 0;
                    dropdownPort.IsEnabled = false;
                });
            };

            arduinoCom.OnDisconnected = () =>
            {
                RunOnUiThread(() =>
                {
                    connectButton.Content = "Connect";
                    connectButton.IsEnabled = false;

                    dropdownPort.ItemsSource = new string[] { };
                    dropdownPort.IsEnabled = false;

                    SettingsGrid.Visibility = Visibility.Hidden;
                    specialKeys = null;
                    keyModes = null;
                    keyValues = null;
                });
            };

            arduinoCom.AskForPort = (string[] ports, Action<string> callback) =>
            {
                RunOnUiThread(() =>
                {
                    ConnectCallback = callback;

                    connectButton.Content = "Connect";
                    connectButton.IsEnabled = true;

                    dropdownPort.ItemsSource = ports;
                    dropdownPort.SelectedIndex = 0;
                    dropdownPort.IsEnabled = true;
                });
            };

            arduinoCom.OnCommand = (byte[] message) =>
            {
                Console.WriteLine(string.Join(", ", message));

                if (message.Length > 0)
                {
                    switch(message[0])
                    {
                        case Consts.CMD_RESP_SETTINGS:
                            byte numKeys = message[1];
                            byte numSpecial = message[2];
                            specialKeys = new byte[numSpecial];
                            for(byte i = 0; i < numSpecial; i++)
                            {
                                specialKeys[i] = message[3 + i];
                            }

                            keyModes = new byte[numKeys];
                            keyValues = new byte[numKeys];

                            for(byte i = 0; i < numKeys; i++)
                            {
                                keyModes[i] = message[3 + numSpecial + i * 2];
                                keyValues[i] = message[4 + numSpecial + i * 2];
                            }

                            int baseSettingKey = 3 + numSpecial + numKeys * 2;
                            settingDebounce = message[baseSettingKey + Consts.SETTING_DEBOUNCE];
                            settingTrigger = message[baseSettingKey + Consts.SETTING_TRIGGER];

                            UpdateSettingsPanel();
                            break;

                        case Consts.CMD_KEY:
                            {
                                byte keyId = message[1];

                                if (!IsSpecial(keyId))
                                {
                                    byte keyVal = keyValues[keyId];
                                    bool pressed = message[2] == 1;

                                    if (keyVal > 0)
                                    {
                                        VirtualKeyCode keycode = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), Keys.KEYS_LIST[keyVal]);
                                        if (pressed)
                                        {
                                            inputSimulator.Keyboard.KeyDown(keycode);
                                        }
                                        else
                                        {
                                            inputSimulator.Keyboard.KeyUp(keycode);
                                        }
                                    }
                                }
                            }
                            break;

                        case Consts.CMD_SPECIAL:
                            {
                                byte keyId = message[1];

                                if (IsSpecial(keyId))
                                {
                                    byte keyVal = keyValues[keyId];
                                
                                    switch(keyVal)
                                    {
                                        case 0:
                                            discordCom.ToggleMute();
                                            break;
                                        case 1:
                                            discordCom.ToggleDeaf();
                                            break;
                                    }
                                }
                            }
                            break;
                    }
                }
            };

            arduinoCom.AllowConnect();
        }

        bool updatingSettings = false;
        private void UpdateSettingsPanel()
        {
            if (specialKeys == null || keyModes == null || keyValues == null)
                return;

            RunOnUiThread(() =>
            {
                SettingsGrid.Visibility = Visibility.Visible;

                updatingSettings = true;
                int numKeys = keyModes.Length;
                for (int i = 0; i < 7; i++)
                {
                    ComboBox currentModeComboBox = modeComboBoxes[i];
                    ComboBox currentValComboBox = valComboBoxes[i];

                    if(i >= numKeys)
                    {
                        keyLabels[i].Visibility = Visibility.Hidden;
                        currentModeComboBox.Visibility = Visibility.Hidden;
                        currentValComboBox.Visibility = Visibility.Hidden;
                    } else
                    {
                        keyLabels[i].Visibility = Visibility.Visible;
                        currentModeComboBox.Visibility = Visibility.Visible;
                        currentValComboBox.Visibility = Visibility.Visible;

                        if(IsSpecial(i))
                        {
                            currentModeComboBox.ItemsSource = new string[] { "Keyboard", "Discord" };
                            currentModeComboBox.IsEnabled = true;
                            currentModeComboBox.SelectedIndex = keyModes[i];
                        } else
                        {
                            currentModeComboBox.ItemsSource = new string[] { "Keyboard" };
                            currentModeComboBox.IsEnabled = false;
                            currentModeComboBox.SelectedIndex = 0;
                        }

                        UpdateValComboBox(currentValComboBox, i);
                    }
                }

                debounceSettingInput.Text = settingDebounce.ToString();

                specialTriggerComboBox.ItemsSource = new string[] { "KeyUp", "KeyDown" };
                specialTriggerComboBox.SelectedIndex = settingTrigger;

                updatingSettings = false;
            });
        }

        private bool IsSpecial(int i)
        {
            return specialKeys.Where(x => x == i).Count() > 0;
        }

        private void UpdateValComboBox(ComboBox comboBox, int pos)
        {
            byte mode = keyModes[pos];
            bool _updating = updatingSettings;
            updatingSettings = true;
            if (mode == 0)
            {
                comboBox.ItemsSource = Keys.KEYS_LIST;
            } else
            {
                comboBox.ItemsSource = new string[] { "Mute", "Deaf" };
            }
            updatingSettings = _updating;
            comboBox.SelectedIndex = keyValues[pos];
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            if(ConnectCallback != null)
            {
                ConnectCallback.Invoke((string)dropdownPort.SelectedItem);
                ConnectCallback = null;
            } else if(arduinoCom.IsConnected())
            {
                arduinoCom.Disconnect();
            } else
            {
                arduinoCom.AllowConnect();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            arduinoCom.Dispose();
            Environment.Exit(0);
        }

        private void discordButton_Click(object sender, RoutedEventArgs e)
        {
            DiscordSettings window = new DiscordSettings(discordCom);
            window.ShowDialog();
        }

        private void NumericInputCheck(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void debounceSettingInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (updatingSettings) return;

            string text = debounceSettingInput.Text;
            int val = int.Parse(text);

            if (text.StartsWith("0")) debounceSettingInput.Text = text.TrimStart('0');
            else if (val < 1) debounceSettingInput.Text = "1";
            else if (val > 255) debounceSettingInput.Text = "255";
            else
            {
                settingDebounce = (byte)val;
                arduinoCom.SendCommandAsync(new Command(Consts.TYPE_NORMAL, new byte[] { Consts.CMD_SET_OTHER_SETTING, Consts.SETTING_DEBOUNCE, settingDebounce }));
            }
        }

        private void specialTriggerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingSettings) return;

            byte val = (byte)specialTriggerComboBox.SelectedIndex;
            if(val == Consts.TRIGGER_UP || val == Consts.TRIGGER_DOWN)
            {
                settingTrigger = val;
                arduinoCom.SendCommandAsync(new Command(Consts.TYPE_NORMAL, new byte[] { Consts.CMD_SET_OTHER_SETTING, Consts.SETTING_TRIGGER, val }));
            }
        }

        private void MinimizeButton_MouseEnter(object sender, MouseEventArgs e)
        {
            MinimizeButton_Background.Fill = Brushes.Gray;
        }

        private void MinimizeButton_MouseLeave(object sender, MouseEventArgs e)
        {
            Brush barColor = TopBar_Background.Fill;
            MinimizeButton_Background.Fill = barColor;
        }

        private void MinimizeButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Minimize();
        }
    }
}
