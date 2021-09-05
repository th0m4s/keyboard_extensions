using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace KeyboardExtensions
{
    public class ArduinoCom
    {
        SerialPort serialPort = null;

        public Action<string> OnConnected;
        public Action OnDisconnected;
        public Action<byte[]> OnCommand;
        public Action<string[], Action<string>> AskForPort;

        bool CanConnect = false;
        bool AskingForPort = false;

        Thread searchThread;
        Thread comThread;

        Queue<Command> commands;

        int LastTickSent = 0;
        int LastTickReceived = -1;
        const int PingTime = 500;

        public void Dispose()
        {
            try
            {
                if(serialPort != null && serialPort.IsOpen) serialPort.Close();
            } catch(Exception) { }

            searchThread.Abort();
            comThread.Abort();
        }

        public ArduinoCom()
        {
            commands = new Queue<Command>();

            searchThread = new Thread(delegate ()
            {
                while(true)
                {
                    if (serialPort == null)
                    {
                        string[] names = SerialPort.GetPortNames();

                        if (CanConnect && names.Length == 1)
                        {
                            CreateSerialPort(names[0]);
                        }
                        else if (names.Length > 0 && !AskingForPort)
                        {
                            bool used = false;
                            if (AskForPort != null)
                            {
                                AskingForPort = true;
                                AskForPort.Invoke(names, delegate (string name)
                                {
                                    AllowConnect();
                                    AskingForPort = false;
                                    if (!used)
                                    {
                                        used = true;
                                        CreateSerialPort(name);
                                    }
                                });
                            }
                        }
                    }


                    if (!CanConnect && serialPort != null)
                    {
                        if (serialPort.IsOpen) serialPort.Close();
                        InternalDisconnect();
                    }

                    Thread.Sleep(100);
                }
            });
            searchThread.Start();

            comThread = new Thread(delegate ()
            {
                byte messageType = 0;
                byte messageLength = 0;
                byte[] message = null;
                byte msgPosition = 0;

                while(true)
                {
                    Thread.Sleep(Consts.FRAME);

                    try
                    {
                        if (serialPort != null)
                        {
                            if (serialPort.IsOpen)
                            {
                                int now = Environment.TickCount;
                                if (now - LastTickSent >= PingTime)
                                {
                                    LastTickSent = now;
                                    SendCommandAsync(new Command(Consts.TYPE_PING));
                                }

                                if (serialPort.BytesToRead > 0)
                                {
                                    if (messageType == 0)
                                    {
                                        messageType = (byte)serialPort.ReadByte();
                                    }

                                    if (serialPort.BytesToRead > 0)
                                    {
                                        if (messageLength == 0)
                                        {
                                            messageLength = (byte)serialPort.ReadByte();
                                            message = new byte[messageLength];

                                            msgPosition = 0;
                                        }

                                        while (serialPort.BytesToRead > 0 && msgPosition < messageLength)
                                        {
                                            message[msgPosition++] = (byte)serialPort.ReadByte();
                                        }
                                    }
                                }

                                if (message != null && msgPosition == messageLength)
                                {
                                    if (messageType == Consts.TYPE_PING)
                                    {
                                        if (LastTickReceived < 0 && OnConnected != null)
                                        {
                                            OnConnected.Invoke(serialPort.PortName);
                                        }

                                        LastTickReceived = now;
                                    }
                                    else if (messageType == Consts.TYPE_NORMAL)
                                    {
                                        if (OnCommand != null)
                                        {
                                            OnCommand.Invoke(message);
                                        }
                                    }

                                    messageType = 0;
                                    messageLength = 0;
                                }

                                while (commands.Count > 0)
                                {
                                    Command command = commands.Dequeue();
                                    serialPort.Write(new byte[] { command.type, (byte)command.buffer.Length }, 0, 2);
                                    serialPort.Write(command.buffer, 0, command.buffer.Length);
                                }
                            }
                            else if(LastTickReceived > 0)
                            {
                                InternalDisconnect();
                            }
                        }
                    } catch(UnauthorizedAccessException)
                    {
                        InternalDisconnect();
                    }
                }
            });
            comThread.Start();
        }

        private void InternalDisconnect()
        {
            serialPort = null;
            if (OnDisconnected != null)
                OnDisconnected.Invoke();
        }

        public void AllowConnect()
        {
            CanConnect = true;
        }

        public void Disconnect()
        {
            CanConnect = false;
        }

        public bool IsConnected()
        {
            return serialPort != null && serialPort.IsOpen;
        }

        public void SendCommandAsync(Command command)
        {
            commands.Enqueue(command);
        }

        private void CreateSerialPort(string name)
        {
            LastTickReceived = -1;
            LastTickSent = 0;

            serialPort = new SerialPort(name, 19200);
            serialPort.Open();
            serialPort.ReadExisting();
        }
    }

    public class Command
    {
        public byte type;
        public byte[] buffer;

        public Command(byte type, byte[] buffer = null)
        {
            this.type = type;

            if (buffer == null)
                buffer = new byte[0];

            this.buffer = buffer;
        }
    }

    public class Consts
    {
        public const int FRAME = 1000 / 60;

        public const byte TYPE_PING = 1;
        public const byte TYPE_NORMAL = 2;

        public const byte CMD_REQ_SETTINGS = 1;
        public const byte CMD_RESP_SETTINGS = 2;
        public const byte CMD_SET_KEY_SETTING = 3;
        public const byte CMD_REQ_VOICE = 4;
        public const byte CMD_RESP_VOICE = 5;
        public const byte CMD_KEY = 6;
        public const byte CMD_SPECIAL = 7;
        public const byte CMD_SET_OTHER_SETTING = 8;

        public const byte VOICE_MUTE = 0;
        public const byte VOICE_DEAF = 1;
        public const byte VOICE_ALL = 2;

        public const byte SETTING_DEBOUNCE = 0;
        public const byte SETTING_TRIGGER = 1;

        public const byte TRIGGER_UP = 0;
        public const byte TRIGGER_DOWN = 1;
    }
}
