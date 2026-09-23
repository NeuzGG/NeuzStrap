using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace NeuzStrap.Integrations
{
    /// <summary>
    /// Watches the keys you chose (and the mouse buttons) for the on-screen overlay.
    ///
    /// It asks Windows for the state of those specific keys only (GetAsyncKeyState), so it is not a
    /// keylogger: no hook is installed and nothing you type is ever seen, stored or sent anywhere.
    /// </summary>
    public sealed class InputMonitor
    {
        public struct WatchedKey
        {
            public string Name;
            public string Label;
            public int Vk;
        }

        const int VkLeftMouse = 0x01, VkRightMouse = 0x02;

        readonly WatchedKey[] _keys;
        readonly bool[] _down;
        readonly Queue<long> _leftClicks = new Queue<long>();
        readonly Queue<long> _rightClicks = new Queue<long>();
        readonly Queue<long> _keyPresses = new Queue<long>();
        readonly Stopwatch _clock = Stopwatch.StartNew();

        public InputMonitor(IEnumerable<string> keyNames)
        {
            _keys = KeyNames.Clean(keyNames)
                .Select(n => new WatchedKey { Name = n, Label = KeyNames.Label(n), Vk = KeyNames.ToVirtualKey(n) })
                .ToArray();
            _down = new bool[_keys.Length];
        }

        public int KeyCount => _keys.Length;
        public string[] Labels => _keys.Select(k => k.Label).ToArray();
        public string[] Names => _keys.Select(k => k.Name).ToArray();
        public bool IsDown(int index) => _down[index];
        public bool LeftMouseDown { get; private set; }
        public bool RightMouseDown { get; private set; }
        /// <summary>Clicks in the last second.</summary>
        public int LeftCps { get; private set; }
        public int RightCps { get; private set; }
        /// <summary>Presses of the watched keys in the last second.</summary>
        public int Kps { get; private set; }

        public void Poll()
        {
            // Read every key we care about once: the "pressed since last call" bit is cleared on read.
            var states = new Dictionary<int, short>();
            short Read(int vk)
            {
                if (!states.TryGetValue(vk, out short st)) states[vk] = st = GetAsyncKeyState(vk);
                return st;
            }

            long now = _clock.ElapsedMilliseconds;

            for (int i = 0; i < _keys.Length; i++)
            {
                short st = Read(_keys[i].Vk);
                _down[i] = (st & 0x8000) != 0;
                if ((st & 0x0001) != 0) _keyPresses.Enqueue(now);
            }

            short left = Read(VkLeftMouse), right = Read(VkRightMouse);
            LeftMouseDown = (left & 0x8000) != 0;
            RightMouseDown = (right & 0x8000) != 0;
            if ((left & 0x0001) != 0) _leftClicks.Enqueue(now);
            if ((right & 0x0001) != 0) _rightClicks.Enqueue(now);

            LeftCps = CountLastSecond(_leftClicks, now);
            RightCps = CountLastSecond(_rightClicks, now);
            Kps = CountLastSecond(_keyPresses, now);
        }

        static int CountLastSecond(Queue<long> stamps, long now)
        {
            while (stamps.Count > 0 && now - stamps.Peek() > 1000) stamps.Dequeue();
            return stamps.Count;
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
    }
}
