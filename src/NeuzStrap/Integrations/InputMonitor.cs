using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NeuzStrap.Integrations
{
    /// <summary>
    /// Watches a handful of gameplay keys and the mouse buttons for the on-screen overlay.
    ///
    /// It asks Windows for the state of those specific keys only (GetAsyncKeyState), so it is not a
    /// keylogger: no hook is installed and nothing you type is ever seen, stored or sent anywhere.
    /// </summary>
    public sealed class InputMonitor
    {
        public struct Key
        {
            public int Vk;
            public string Label;
            public Key(int vk, string label) { Vk = vk; Label = label; }
        }

        public static readonly Key[] Watched =
        {
            new Key(0x57, "W"), new Key(0x41, "A"), new Key(0x53, "S"), new Key(0x44, "D"),
            new Key(0x20, "SPACE"), new Key(0xA0, "SHIFT"),
        };

        const int VkLeftMouse = 0x01, VkRightMouse = 0x02;

        readonly bool[] _down = new bool[Watched.Length];
        readonly Queue<long> _leftClicks = new Queue<long>();
        readonly Queue<long> _rightClicks = new Queue<long>();
        readonly Stopwatch _clock = Stopwatch.StartNew();

        public bool IsDown(int index) => _down[index];
        public bool LeftMouseDown { get; private set; }
        public bool RightMouseDown { get; private set; }
        /// <summary>Clicks in the last second.</summary>
        public int LeftCps { get; private set; }
        public int RightCps { get; private set; }

        public void Poll()
        {
            for (int i = 0; i < Watched.Length; i++)
                _down[i] = (GetAsyncKeyState(Watched[i].Vk) & 0x8000) != 0;

            long now = _clock.ElapsedMilliseconds;
            short left = GetAsyncKeyState(VkLeftMouse);
            short right = GetAsyncKeyState(VkRightMouse);
            LeftMouseDown = (left & 0x8000) != 0;
            RightMouseDown = (right & 0x8000) != 0;
            if ((left & 0x0001) != 0) _leftClicks.Enqueue(now);   // pressed at least once since the last poll
            if ((right & 0x0001) != 0) _rightClicks.Enqueue(now);

            LeftCps = CountLastSecond(_leftClicks, now);
            RightCps = CountLastSecond(_rightClicks, now);
        }

        static int CountLastSecond(Queue<long> clicks, long now)
        {
            while (clicks.Count > 0 && now - clicks.Peek() > 1000) clicks.Dequeue();
            return clicks.Count;
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
    }
}
