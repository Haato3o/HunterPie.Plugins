using System;
using System.IO;
using HunterPie;
using System.Linq;
using HunterPie.Core;
using Newtonsoft.Json;
using Debugger = HunterPie.Logger.Debugger;
using System.Windows;
using System.Windows.Interop;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace HunterPie.Plugins
{
  public class DamageChat : IPlugin
  {
    public string Name { get; set; }
    public string Description { get; set; }
    public Game Context { get; set; }

    IntPtr hWnd;
    HwndSource source;

    public class DamageInformation
    {
      public float DamageValue { get; set; }
      public string DamageMessage { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput
    {
      public ushort wVk;
      public ushort wScan;
      public uint dwFlags;
      public uint time;
      public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
      public int dx;
      public int dy;
      public uint mouseData;
      public uint dwFlags;
      public uint time;
      public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HardwareInput
    {
      public uint uMsg;
      public ushort wParamL;
      public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
      [FieldOffset(0)] public MouseInput mi;
      [FieldOffset(0)] public KeyboardInput ki;
      [FieldOffset(0)] public HardwareInput hi;
    }

    public struct Input
    {
      public int type;
      public InputUnion u;
    }

    [Flags]
    public enum InputType
    {
      Mouse = 0,
      Keyboard = 1,
      Hardware = 2
    }

    [Flags]
    public enum KeyEventF
    {
      KeyDown = 0x0000,
      ExtendedKey = 0x0001,
      KeyUp = 0x0002,
      Unicode = 0x0004,
      Scancode = 0x0008
    }

    [Flags]
    public enum MouseEventF
    {
      Absolute = 0x8000,
      HWheel = 0x01000,
      Move = 0x0001,
      MoveNoCoalesce = 0x2000,
      LeftDown = 0x0002,
      LeftUp = 0x0004,
      RightDown = 0x0008,
      RightUp = 0x0010,
      MiddleDown = 0x0020,
      MiddleUp = 0x0040,
      VirtualDesk = 0x4000,
      Wheel = 0x0800,
      XDown = 0x0080,
      XUp = 0x0100
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetMessageExtraInfo();

    public void Initialize(Game context)
    {
      Name = "DamageChat";
      Description = "Post damage dealt to chat.";

      Context = context;

      SetHotkey();
    }

    public static string configSerialized = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Modules\\DamageChat", "config.json"));
    ModConfig config = JsonConvert.DeserializeObject<ModConfig>(configSerialized);

    public void Unload()
    {
      // Make sure to unregister the hotkey on Unload
      Application.Current.Dispatcher.Invoke(new Action(() =>
      {
        bool success = KeyboardHookHelper.UnregisterHotKey(hWnd, 999);

        // Make sure to remove the hook, so the next time you register a hotkey it won't
        // call the callback function multiple times
        source.RemoveHook(HwndHook);

        if (success)
        {
          Debugger.Log($"[{Name.ToUpper()}] Successfully unregistered hotkey!");
        }
        else
        {
          Debugger.Error($"[{Name.ToUpper()}] Failed to unregister hotkey.");
        }
      }));
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
        // KeyboardHookHelper.KeyboardKeys key = KeyboardHookHelper.KeyboardKeys.P;
        KeyboardHookHelper.KeyboardKeys key = (KeyboardHookHelper.KeyboardKeys)Enum.Parse(typeof(KeyboardHookHelper.KeyboardKeys), config.HotKey);

        // Hotkeys also need an id, you can choose whatever you want, just make sure it's unique
        int hotkeyId = 999;

        // Now we register the hotkey
        bool success = KeyboardHookHelper.RegisterHotKey(hWnd, hotkeyId, Modifiers, (int)key);

        if (success)
        {
          Debugger.Log($"[{Name.ToUpper()}] Successfully registered hotkey!");
        }
        else
        {
          Debugger.Error($"[{Name.ToUpper()}] Failed to register hotkey.");
        }
      }));
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
      const int WM_HOTKEY = 0x0312;
      switch (msg)
      {
        case WM_HOTKEY:
          switch (wParam.ToInt32())
          {
            // Check if it's our hotkey id
            case 999:
              List<Member> members = Context.Player.PlayerParty.Members;
              List<DamageInformation> damageInformation = new List<DamageInformation>();

              foreach (Member member in members)
              {
                if (member.Name != "" && member.Name != null)
                {
                  string DamageString = $"{member.Name} dealt {member.Damage} ({(Math.Floor(member.DamagePercentage * 100) / 100) * 100}%) damage";
                  damageInformation.Add(new DamageInformation { DamageValue = member.Damage, DamageMessage = DamageString });
                }
              }

              List<DamageInformation> sortedDamageInformation = damageInformation.OrderBy(i => i.DamageValue).ToList();
              sortedDamageInformation.Reverse();
              foreach (DamageInformation information in sortedDamageInformation)
              {
                Clipboard.SetText(information.DamageMessage);
                Debugger.Log($"[{Name.ToUpper()}] sent {information.DamageMessage}");
                KeyComboEvent(0x1D, 0x2F);

                // We want a short delay before pressing enter otherwise both functions play out at the same time, and sometimes causes nothing to send
                Thread.Sleep(100);
                KeyPressEvent(0x1C);
                Thread.Sleep(100);
              }

              // You can find a full list of DirectInput key codes here:
              // http://www.flint.jp/misc/?q=dik&lang=en
              break;
          }
          break;
      }
      return IntPtr.Zero;
    }

    private void KeyPressEvent(ushort code)
    {
      Input[] inputs = new Input[]
      {
        InputDownEvent(code),
        InputUpEvent(code)
      };

      SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
    }

    private void KeyComboEvent(ushort one, ushort two)
    {
      Input[] inputs = new Input[]
      {
        InputDownEvent(one),
        InputDownEvent(two),
        InputUpEvent(one),
        InputUpEvent(two)
      };

      SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
    }

    private Input InputUpEvent(ushort code)
    {
      return new Input
      {
        type = (int)InputType.Keyboard,
        u = new InputUnion
        {
          ki = new KeyboardInput
          {
            wVk = 0,
            wScan = code,
            dwFlags = (uint)(KeyEventF.KeyUp | KeyEventF.Scancode),
            dwExtraInfo = GetMessageExtraInfo()
          }
        }
      };
    }

    private Input InputDownEvent(ushort code)
    {
      return new Input
      {
        type = (int)InputType.Keyboard,
        u = new InputUnion
        {
          ki = new KeyboardInput
          {
            wVk = 0,
            wScan = code,
            dwFlags = (uint)(KeyEventF.KeyDown | KeyEventF.Scancode),
            dwExtraInfo = GetMessageExtraInfo()
          }
        }
      };
    }

    internal class ModConfig
    {
      public string HotKey { get; set; }
    }
  }
}