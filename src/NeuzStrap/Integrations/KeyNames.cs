using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace NeuzStrap.Integrations
{
    /// <summary>
    /// Turns the key names stored in settings ("W", "SPACE", "SHIFT", "MOUSE1"...) into Windows virtual-key
    /// codes, and back from a real key press when you pick a key in the settings.
    /// </summary>
    public static class KeyNames
    {
        static readonly Dictionary<string, int> Special = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["SPACE"] = 0x20, ["SHIFT"] = 0xA0, ["RSHIFT"] = 0xA1, ["CTRL"] = 0xA2, ["RCTRL"] = 0xA3,
            ["ALT"] = 0xA4, ["TAB"] = 0x09, ["ENTER"] = 0x0D, ["ESC"] = 0x1B, ["BACK"] = 0x08, ["CAPS"] = 0x14,
            ["UP"] = 0x26, ["DOWN"] = 0x28, ["LEFT"] = 0x25, ["RIGHT"] = 0x27,
            ["MOUSE1"] = 0x01, ["MOUSE2"] = 0x02, ["MOUSE3"] = 0x04, ["MOUSE4"] = 0x05, ["MOUSE5"] = 0x06,
        };

        public static readonly string[] Default = { "W", "A", "S", "D", "SPACE", "SHIFT" };

        /// <summary>Virtual-key code for a stored name, or -1 if it isn't one we support.</summary>
        public static int ToVirtualKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;
            name = name.Trim().ToUpperInvariant();
            if (Special.TryGetValue(name, out int vk)) return vk;
            if (name.Length == 1 && name[0] >= 'A' && name[0] <= 'Z') return name[0];
            if (name.Length == 1 && name[0] >= '0' && name[0] <= '9') return name[0];
            if (name.Length >= 2 && name[0] == 'F' && int.TryParse(name.Substring(1), out int f) && f >= 1 && f <= 12) return 0x70 + f - 1;
            return -1;
        }

        public static bool IsSupported(string name) => ToVirtualKey(name) >= 0;

        /// <summary>The name to store (and show) for a key the user just pressed. Null if it can't be used.</summary>
        public static string FromKeyPress(Keys key)
        {
            key &= Keys.KeyCode;
            if (key >= Keys.A && key <= Keys.Z) return key.ToString();
            if (key >= Keys.D0 && key <= Keys.D9) return ((char)('0' + (key - Keys.D0))).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return "NUM" + (key - Keys.NumPad0);
            if (key >= Keys.F1 && key <= Keys.F12) return key.ToString();

            switch (key)
            {
                case Keys.Space: return "SPACE";
                case Keys.ShiftKey:
                case Keys.LShiftKey: return "SHIFT";
                case Keys.RShiftKey: return "RSHIFT";
                case Keys.ControlKey:
                case Keys.LControlKey: return "CTRL";
                case Keys.RControlKey: return "RCTRL";
                case Keys.Menu:
                case Keys.LMenu:
                case Keys.RMenu: return "ALT";
                case Keys.Tab: return "TAB";
                case Keys.Enter: return "ENTER";
                case Keys.Escape: return "ESC";
                case Keys.Back: return "BACK";
                case Keys.CapsLock: return "CAPS";
                case Keys.Up: return "UP";
                case Keys.Down: return "DOWN";
                case Keys.Left: return "LEFT";
                case Keys.Right: return "RIGHT";
                default: return null;
            }
        }

        /// <summary>Short text drawn in the overlay for a key.</summary>
        public static string Label(string name)
        {
            switch ((name ?? "").ToUpperInvariant())
            {
                case "MOUSE1": return "LMB";
                case "MOUSE2": return "RMB";
                case "MOUSE3": return "MMB";
                case "RSHIFT": return "RSHIFT";
                case "RCTRL": return "RCTRL";
                default: return (name ?? "").ToUpperInvariant();
            }
        }

        /// <summary>Drops anything unusable and duplicates, and falls back to WASD when nothing is left.</summary>
        public static List<string> Clean(IEnumerable<string> names)
        {
            var result = new List<string>();
            if (names != null)
                foreach (var raw in names)
                {
                    string name = (raw ?? "").Trim().ToUpperInvariant();
                    if (IsSupported(name) && !result.Contains(name) && result.Count < 10) result.Add(name);
                }
            return result.Count > 0 ? result : new List<string>(Default);
        }

        /// <summary>NumPad keys use a name we also have to map back.</summary>
        static KeyNames()
        {
            for (int i = 0; i <= 9; i++) Special["NUM" + i] = 0x60 + i;
        }
    }
}
