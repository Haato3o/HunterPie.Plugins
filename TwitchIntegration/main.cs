using System;
using System.IO;
using HunterPie.Core;
using Debugger = HunterPie.Logger.Debugger;
using Newtonsoft.Json;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using System.Net;
using System.Linq;

namespace HunterPie.Plugins
{
    public class TwitchIntegration : IPlugin
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Game Context { get; set; }

        // Cache build link so we don't have to get a new one every single time someone uses a command
        private string _buildLink { get; set; }
        private string BuildLink
        {
            get => _buildLink;
            set
            {
                if (value != _buildLink)
                {
                    _buildLink = value;
                    ConvertToTinyUrlSync(value);
                }
            }
        }
        private string TinyURL { get; set; }

        TwitchClient client { get; set; }

        public void Initialize(Game context)
        {

            Name = "TwitchIntegration";
            Description = "A Twitch bot that talks directly to HunterPie";

            Context = context;
            HookEvents();
            SetupTwitchClient();
        }

        public void Unload()
        {
            UnhookEvents();
            DisconnectTwitchClient();
        }

        private void HookEvents()
        {
            Context.Player.OnCharacterLogin += UpdateBuildLink;
            Context.Player.OnSessionChange += UpdateBuildLink;
            Context.Player.OnClassChange += UpdateBuildLink;
        }

        private void UnhookEvents()
        {
            Context.Player.OnCharacterLogin -= UpdateBuildLink;
            Context.Player.OnSessionChange -= UpdateBuildLink;
            Context.Player.OnClassChange -= UpdateBuildLink;
        }

        private void SetupTwitchClient()
        {
            if (!File.Exists(Path.Combine(Environment.CurrentDirectory, "Modules\\TwitchIntegration", "config.json")))
            {
                Debugger.Error("Config.json for TwitchClient plugin not found!");
                return;
            }
            string configSerialized = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "Modules\\TwitchIntegration", "config.json"));
            ModConfig config = JsonConvert.DeserializeObject<ModConfig>(configSerialized);

            ConnectionCredentials creds = new ConnectionCredentials(config.Username, config.OAuth);
            var clientOptions = new ClientOptions()
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);

            client.Initialize(creds, config.Channel);

            client.OnMessageReceived += TwitchOnMessageRecv;
            client.OnConnected += TwitchOnConnected;

            client.Connect();
        }

        private void DisconnectTwitchClient()
        {
            client.OnMessageReceived -= TwitchOnMessageRecv;
            client.OnConnected -= TwitchOnConnected;
            client.Disconnect();
            client = null;
        }
        private void TwitchOnConnected(object sender, OnConnectedArgs e)
        {
            Debugger.Module("Twitch client connected!", Name);
        }

        private void TwitchOnMessageRecv(object sender, OnMessageReceivedArgs e)
        {
            if (!Context.Player.IsLoggedOn || client == null) return;

            string command = e.ChatMessage.Message.Split(' ').FirstOrDefault();
            switch (command?.ToLower())
            {
                case "!id":
                case "!session":
                    if (string.IsNullOrEmpty(Context.Player.SessionID))
                    {
                        client?.SendMessage(e.ChatMessage.Channel, "I'm currently not in a session :(");
                    } else
                    {
                        client?.SendMessage(e.ChatMessage.Channel, $"Session ID: {Context.Player.SessionID}");
                    }
                    break;
                case "!build":
                    client?.SendMessage(e.ChatMessage.Channel, $"Link to my current {Context.Player.WeaponName} build: {TinyURL}");
                    break;
                case "!rank":
                    client?.SendMessage(e.ChatMessage.Channel, $"{Context.Player.Name} | HR: {Context.Player.Level} | MR: {Context.Player.MasterRank} | PlayTime: {TimeSpan.FromSeconds(Context.Player.PlayTime).ToString(@"dd\.hh\:mm\:ss")}");
                    break;
                default:
                    return;
            }
        }

        private void UpdateBuildLink(object source, EventArgs args)
        {
            BuildLink = Honey.LinkStructureBuilder(Context.Player.GetPlayerGear());
        }

        private void ConvertToTinyUrlSync(string link)
        {
            using (WebClient wClient = new WebClient())
            {
                wClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                string newUrl = wClient.DownloadString($"http://tinyurl.com/api-create.php?url={link}");
                TinyURL = newUrl;
            }
        }
    }

    internal class ModConfig
    {
        public string Username { get; set; }
        public string OAuth { get; set; }
        public string Channel { get; set; }
    }
}
