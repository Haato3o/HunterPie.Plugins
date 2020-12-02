using HunterPie;
using HunterPie.Core;
using HunterPie.Core.Events;
using HunterPie.Core.Input;
using Debugger = HunterPie.Logger.Debugger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;

namespace HunterPie.Plugins
{
    public class DiscordIntegration : IPlugin
    {
        List<int> hotkeyIds = new List<int>();

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
        private string WebHook { get; set; }
        private string DPSString { get; set; }

        public void Initialize(Game context)
        {
            Name = "DiscordWebhook";
            Description = "A HunterPie plugin posts MHW data to Discord.";

            Context = context;
            HookEvents();
            SetupDiscord();
            SetHotkey();
        }

        public void Unload()
        {
            UnhookEvents();
            UnsetHotkey();
        }

        private void SetHotkey()
        {
            Dictionary<string, Action> dummy = new Dictionary<string, Action>();
            dummy["Shift+Ctrl+P"] = myHotkeyDPS;
            dummy["Shift+Ctrl+G"] = myHotkeyGear;

            // Now we register them iterating the dummy dictionary
            foreach (string hk in dummy.Keys) {
                int hkId = Hotkey.Register(hk, dummy[hk]);
                if (hkId > 0) {
                    hotkeyIds.Add(hkId);
                }
            }
            dummy.Clear();
        }

        private void UnsetHotkey()
        {
            foreach (int hkId in hotkeyIds) {
                Hotkey.Unregister(hkId);
            }
            hotkeyIds.Clear();
        }

        private void myHotkeyDPS()
        {
            if (DPSString == null)
                return;

            PostToDiscord($"```{DPSString}```");
        }

        private void myHotkeyGear()
        {
            UpdateBuildLink();
            PostToDiscord(TinyURL);
        }

        private void HookEvents()
        {
            Context.FirstMonster.OnHPUpdate += UpdateDPSString;
            Context.SecondMonster.OnHPUpdate += UpdateDPSString;
            Context.ThirdMonster.OnHPUpdate += UpdateDPSString;
        }

        private void UnhookEvents()
        {
            Context.FirstMonster.OnHPUpdate -= UpdateDPSString;
            Context.SecondMonster.OnHPUpdate -= UpdateDPSString;
            Context.ThirdMonster.OnHPUpdate -= UpdateDPSString;
        }

        private void SetupDiscord()
        {
            if (!File.Exists(Path.Combine(Environment.CurrentDirectory, "Modules\\DiscordWebhook", "config.json")))
            {
                throw new FileNotFoundException("Config.json for DiscordClient plugin not found!");
            }
            string configSerialized = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "Modules\\DiscordWebhook", "config.json"));
            ModConfig config = JsonConvert.DeserializeObject<ModConfig>(configSerialized);

            WebHook = config.Webhook;
        }

        private void UpdateBuildLink()
        {
            BuildLink = Honey.LinkStructureBuilder(Context.Player.GetPlayerGear());
        }

        private void UpdateDPSString(object source, MonsterUpdateEventArgs args)
        {
            Monster sender = (Monster)source;
            float TimeElapsed = (float)Context.Player.PlayerParty.Epoch.TotalSeconds - (float)Context.Player.PlayerParty.TimeDifference.TotalSeconds;
            TimeSpan time = TimeSpan.FromSeconds(TimeElapsed);
            string duration = time .ToString(@"mm\:ss\:fff");
            DPSString = String.Format("{0,-15}{1:10}({2}/{3}){4,20}{5}\n", "Damage Meter", sender.Name, sender.Health, sender.MaxHealth, "Time: ", duration);
            DPSString += String.Format("{0,-25}{1,10}{2,12}{3,10}{4,10}\n", "Name(HR/MR)", "Weapon", "Damage", "Percent", "DPS");
            List<Member> members = Context.Player.PlayerParty.Members;
            foreach (Member member in members)
            {
                string name = FillWithSpaces($"{member.Name}({member.HR}/{member.MR})", 25, 1);
                string weapon = FillWithSpaces(GStrings.GetWeaponNameByID(member.Weapon), 10, 0);
                string name_weapon = name + weapon;
                if (name.Trim() != "(0/0)")
                {
                    float DPS = member.Damage / TimeElapsed;
                    int damage = member.Damage;
                    float percentage = member.DamagePercentage*100;
                    DPSString += String.Format("{0:-35}{1,12:N0}{2,10:0.00}%{3,10:0.00}/s\n", name_weapon, damage, percentage, DPS);
                }
            }
        }

        public string FillWithSpaces(string text, int width, int tail)
        {
            var font = new Font("Courier New", 10.0F);
            while (TextRenderer.MeasureText(text, font).Width < width*8)
                if (tail == 1)
                    text += ' ';
                else
                    text = ' ' + text;
            return text;
        }

        private void ConvertToTinyUrlSync(string link)
        {
            using (WebClient wClient = new WebClient())
            {
                wClient.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.121 Safari/537.36 OPR/71.0.3770.441");
                string newUrl = wClient.DownloadString($"http://tinyurl.com/api-create.php?url={link}");
                TinyURL = newUrl;
            }
        }

        private async void PostToDiscord(string msg)
        {
            if (WebHook == null)
                return;

            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(new HttpMethod("POST"), WebHook))
                {
                    var mydata = new
                    {
                         username = "",
                         content = msg
                    };

                    request.Content = new StringContent(JsonConvert.SerializeObject(mydata), Encoding.UTF8, "application/json");
                    var response = await httpClient.SendAsync(request);
                }
            }
        }
    }

    internal class ModConfig
    {
        public string Webhook { get; set; }
    }
}
