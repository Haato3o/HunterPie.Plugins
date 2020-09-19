using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using HunterPie;
using HunterPie.Core;
using HunterPie.Core.Input;
using Debugger = HunterPie.Logger.Debugger;

namespace HunterPie.Plugins
{
  public class DamageChat : IPlugin
  {
    public string Name { get; set; }
    public string Description { get; set; }
    public Game Context { get; set; }

    // This variable will hold our hotkey id that we use to unregister it on unload,
    // for multiple hotkeys, you can use a List<int> instead, always adding the valid hotkey ids.
    private int hotKeyId;

    // This class is used to store both the damage count and the message
    // Primarily used to sort the message by the value (highest to lowest)
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

    public static string configSerialized = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Modules\\DamageChat", "config.json"));
    ModConfig config = JsonConvert.DeserializeObject<ModConfig>(configSerialized);

    public void Initialize(Game context)
    {
      Name = "DamageChat";
      Description = "Post damage dealt to chat.";

      Context = context;

      SetHotkeys();
    }

    public void SetHotkeys() 
    {
      // Hotkey.Register will try to add the hotkey, if it fails it will return -1
      // if it succeeds then it will return a valid hotkey id that you can use it unregister it.
      int hkId = Hotkey.Register(config.Hotkey, HotkeyCallback);
      if (hkId > 0) 
      {
        hotKeyId = hkId;
        this.Log("Hotkey registered successfully!");
      } 
      else 
      {
        this.Log("Hotkey failed to register.");
      }
    }

    public void HotkeyCallback() 
    {
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

      if (Game.IsWindowFocused) 
      {
        foreach (DamageInformation information in sortedDamageInformation)
        {
          // SetText can cause errors for some users
          // Clipboard.SetText(information.DamageMessage);
          Clipboard.SetData(DataFormats.Text, information.DamageMessage);

          KeyMultiEvent(0x1D, 0x2F);

          // We want a short delay before pressing enter otherwise both functions play out at the same time, and sometimes causes nothing to send
          Thread.Sleep(100);
          KeyPressEvent(0x1C);
          // We use another one here to prevent from two key events firing at the same time
          Thread.Sleep(100);
        }
      }
    }

    public void Unload()
    {
      // Now we can unregister the hotkey we registered
      // WE MUST UNREGISTER IT ON UNLOAD, if we don't then:
      // 1 - We'll create a memory leak that will only be resolved when HunterPie is closed
      // 2 - We will not be able to register this hotkey again next time the mod loads, unless HunterPie is restarted
      Hotkey.Unregister(hotKeyId);
    }

    private void KeyPressEvent(ushort one)
    {
      Input[] inputs = new Input[]
      {
        InputDownEvent(one),
        InputUpEvent(one)
      };

      SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
    }

    private void KeyMultiEvent(ushort one, ushort two)
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
      public string Hotkey { get; set; }
    }
  }
}