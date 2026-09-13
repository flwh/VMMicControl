using System;
using System.Windows.Forms;

namespace VMMicControl
{
    internal enum HotkeyActionKind
    {
        ToggleChannel,
        MuteAll,
        UnmuteAll,
        ToggleAll
    }

    /// <summary>一条快捷键绑定</summary>
    internal sealed class HotkeyBinding
    {
        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModShift = 0x0004;

        public HotkeyBinding(HotkeyActionKind kind, int channelIndex, string label)
        {
            Kind = kind;
            ChannelIndex = channelIndex;
            Label = label;
            Modifiers = 0;
            VirtualKey = 0;
        }

        public HotkeyActionKind Kind { get; private set; }
        public int ChannelIndex { get; private set; }
        public string Label { get; set; }
        public uint Modifiers { get; set; }
        public uint VirtualKey { get; set; }
        public int Id { get; set; }

        public bool Enabled
        {
            get { return VirtualKey != 0; }
        }

        public string SettingsKey
        {
            get
            {
                switch (Kind)
                {
                    case HotkeyActionKind.MuteAll: return "muteall";
                    case HotkeyActionKind.UnmuteAll: return "unmuteall";
                    case HotkeyActionKind.ToggleAll: return "toggleall";
                    default: return "ch." + ChannelIndex;
                }
            }
        }

        public HotkeyBinding Clone()
        {
            HotkeyBinding copy = new HotkeyBinding(Kind, ChannelIndex, Label);
            copy.Modifiers = Modifiers;
            copy.VirtualKey = VirtualKey;
            return copy;
        }

        public void Clear()
        {
            Modifiers = 0;
            VirtualKey = 0;
        }

        public void Set(uint modifiers, uint virtualKey)
        {
            Modifiers = modifiers;
            VirtualKey = virtualKey;
        }

        public string ToSettingValue()
        {
            return Modifiers + "," + VirtualKey;
        }

        public void FromSettingValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            string[] parts = value.Split(new char[] { ',' }, 2);
            if (parts.Length != 2) return;
            uint m, v;
            if (uint.TryParse(parts[0], out m) && uint.TryParse(parts[1], out v)) Set(m, v);
        }

        public string DisplayText
        {
            get
            {
                if (!Enabled) return "未设置";
                string text = string.Empty;
                if ((Modifiers & ModControl) != 0) text += "Ctrl + ";
                if ((Modifiers & ModAlt) != 0) text += "Alt + ";
                if ((Modifiers & ModShift) != 0) text += "Shift + ";
                return text + KeyName(VirtualKey);
            }
        }

        public static uint ModifiersFromKeys(Keys modifiers)
        {
            uint mods = 0;
            if ((modifiers & Keys.Control) != 0) mods |= ModControl;
            if ((modifiers & Keys.Alt) != 0) mods |= ModAlt;
            if ((modifiers & Keys.Shift) != 0) mods |= ModShift;
            return mods;
        }

        public static bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
                   key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.LMenu || key == Keys.RMenu;
        }

        public static string KeyName(uint vk)
        {
            Keys k = (Keys)vk;
            if (k >= Keys.A && k <= Keys.Z) return k.ToString();
            if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (int)(k - Keys.D0))).ToString();
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num" + ((int)(k - Keys.NumPad0));
            if (k >= Keys.F1 && k <= Keys.F24) return k.ToString();
            switch (k)
            {
                case Keys.Space: return "Space";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.Oem1: return ";";
                case Keys.Oem2: return "/";
                case Keys.Oem3: return "`";
                case Keys.Oem4: return "[";
                case Keys.Oem5: return "\\";
                case Keys.Oem6: return "]";
                case Keys.Oem7: return "'";
                case Keys.Left: return "←";
                case Keys.Right: return "→";
                case Keys.Up: return "↑";
                case Keys.Down: return "↓";
                default: return k.ToString();
            }
        }
    }
}
