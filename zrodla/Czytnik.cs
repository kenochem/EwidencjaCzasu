// Przechwytywanie czytnika kart (USB w trybie klawiatury)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace EwidencjaCzasu
{
    struct KeyEvt
    {
        public ushort Vk, Scan;
        public bool Up, Extended;
        public uint Time;
    }

    // Globalny hak klawiatury. Cyfry są na chwilę wstrzymywane: jeśli przyjdzie
    // min. 8 cyfr + Enter w tempie czytnika (każdy znak < max_odstep_ms), to jest karta
    // i nic nie trafia do aktywnego okna. W każdym innym przypadku wstrzymane klawisze
    // są natychmiast odtwarzane w oryginalnej kolejności (opóźnienie ok. 50 ms).
    class KeyboardCapture
    {
        public event Action<string> CardRead;
        public event Action<string> Error;
        public Action<List<KeyEvt>> ReplaySink;

        static readonly IntPtr ReplayMarker = new IntPtr(0x52435031);
        Form host;
        WinTimer timer;
        IntPtr hook = IntPtr.Zero;
        Native.LowLevelKeyboardProc proc;
        readonly List<KeyEvt> buffer = new List<KeyEvt>();
        readonly List<KeyEvt> replay = new List<KeyEvt>();
        bool flushScheduled, swallowEnterUp, enabled = true;
        uint lastTime, swallowSince;
        int lastReinstall;

        public KeyboardCapture() { ReplaySink = SendKeys; }

        public void Start()
        {
            var ready = new System.Threading.ManualResetEvent(false);
            var thread = new System.Threading.Thread(() =>
            {
                host = new Form();
                var h = host.Handle;
                proc = HookProc;
                Install();
                timer = new WinTimer { Interval = 15 };
                timer.Tick += OnTick;
                timer.Start();
                ready.Set();
                Application.Run();
            });
            thread.IsBackground = true;
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            ready.WaitOne();
        }

        public void SetEnabled(bool on)
        {
            host.BeginInvoke(new Action(() =>
            {
                enabled = on;
                if (on) Install(); else { Uninstall(); buffer.Clear(); }
            }));
        }

        // Po wybudzeniu z uśpienia / odblokowaniu Windows hak bywa po cichu odpięty – podpinamy go od nowa.
        public void Reinstall()
        {
            host.BeginInvoke(new Action(() =>
            {
                if (!enabled) return;
                Uninstall();
                buffer.Clear();
                Install();
            }));
        }

        void Install()
        {
            if (hook != IntPtr.Zero) return;
            hook = Native.SetWindowsHookEx(13 /* WH_KEYBOARD_LL */, proc, Native.GetModuleHandle(null), 0);
            lastReinstall = Environment.TickCount;
            if (hook == IntPtr.Zero && Error != null)
                Error("Nie udało się włączyć przechwytywania czytnika (kod " + Marshal.GetLastWin32Error() + ").");
        }

        void Uninstall()
        {
            if (hook == IntPtr.Zero) return;
            Native.UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
        }

        void OnTick(object s, EventArgs e)
        {
            if (buffer.Count > 0 && unchecked((uint)Environment.TickCount - lastTime) > (uint)Cfg.MaxGapMs)
                MoveBufferToReplay();
            // Windows potrafi po cichu odpiąć hak (np. po wybudzeniu) – odświeżamy go co 5 minut
            if (enabled && buffer.Count == 0 && replay.Count == 0 && Environment.TickCount - lastReinstall > 300000)
            {
                Uninstall();
                Install();
            }
        }

        IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                bool injected = (k.flags & 0x10) != 0;
                if (k.dwExtraInfo != ReplayMarker && (!injected || Cfg.AcceptInjected))
                {
                    int msg = wParam.ToInt32();
                    var ev = new KeyEvt
                    {
                        Vk = (ushort)k.vkCode,
                        Scan = (ushort)k.scanCode,
                        Up = msg == 0x101 || msg == 0x105,
                        Extended = (k.flags & 1) != 0,
                        Time = k.time
                    };
                    if (Process(ev)) return (IntPtr)1;
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        static bool IsDigit(ushort vk) { return (vk >= 0x30 && vk <= 0x39) || (vk >= 0x60 && vk <= 0x69); }
        static char DigitChar(ushort vk) { return (char)('0' + (vk >= 0x60 ? vk - 0x60 : vk - 0x30)); }

        bool IsDown(ushort vk)
        {
            int n = 0;
            foreach (var b in buffer) if (b.Vk == vk) n += b.Up ? -1 : 1;
            return n > 0;
        }

        string Digits()
        {
            var sb = new StringBuilder();
            foreach (var b in buffer) if (!b.Up) sb.Append(DigitChar(b.Vk));
            return sb.ToString();
        }

        // Zwraca true, jeśli klawisz ma zostać zatrzymany (nie trafi teraz do aktywnego okna).
        public bool Process(KeyEvt e)
        {
            if (swallowEnterUp)
            {
                if (e.Vk == 0x0D && e.Up) { swallowEnterUp = false; return true; }
                if (unchecked(e.Time - swallowSince) > 1000) swallowEnterUp = false;
            }

            if (buffer.Count > 0 && unchecked(e.Time - lastTime) > (uint)Cfg.MaxGapMs)
                MoveBufferToReplay();

            if (buffer.Count == 0)
            {
                if (!e.Up && IsDigit(e.Vk)) { buffer.Add(e); lastTime = e.Time; return true; }
                // czekają jeszcze wcześniejsze klawisze do odtworzenia – zachowujemy kolejność
                if (flushScheduled) { replay.Add(e); return true; }
                return false;
            }

            if (IsDigit(e.Vk))
            {
                bool down = IsDown(e.Vk);
                if (!e.Up && !down) { buffer.Add(e); lastTime = e.Time; return true; }
                if (e.Up && down) { buffer.Add(e); lastTime = e.Time; return true; }
                // ten sam klawisz wciśnięty dwa razy bez puszczenia = przytrzymanie przez człowieka
            }
            else if (e.Vk == 0x0D && !e.Up)
            {
                string code = Digits();
                if (code.Length >= Cfg.MinDigits && code.Length <= 24)
                {
                    buffer.Clear();
                    swallowEnterUp = true;
                    swallowSince = e.Time;
                    var h = CardRead;
                    if (h != null) h(code);
                    return true;
                }
            }

            buffer.Add(e);
            MoveBufferToReplay();
            return true;
        }

        void MoveBufferToReplay()
        {
            replay.AddRange(buffer);
            buffer.Clear();
            if (flushScheduled) return;
            flushScheduled = true;
            if (host != null) host.BeginInvoke(new Action(Flush));
            else Flush();
        }

        void Flush()
        {
            flushScheduled = false;
            if (replay.Count == 0) return;
            var copy = new List<KeyEvt>(replay);
            replay.Clear();
            ReplaySink(copy);
        }

        static void SendKeys(List<KeyEvt> keys)
        {
            var inputs = new Native.INPUT[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                inputs[i].type = 1; // INPUT_KEYBOARD
                inputs[i].u.ki.wVk = keys[i].Vk;
                inputs[i].u.ki.wScan = keys[i].Scan;
                inputs[i].u.ki.dwFlags = (keys[i].Up ? 2u : 0u) | (keys[i].Extended ? 1u : 0u);
                inputs[i].u.ki.dwExtraInfo = ReplayMarker;
            }
            Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
        }
    }

    static class Native
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public InputUnion u; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("kernel32.dll")]
        public static extern uint SetThreadExecutionState(uint flags);
    }
}
