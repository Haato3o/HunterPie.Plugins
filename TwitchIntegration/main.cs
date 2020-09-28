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

        TwitchStrings strings;
        TwitchClient client { get; set; }

        public void Initialize(Game context)
        {

            Name = "TwitchIntegration";
            Description = "A Twitch bot that talks directly to HunterPie";

            Context = context;
            HookEvents();
            LoadStrings();
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

        private void LoadStrings()
        {
            if (!File.Exists(Path.Combine(Environment.CurrentDirectory, "Modules\\TwitchIntegration", "strings.json")))
            {
                strings = new TwitchStrings();
            } else
            {
                string stringsSerialized = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "Modules\\TwitchIntegration", "strings.json"));
                strings = JsonConvert.DeserializeObject<TwitchStrings>(stringsSerialized);
            }
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
            string parsed;
            switch (command?.ToLowerInvariant())
            {
                case "!id":
                case "!session":
                    if (string.IsNullOrEmpty(Context.Player.SessionID))
                    {
                        client?.SendMessage(e.ChatMessage.Channel, strings.string_not_in_session);
                    } else
                    {
                        parsed = strings.string_session_reply.Replace("{session}", Context.Player.SessionID);
                        client?.SendMessage(e.ChatMessage.Channel, parsed);
                    }
                break;
                case "!build":
                    parsed = strings.string_build_reply.Replace("{weaponName}", Context.Player.WeaponName).Replace("{url}", TinyURL);
                    client?.SendMessage(e.ChatMessage.Channel, parsed);
                    break;
                case "!rank":
                    parsed = strings.string_rank_reply.Replace("{playerName}", Context.Player.Name).Replace("{playerHR}", Context.Player.Level.ToString())
                        .Replace("playerMR", Context.Player.MasterRank.ToString()).Replace("{playerPlaytime}", TimeSpan.FromSeconds(Context.Player.PlayTime).ToString(@"dd\.hh\:mm\:ss"));
                    client?.SendMessage(e.ChatMessage.Channel, parsed);
                    break;
                default:
                    break;
            }
        }

        private void UpdateBuildLink(object source, EventArgs args)
        {
            BuildLink = Honey.LinkStructureBuilder(Context.Player.GetPlayerGear());
        }

        private void ConvertToTinyUrlSync(string link)
        {
            try
            {
                using (WebClient wClient = new WebClient())
                {
                    wClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                    string newUrl = wClient.DownloadString($"http://tinyurl.com/api-create.php?url={link}");
                    TinyURL = newUrl;
                }
            } catch { }
        }
    }

    public class TwitchStrings
    {
        public string string_not_in_session { get; set; } = "I'm currently not in a session :(";
        public string string_session_reply { get; set; } = "Session ID: {session}";
        public string string_build_reply { get; set; } = "Link to my current {weaponName} build: {url}";
        public string string_rank_reply { get; set; } = "{playerName} | HR: {playerHR} | MR: {playerMR} | PlayTime: {playerPlaytime}";
    }

    internal class ModConfig
    {
        public string Username { get; set; }
        public string OAuth { get; set; }
        public string Channel { get; set; }
    }
}
