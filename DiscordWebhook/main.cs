
using System;
using System.IO;
using HunterPie;
using HunterPie.Core;
using HunterPie.Core.Events;
using Debugger = HunterPie.Logger.Debugger;
using Newtonsoft.Json;
using System.Net;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace HunterPie.Plugins
{
    public class DiscordIntegration : IPlugin
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Game Context { get; set; }

        IntPtr hWnd;
        HwndSource source;

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
            // We need the current window handle to register a global hotkey
            // HunterPie is multithreaded, so we should invoke to get the main window handle
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                hWnd = new WindowInteropHelper(Application.Current.MainWindow).EnsureHandle();
                source = HwndSource.FromHwnd(hWnd);

                // This is our "callback"
                source.AddHook(HwndHook);

                // Key modifiers
                // 0x1 = Alt; 0x2 = Ctrl; 0x4 = Shift
                int Modifiers = 0x2 | 0x4;

                // Setting the key to P, you can find the keys here: https://github.com/Haato3o/HunterPie/blob/master/HunterPie/Core/KeyboardHook.cs
                KeyboardHookHelper.KeyboardKeys keyP = KeyboardHookHelper.KeyboardKeys.P;
                KeyboardHookHelper.KeyboardKeys keyG = KeyboardHookHelper.KeyboardKeys.G;

                // Now we register the hotkey
                bool success = KeyboardHookHelper.RegisterHotKey(hWnd, 999, Modifiers, (int)keyP);
                success = success & KeyboardHookHelper.RegisterHotKey(hWnd, 998, Modifiers, (int)keyG);

                if (success)
                {
                    Debugger.Log("Registered hotkey!");
                } else
                {
                    Debugger.Error("Failed to register hotkey");
                }
            }));
        }

        private void UnsetHotkey()
        {
            // Make sure to unregister the hotkey on Unload
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                bool success = KeyboardHookHelper.UnregisterHotKey(hWnd, 999);
                success = success & KeyboardHookHelper.UnregisterHotKey(hWnd, 998);

                // Make sure to remove the hook, so the next time you register a hotkey it won't
                // call the callback function multiple times
                source.RemoveHook(HwndHook);
                
                if (success)
                {
                    Debugger.Log("Successfully unregistered hotkey");
                } else
                {
                    Debugger.Error("failed to unregister");
                }
            }));
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            string DiscordMsg = null;
            switch (msg)
            {
                case WM_HOTKEY:
                    switch (wParam.ToInt32())
                    {
                        // Check if it's our hotkey id
                        case 999: 
                            if (DPSString != null)
                                DiscordMsg = $"```{DPSString}```";
                            break;
                        case 998: 
                            DiscordMsg = BuildLink;
                            break;
                    }
                    break;
            }

            if (DiscordMsg != null)
                PostToDiscord(DiscordMsg);

            return IntPtr.Zero;
        }

        private void HookEvents()
        {
            Context.Player.OnCharacterLogin += UpdateBuildLink;
            Context.Player.OnSessionChange += UpdateBuildLink;
            Context.Player.OnWeaponChange += UpdateBuildLink;
            Context.Player.OnPeaceZoneEnter += StopCalculateDPS;
            Context.Player.OnPeaceZoneLeave += StartCalculateDPS;
        }

        private void UnhookEvents()
        {
            Context.Player.OnCharacterLogin -= UpdateBuildLink;
            Context.Player.OnSessionChange -= UpdateBuildLink;
            Context.Player.OnWeaponChange -= UpdateBuildLink;
            Context.Player.OnPeaceZoneEnter -= StopCalculateDPS;
            Context.Player.OnPeaceZoneLeave -= StartCalculateDPS;
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

       private void UpdateBuildLink(object source, EventArgs args)
       {
           BuildLink = Honey.LinkStructureBuilder(Context.Player.GetPlayerGear());
       }

       private void StartCalculateDPS(object source, EventArgs args)
       {
            Context.FirstMonster.OnHPUpdate += UpdateDPSString;
            Context.SecondMonster.OnHPUpdate += UpdateDPSString;
            Context.ThirdMonster.OnHPUpdate += UpdateDPSString;
        }

       private void StopCalculateDPS(object source, EventArgs args)
       {
           Context.FirstMonster.OnHPUpdate -= UpdateDPSString;
           Context.SecondMonster.OnHPUpdate -= UpdateDPSString;
           Context.ThirdMonster.OnHPUpdate -= UpdateDPSString;
       }

       private void UpdateDPSString(object source, MonsterUpdateEventArgs args)
       {
           Monster sender = (Monster)source;
           DPSString = $"Damage Meter({sender.Name}) {sender.Health}/{sender.MaxHealth}\n";
           float TimeElapsed = (float)Context.Player.PlayerParty.Epoch.TotalSeconds - (float)Context.Player.PlayerParty.TimeDifference.TotalSeconds;
           List<Member> members = Context.Player.PlayerParty.Members;
           foreach (Member member in members)
           {
               string name = member.Name;
               if (name != "")
               {
                   string DPS = $"{member.Damage / TimeElapsed:0.00}/s";
                   int damage = member.Damage;
                   float percentage = member.DamagePercentage;
                   DPSString += $"{name} {damage} {percentage*100:0.00}% DPS: {DPS}\n";
               }
           }
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