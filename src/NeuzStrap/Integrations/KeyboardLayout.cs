using System;
using System.Collections.Generic;

namespace NeuzStrap.Integrations
{
    /// <summary>Where a key sits on a real keyboard, in "key units" (1 unit = one normal key).</summary>
    public struct KeySlot
    {
        public float Col;
        public int Row;
        public float Width;
        public KeySlot(float col, int row, float width = 1f) { Col = col; Row = row; Width = width; }
    }

    /// <summary>
    /// Positions for the overlay's keyboard view. Rows and the staggered offsets match a normal
    /// keyboard, so W sits above and between A/S/D just like under your fingers.
    /// </summary>
    public static class KeyboardLayout
    {
        // row -1: F-keys | 0: numbers | 1: QWERTY | 2: ASDF | 3: ZXCV | 4: space row | 5: mouse
        const float Row1Offset = 1.5f, Row2Offset = 1.75f, Row3Offset = 2.25f;

        static readonly Dictionary<string, KeySlot> Slots = new Dictionary<string, KeySlot>(StringComparer.OrdinalIgnoreCase);

        static KeyboardLayout()
        {
            Slots["ESC"] = new KeySlot(0, 0);
            for (int i = 0; i < 12; i++) Slots["F" + (i + 1)] = new KeySlot(1.5f + i, -1);

            const string numbers = "1234567890";
            for (int i = 0; i < numbers.Length; i++) Slots[numbers[i].ToString()] = new KeySlot(1 + i, 0);

            const string row1 = "QWERTYUIOP";
            for (int i = 0; i < row1.Length; i++) Slots[row1[i].ToString()] = new KeySlot(Row1Offset + i, 1);
            Slots["TAB"] = new KeySlot(0, 1, 1.5f);

            const string row2 = "ASDFGHJKL";
            for (int i = 0; i < row2.Length; i++) Slots[row2[i].ToString()] = new KeySlot(Row2Offset + i, 2);
            Slots["CAPS"] = new KeySlot(0, 2, 1.75f);
            Slots["ENTER"] = new KeySlot(Row2Offset + row2.Length, 2, 2.25f);

            const string row3 = "ZXCVBNM";
            for (int i = 0; i < row3.Length; i++) Slots[row3[i].ToString()] = new KeySlot(Row3Offset + i, 3);
            Slots["SHIFT"] = new KeySlot(0, 3, 2.25f);
            Slots["RSHIFT"] = new KeySlot(Row3Offset + row3.Length + 1, 3, 2.75f);

            Slots["CTRL"] = new KeySlot(0, 4, 1.5f);
            Slots["ALT"] = new KeySlot(1.5f, 4, 1.25f);
            Slots["SPACE"] = new KeySlot(2.75f, 4, 5f);
            Slots["RCTRL"] = new KeySlot(7.75f, 4, 1.5f);
            Slots["BACK"] = new KeySlot(11, 0, 2f);

            Slots["UP"] = new KeySlot(13.5f, 3);
            Slots["LEFT"] = new KeySlot(12.5f, 4);
            Slots["DOWN"] = new KeySlot(13.5f, 4);
            Slots["RIGHT"] = new KeySlot(14.5f, 4);

            for (int i = 0; i <= 9; i++) Slots["NUM" + i] = new KeySlot(16 + i % 3, 4 - i / 3);

            // mouse buttons get their own little row under the keyboard
            Slots["MOUSE1"] = new KeySlot(0, 5, 1.5f);
            Slots["MOUSE2"] = new KeySlot(1.5f, 5, 1.5f);
            Slots["MOUSE3"] = new KeySlot(3f, 5, 1.5f);
            Slots["MOUSE4"] = new KeySlot(4.5f, 5, 1.5f);
            Slots["MOUSE5"] = new KeySlot(6f, 5, 1.5f);
        }

        public static KeySlot For(string name)
        {
            if (Slots.TryGetValue(name ?? "", out var slot)) return slot;
            return new KeySlot(0, 5); // anything unknown goes on the bottom row
        }

        /// <summary>
        /// Lays the chosen keys out on a mini keyboard: returns each key's box in key units, already
        /// shifted so the top-left of the used area is at 0,0.
        /// </summary>
        public static List<KeySlot> Arrange(IList<string> keyNames, out float widthUnits, out int rows)
        {
            var slots = new List<KeySlot>();
            float minCol = float.MaxValue, maxRight = 0;
            int minRow = int.MaxValue, maxRow = int.MinValue;

            foreach (var name in keyNames)
            {
                var slot = For(name);
                slots.Add(slot);
                minCol = Math.Min(minCol, slot.Col);
                maxRight = Math.Max(maxRight, slot.Col + slot.Width);
                minRow = Math.Min(minRow, slot.Row);
                maxRow = Math.Max(maxRow, slot.Row);
            }

            if (slots.Count == 0) { widthUnits = 0; rows = 0; return slots; }

            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                slots[i] = new KeySlot(s.Col - minCol, s.Row - minRow, s.Width);
            }
            widthUnits = maxRight - minCol;
            rows = maxRow - minRow + 1;
            return slots;
        }
    }
}
