using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Navigation;
using Websocket.Client;

namespace KeyboardExtensions
{
    public class DiscordCom
    {
        ArduinoCom arduinoCom;
        WebsocketClient client;

        private static readonly HttpClient httpClient = new HttpClient();
        private string API_ENDPOINT = "https://discord.com/api";

        private ConnStatus _currentStatus = ConnStatus.Disconnected;
        public ConnStatus CurrentStatus
        {
            get
            {
                return _currentStatus;
            }

            set
            {
                _currentStatus = value;
                if(OnStatusChanged != null) OnStatusChanged(this, value);
            }
        }
        public EventHandler<ConnStatus> OnStatusChanged;

        bool _mute = false;
        bool _deaf = false;

        bool mute
        {
            get
            {
                return _mute;
            }

            set
            {
                _mute = value;
                arduinoCom.SendCommandAsync(new Command(Consts.TYPE_NORMAL, new byte[] { Consts.CMD_RESP_VOICE, Consts.VOICE_MUTE, (byte)(value ? 1 : 0) }));
            }
        }

        bool deaf
        {
            get
            {
                return _deaf;
            }

            set
            {
                _deaf = value;
                arduinoCom.SendCommandAsync(new Command(Consts.TYPE_NORMAL, new byte[] { Consts.CMD_RESP_VOICE, Consts.VOICE_DEAF, (byte)(value ? 1 : 0) }));
            }
        }

        public DiscordCom(ArduinoCom arduinoCom)
        {
            this.arduinoCom = arduinoCom;

            for(int i = 6463; i <= 6472; i++)
            {
                Console.WriteLine("Trying Discord port " + i);
                client = new WebsocketClient(new Uri("ws://127.0.0.1:" + i + "/?client_id=" + Secrets.DISCORD_CLIENT_ID));

                try
                {
                    client.StartOrFail().Wait();
                } catch(Exception)
                {
                    Console.WriteLine("Trying next port...");
                    continue;
                }

                Console.WriteLine("Discord connected to WS");
                client.DisconnectionHappened.Subscribe(info =>
                {
                    CurrentStatus = ConnStatus.Disconnected;
                });

                AuthorizeSocket();
                client.ReconnectionHappened.Subscribe(info =>
                {
                    AuthorizeSocket();
                });
            }
        }

        private void SetMuteDeaf(bool _m, bool _d)
        {
            _mute = _m;
            _deaf = _d;

            arduinoCom.SendCommandAsync(new Command(Consts.TYPE_NORMAL, new byte[] { Consts.CMD_RESP_VOICE, Consts.VOICE_ALL, (byte)(_m ? 1 : 0), (byte)(_d ? 1 : 0) }));
        }

        private int TimestampNow()
        {
            return (int)(DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        private string RandomUuid()
        {
            return Guid.NewGuid().ToString();
        }

        private string PostRequest(string url, HttpContent content)
        {
            Task<HttpResponseMessage> postTask = httpClient.PostAsync(url, content);
            postTask.Wait();
            Task<string> readTask = postTask.Result.Content.ReadAsStringAsync();
            readTask.Wait();
            return readTask.Result;
        }

        private void AuthorizeSocket()
        {

        }

        private string GetValidAccessToken()
        {
            string token = "";
            int now = TimestampNow();

            string currentToken = RegSaver.GetSetting<string>("accessToken", null);
            if(currentToken != null)
            {
                int currentExpiration = RegSaver.GetSetting("expiresAt", 0);
                if(now < currentExpiration)
                {
                    string refreshToken = RegSaver.GetSetting<string>("refreshToken", null);
                    if(refreshToken != null && now >= currentExpiration - 3600*48)
                    {
                        Console.WriteLine("Refreshing Discord token...");

                        var refreshData = new Dictionary<string, string>
                        {
                            {"client_id", Secrets.DISCORD_CLIENT_ID}, {"client_secret", Secrets.DISCORD_CLIENT_SECRET}, {"grant_type", "refresh_token"}, {"refresh_token", refreshToken}
                        };

                        JObject tokenResponse = JSONDecoder.Decode(PostRequest(API_ENDPOINT + "/oauth2/token", new FormUrlEncodedContent(refreshData)));

                        try
                        {
                            if (tokenResponse["error"] != null)
                            {
                                token = currentToken;
                                Console.WriteLine("Cannot refresh Discord token: " + tokenResponse["error_description"].StringValue);
                            }
                        }
                        catch (KeyNotFoundException)
                        {
                            // no error
                            token = tokenResponse["access_token"].StringValue;
                            SaveAccessTokenResponse(tokenResponse, now);
                        }
                    } else
                    {
                        token = currentToken;
                    }
                }
            }

            if(token == "")
            {
                /*JObject authorizeResponse = JSONDecoder.Decode(read().Value);
                if(authorizeResponse["evt"].StringValue != null && authorizeResponse["evt"].StringValue.Trim().Length > 0)
                {
                    throw new Exception("Discord not authorized");
                } else
                {
                    string authCode = authorizeResponse["data"]["code"].StringValue;
                    var authData = new Dictionary<string, string>
                    {
                        {"client_id", CLIENT_ID}, {"client_secret", CLIENT_SECRET}, {"grant_type", "authorization_code"}, {"code", authCode}, {"redirect_uri", "http://127.0.0.1"}
                    };

                    JObject tokenResponse = JSONDecoder.Decode(PostRequest(API_ENDPOINT + "/oauth2/token", new FormUrlEncodedContent(authData)));

                    try
                    {
                        if (tokenResponse["error"] != null)
                        {
                            throw new Exception("Cannot get Discord token: " + tokenResponse["error_description"].StringValue);
                        }
                    } catch(KeyNotFoundException)
                    {
                        // no error
                        token = tokenResponse["access_token"].StringValue;
                        SaveAccessTokenResponse(tokenResponse, now);
                    }

                */
            }

            return token;
        }

        public void ToggleMute()
        {
            mute = !mute;
        }

        public void ToggleDeaf()
        {
            deaf = !deaf;
        }

        private void SaveAccessTokenResponse(JObject tokenResponse, int now)
        {
            string refreshToken = tokenResponse["refresh_token"].StringValue;
            int expiresIn = tokenResponse["expires_in"].IntValue;
            int expiresAt = expiresIn + now;

            RegSaver.SetSetting("accessToken", tokenResponse["access_token"].StringValue);
            RegSaver.SetSetting("expiresAt", expiresAt);
            RegSaver.SetSetting("refreshToken", refreshToken);
        }

        private byte[] GetLE(int val)
        {
            byte[] res = BitConverter.GetBytes(val);
            return BitConverter.IsLittleEndian ? res : res.Reverse().ToArray();
        }

        private int FromLE(byte[] val)
        {
            if (!BitConverter.IsLittleEndian) 
                val = val.Reverse().ToArray();
            return BitConverter.ToInt32(val, 0);
        }

        public enum ConnStatus
        {
            Disconnected, Connecting, Authenticating, Settingup, Connected
        }
    }
}
