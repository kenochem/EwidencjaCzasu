// Ewidencja Czasu Pracy – ikonka przy zegarku, odbicia kart, przypomnienia, okna dialogowe
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Media;
using System.Windows.Forms;
using Microsoft.Win32;
using WinTimer = System.Windows.Forms.Timer;

namespace EwidencjaCzasu
{
    static class Colors
    {
        public static readonly Color Green = Color.FromArgb(22, 128, 61), Blue = Color.FromArgb(29, 78, 216),
            Gray = Color.FromArgb(75, 85, 99), Red = Color.FromArgb(185, 28, 28), Orange = Color.FromArgb(194, 65, 12);
    }

    class TrayApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "EwidencjaCzasu";
        const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x1;

        readonly NotifyIcon tray;
        readonly Control ui;
        readonly KeyboardCapture capture;
        readonly ToolStripMenuItem pauseItem, autoItem;
        readonly WinTimer clock;
        readonly DateTime started = DateTime.Now;
        EmployeesForm employeesForm;
        PanelForm panel;
        ContextMenuStrip menu;
        bool dialogOpen, awake;
        DateTime reminderDay, backupWarnDay, lastDailyBackup;

        public TrayApp(bool restarted)
        {
            ui = new Control();
            ui.CreateControl();
            var h = ui.Handle;

            var menu = new ContextMenuStrip();
            var panelItem = new ToolStripMenuItem("Panel zarządzania", null, (s, e) => ShowPanel());
            menu.Items.Add(panelItem);
            menu.Items.Add("Pracownicy i karty…", null, (s, e) => { if (Unlocked("Pracownicy i karty")) ShowEmployees(); });
            menu.Items.Add("Otwórz folder z danymi", null, (s, e) =>
            {
                if (Unlocked("Folder z danymi")) Process.Start("explorer.exe", "\"" + Store.Dir + "\"");
            });
            menu.Items.Add(new ToolStripSeparator());
            pauseItem = new ToolStripMenuItem("Wstrzymaj przechwytywanie czytnika", null, (s, e) => { if (Unlocked("Wstrzymanie czytnika")) TogglePause(); });
            autoItem = new ToolStripMenuItem("Uruchamiaj razem z Windows", null, (s, e) => { if (Unlocked("Autostart")) ToggleAutostart(); });
            EnsureAutostart();
            autoItem.Checked = AutoStartRegistered;
            menu.Items.Add(pauseItem);
            menu.Items.Add(autoItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Zamknij program", null, (s, e) => Quit());
            this.menu = menu;
            StyleMenu();
            Theme.Changed += () => { StyleMenu(); tray.Icon = MakeIcon(pauseItem.Checked ? Colors.Gray : Theme.Accent); };

            tray = new NotifyIcon
            {
                Icon = MakeIcon(Theme.Accent),
                Text = "Ewidencja czasu pracy – czytnik aktywny",
                ContextMenuStrip = menu,
                Visible = true
            };
            tray.DoubleClick += (s, e) => ShowPanel();

            capture = new KeyboardCapture();
            capture.CardRead += code => ui.BeginInvoke(new Action(() => OnCard(code)));
            capture.Error += msg => ui.BeginInvoke(new Action(() => Toast.Popup("Błąd", msg, Colors.Red, 8000)));
            capture.Start();

            clock = new WinTimer { Interval = 30000 };
            clock.Tick += (s, e) => OnClock();
            clock.Start();
            UpdateAwake();
            lastDailyBackup = DateTime.Now;

            if (Program.TestCrash && !restarted)
            {
                // tylko do sprawdzenia automatycznego restartu po awarii
                var boom = new WinTimer { Interval = 3000 };
                boom.Tick += (s, e) => { boom.Stop(); throw new InvalidOperationException("Test awarii"); };
                boom.Start();
            }

            SystemEvents.PowerModeChanged += OnPowerMode;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.SessionEnding += OnSessionEnding;

            if (!CheckDowntime(restarted))
                Toast.Popup(restarted ? "Program uruchomiony ponownie" : "Ewidencja czasu działa",
                    restarted ? "Wystąpił błąd – program sam się zrestartował.\nCzytnik znów działa." : "Przyłóż kartę do czytnika.\nPanel: dwuklik na ikonce przy zegarku.",
                    restarted ? Colors.Orange : Colors.Blue, 5000);
        }

        // ---------------------------------------------------------- ciągłość działania

        // Sprawdza, czy program nie działał przez dłuższą chwilę (restart, wyłączenie, uśpienie).
        // Zwraca true, jeśli pokazano ostrzeżenie.
        bool CheckDowntime(bool restarted)
        {
            try
            {
                var last = Store.LastHeartbeat();
                var now = DateTime.Now;
                Store.Heartbeat();
                if (!last.HasValue || (now - last.Value).TotalMinutes < 2) return false;
                Store.LogDowntime(last.Value, now);
                var inWork = Store.DowntimeInWorkHours(last.Value, now);
                if (inWork.TotalMinutes < 2) return false;
                string range = last.Value.Date == now.Date
                    ? last.Value.ToString("HH:mm") + "–" + now.ToString("HH:mm")
                    : last.Value.ToString("dd.MM HH:mm") + " – " + now.ToString("dd.MM HH:mm");
                SystemSounds.Exclamation.Play();
                Toast.Popup("Program nie działał: " + range,
                    "Jeśli ktoś się w tym czasie odbijał – niech przyłoży kartę ponownie\nalbo uzupełnij odbicie w panelu.", Colors.Orange, 30000);
                return true;
            }
            catch { return false; }
        }

        void OnPowerMode(object s, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend) { try { Store.Heartbeat(); } catch { } }
            if (e.Mode == PowerModes.Resume)
            {
                capture.Reinstall();
                ui.BeginInvoke(new Action(() => CheckDowntime(false)));
            }
        }

        void OnSessionSwitch(object s, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect || e.Reason == SessionSwitchReason.RemoteConnect)
                capture.Reinstall();
        }

        void OnSessionEnding(object s, SessionEndingEventArgs e)
        {
            // wylogowanie / wyłączenie / restart Windows – zapisujemy ostatni moment działania
            try { Store.Heartbeat(); } catch { }
        }

        void StyleMenu()
        {
            menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors()) { RoundedEdges = false };
            menu.Font = Theme.UI(10);
            menu.BackColor = Theme.Surface;
            foreach (ToolStripItem it in menu.Items)
            {
                it.ForeColor = Theme.Text;
                if (!(it is ToolStripSeparator)) it.Padding = new Padding(4, 5, 4, 5);
            }
            menu.Items[0].Font = Theme.Semi(10);
        }

        // Akcje administracyjne wymagają PIN-u (chyba że panel jest właśnie otwarty – wtedy PIN już podano).
        bool Unlocked(string why)
        {
            if (panel != null && !panel.IsDisposed) return true;
            return Pin.Require(why);
        }

        // ---------------------------------------------------------- odbicie karty

        void OnCard(string uid)
        {
            try
            {
                if (employeesForm != null && !employeesForm.IsDisposed)
                {
                    employeesForm.AddOrSelectCard(uid);
                    Toast.Popup("Tryb edycji kart", "Karta " + uid + " – odbicie NIE zostało zapisane\n(zamknij okno Pracownicy, aby się odbijać).", Colors.Gray, 4000);
                    return;
                }
                if (dialogOpen)
                {
                    Toast.Popup("Chwileczkę", "Najpierw dokończ otwarte okno programu.", Colors.Gray, 3000);
                    return;
                }

                var emps = Store.LoadEmployees();
                var emp = Store.FindEmployee(emps, uid);
                if (emp == null) { RegisterNewCard(uid, emps); return; }
                if (!emp.Active)
                {
                    SystemSounds.Hand.Play();
                    Toast.Popup("Karta wyłączona", emp.Name + "\nKarta " + uid + " jest nieaktywna.", Colors.Red, 5000);
                    return;
                }

                var now = DateTime.Now;
                string n = Store.Norm(uid);
                var mine = Store.LoadPunches().Where(p => Store.Norm(p.Uid) == n && p.Time <= now).ToList();
                var last = mine.LastOrDefault();
                if (last != null && (now - last.Time).TotalSeconds < Cfg.DebounceSeconds)
                {
                    Toast.Popup("Już zarejestrowano", string.Format("{0}\n{1} o {2:HH:mm}", emp.Name, last.Type.ToLower(), last.Time), Colors.Gray, 3000);
                    return;
                }

                var lastToday = mine.LastOrDefault(p => p.Time.Date == now.Date);
                string type = lastToday != null && lastToday.Type == Store.IN ? Store.OUT : Store.IN;
                var punch = new Punch { Time = now, Uid = emp.Uid, Name = emp.Name, Type = type, Source = "KARTA" };
                bool saved = Store.AppendPunch(punch);
                RefreshPanel();

                string first = emp.Name.Split(' ')[0];
                string warn = saved ? "" : "\nUwaga: zamknij odbicia.csv w Excelu (zapis tymczasowy).";
                SystemSounds.Asterisk.Play();
                if (type == Store.IN)
                {
                    // przypomnienie o niewybitym wyjściu z poprzedniego dnia pracy
                    var prev = mine.LastOrDefault(p => p.Time.Date < now.Date);
                    bool missedExit = prev != null && prev.Type == Store.IN;
                    string extra = missedExit ? "\nUwaga: " + prev.Time.ToString("dd.MM") + " brak odbicia wyjścia – zgłoś godzinę." : "";
                    Toast.Popup("Dzień dobry, " + first + "!", "WEJŚCIE  " + now.ToString("HH:mm") + extra + warn,
                        saved && !missedExit ? Colors.Green : Colors.Orange, missedExit ? 9000 : 4000);
                }
                else
                {
                    var day = Calc.Day(Data.Load(), emp.Uid, now.Date);
                    string vsNorm = day.WorkingDay && day.HasNorm ? "  (" + Calc.Signed(day.Total - day.DayNorm) + ")" : "";
                    Toast.Popup("Do widzenia, " + first + "!", "WYJŚCIE  " + now.ToString("HH:mm") + "   •   dziś: " + Calc.Hm(day.Total) + " h" + vsNorm + warn,
                        saved ? Colors.Blue : Colors.Orange, 5000);
                }
            }
            catch (Exception ex)
            {
                SystemSounds.Hand.Play();
                Toast.Popup("Błąd zapisu odbicia", ex.Message, Colors.Red, 8000);
            }
        }

        void RegisterNewCard(string uid, List<Employee> emps)
        {
            SystemSounds.Exclamation.Play();
            dialogOpen = true;
            try
            {
                if (!Unlocked("Wykryto nową kartę: " + uid + "\nDodanie karty wymaga PIN-u administratora."))
                {
                    Toast.Popup("Nieznana karta", "Karta " + uid + " nie jest przypisana do nikogo.", Colors.Gray, 5000);
                    return;
                }
                string name = InputForm.Ask("Nowa karta",
                    "Wykryto nową kartę o numerze:\n" + uid + "\n\nWpisz imię i nazwisko osoby, do której należy:", "");
                if (string.IsNullOrWhiteSpace(name)) return;
                emps.Add(new Employee { Uid = uid, Name = name.Trim(), Active = true });
                Store.SaveEmployees(emps);
                Store.Audit("NOWA KARTA", "", uid + " " + name.Trim());
                RefreshPanel();
                Toast.Popup("Dodano: " + name.Trim(), "Karta " + uid + " jest gotowa.\nPrzyłóż ją ponownie, aby się odbić.", Colors.Green, 5000);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się zapisać pracownika:\n" + ex.Message, "Ewidencja Czasu");
            }
            finally { dialogOpen = false; }
        }

        // ---------------------------------------------------------- zadania co 30 s

        void OnClock()
        {
            try { Store.Heartbeat(); } catch { }
            UpdateAwake();
            CheckReminder();
            if ((DateTime.Now - started).TotalMinutes >= 2) CheckBackup();
            if ((DateTime.Now - lastDailyBackup).TotalHours >= 1) { lastDailyBackup = DateTime.Now; Store.DailyBackup(); }
        }

        // Komputer nie usypia w dni robocze w ustawionych godzinach (ekran może się wyłączać).
        void UpdateAwake()
        {
            var now = DateTime.Now;
            bool on = Cfg.KeepAwake && PlCalendar.IsWorkingDay(now) && now.TimeOfDay >= Cfg.AwakeFrom && now.TimeOfDay < Cfg.AwakeTo;
            if (on == awake) return;
            Native.SetThreadExecutionState(on ? ES_CONTINUOUS | ES_SYSTEM_REQUIRED : ES_CONTINUOUS);
            awake = on;
        }

        void CheckReminder()
        {
            var now = DateTime.Now;
            if (!Cfg.ReminderOn || reminderDay == now.Date || now.TimeOfDay < Cfg.ReminderAt || !PlCalendar.IsWorkingDay(now) || Store.IsCompanyDayOff(now)) return;
            reminderDay = now.Date;
            try
            {
                var data = Data.Load();
                var inside = data.Emps.Where(e => e.Active)
                    .Where(e => { var l = data.DayPunches(e.Uid, now.Date); return l.Count > 0 && l[l.Count - 1].Type == Store.IN; })
                    .Select(e => e.Name).ToList();
                if (inside.Count == 0) return;
                SystemSounds.Exclamation.Play();
                Toast.Popup("Pamiętajcie o odbiciu wyjścia", "Nadal w biurze: " + string.Join(", ", inside) + "\nKliknij, aby zamknąć.", Colors.Orange, 120000);
            }
            catch { }
        }

        void CheckBackup()
        {
            var today = DateTime.Today;
            if (string.IsNullOrWhiteSpace(Cfg.BackupFolder))
            {
                if (backupWarnDay == today) return;
                backupWarnDay = today;
                Toast.Popup("Kopia zapasowa nie jest ustawiona", "Panel → Ustawienia → folder kopii\n(np. Dysk Google).", Colors.Orange, 8000);
                return;
            }
            if (!CloudBackup.Due) return;
            try
            {
                CloudBackup.Run();
                Toast.Popup("Kopia zapasowa zapisana", "Dane ewidencji skopiowane do:\n" + Cfg.BackupFolder, Colors.Green, 5000);
            }
            catch (Exception ex)
            {
                if (backupWarnDay == today) return;
                backupWarnDay = today;
                Toast.Popup("Nie udało się zrobić kopii", ex.Message, Colors.Orange, 10000);
            }
        }

        // ---------------------------------------------------------- okna

        void ShowPanel()
        {
            if (panel == null || panel.IsDisposed)
            {
                if (!Pin.Require("Otwarcie panelu zarządzania")) return;
                panel = new PanelForm(ShowEmployees);
                panel.FormClosed += (s, e) => panel = null;
                panel.Show();
            }
            if (panel.WindowState == FormWindowState.Minimized) panel.WindowState = FormWindowState.Normal;
            panel.Activate();
        }

        void RefreshPanel()
        {
            if (panel != null && !panel.IsDisposed) panel.RefreshAll();
        }

        void ShowEmployees()
        {
            if (employeesForm != null && !employeesForm.IsDisposed) { employeesForm.Activate(); return; }
            employeesForm = new EmployeesForm();
            employeesForm.FormClosed += (s, e) => { employeesForm = null; RefreshPanel(); };
            employeesForm.Show();
            employeesForm.Activate();
        }

        void TogglePause()
        {
            bool pause = !pauseItem.Checked;
            pauseItem.Checked = pause;
            capture.SetEnabled(!pause);
            tray.Icon = MakeIcon(pause ? Colors.Gray : Theme.Accent);
            tray.Text = pause ? "Ewidencja czasu – WSTRZYMANA" : "Ewidencja czasu pracy – czytnik aktywny";
            Store.Audit(pause ? "CZYTNIK WSTRZYMANY" : "CZYTNIK WZNOWIONY", "", "");
            Toast.Popup(pause ? "Przechwytywanie wstrzymane" : "Przechwytywanie włączone",
                pause ? "Czytnik wpisuje teraz numery jak zwykła klawiatura." : "Odbicia kart są rejestrowane.", pause ? Colors.Orange : Colors.Green, 3000);
        }

        static bool AutoStartRegistered
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunName) != null;
            }
        }

        static string RunCommand { get { return "\"" + Application.ExecutablePath + "\""; } }

        // Autostart jest domyślnie włączony; program poprawia wpis, jeśli plik .exe został przeniesiony.
        static void EnsureAutostart()
        {
            if (Program.CustomDataDir) return;
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    var current = k.GetValue(RunName) as string;
                    if (Cfg.AutoStart && current != RunCommand) k.SetValue(RunName, RunCommand);
                    if (!Cfg.AutoStart && current != null) k.DeleteValue(RunName, false);
                }
            }
            catch { }
        }

        void ToggleAutostart()
        {
            Cfg.AutoStart = !AutoStartRegistered;
            try { Cfg.Save(Store.CfgFile); } catch { }
            EnsureAutostart();
            autoItem.Checked = AutoStartRegistered;
            Store.Audit(Cfg.AutoStart ? "AUTOSTART WŁĄCZONY" : "AUTOSTART WYŁĄCZONY", "", "");
        }

        void Quit()
        {
            if (!Unlocked("Zamknięcie programu")) return;
            if (MessageBox.Show("Zamknąć program? Odbicia kart nie będą rejestrowane.", "Ewidencja Czasu",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Store.Audit("ZAMKNIĘCIE PROGRAMU", "", "");
            try { Store.Heartbeat(); } catch { }
            SystemEvents.PowerModeChanged -= OnPowerMode;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.SessionEnding -= OnSessionEnding;
            Native.SetThreadExecutionState(ES_CONTINUOUS);
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }

        static Icon MakeIcon(Color color)
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            using (var pen = new Pen(Color.White, 3))
            using (var brush = new SolidBrush(color))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, 1, 1, 30, 30);
                g.DrawLine(pen, 16, 16, 16, 7);
                g.DrawLine(pen, 16, 16, 23, 19);
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }

    // ------------------------------------------------------------------ dymek w rogu ekranu

    // Nowoczesny dymek: karta z ikoną, tytułem, treścią i paskiem pozostałego czasu.
    class Toast : Form
    {
        static Toast current;
        const int W = 480;
        readonly WinTimer timer, anim;
        readonly string title, text, kind;
        readonly int duration;
        readonly DateTime shownAt = DateTime.Now;

        public static void Popup(string title, string text, Color color, int ms)
        {
            string kind = color == Colors.Green ? "success" : color == Colors.Blue ? "info" : color == Colors.Orange ? "warning"
                : color == Colors.Red ? "danger" : "neutral";
            if (current != null && !current.IsDisposed) current.Close();
            current = new Toast(title, text, kind, ms);
            current.Show();
        }

        Toast(string title, string text, string kind, int ms)
        {
            this.title = title;
            this.text = text;
            this.kind = kind;
            duration = ms;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Theme.DoubleBuffer(this);
            int bodyH = TextRenderer.MeasureText(text, Theme.UI(11.5f), new Size(W - 112, 0), TextFormatFlags.WordBreak).Height;
            Size = new Size(W, Math.Max(108, 66 + bodyH + 22));
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 18, wa.Bottom - Height - 18);
            Click += (s, e) => Close();

            timer = new WinTimer { Interval = ms };
            timer.Tick += (s, e) => Close();
            timer.Start();
            anim = new WinTimer { Interval = 50 };
            anim.Tick += (s, e) => Invalidate(new Rectangle(0, Height - 4, Width, 4));
            anim.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 /* NOACTIVATE */ | 0x80 /* TOOLWINDOW */ | 0x8 /* TOPMOST */;
                cp.ClassStyle |= 0x20000; // cień
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.RoundCorners(this);
        }

        // Wyraziste kolory niezależnie od motywu: zielony = wejście, grafitowy = wyjście,
        // pomarańczowy = ostrzeżenie, czerwony = błąd – czytelne z daleka przy czytniku.
        static Color Fill(string kind, string title)
        {
            if (title.StartsWith("Do widzenia")) return Color.FromArgb(71, 85, 105);   // grafit
            switch (kind)
            {
                case "success": return Color.FromArgb(22, 163, 74);
                case "warning": return Color.FromArgb(234, 88, 12);
                case "danger": return Color.FromArgb(220, 38, 38);
                case "info": return Color.FromArgb(37, 99, 235);
                default: return Color.FromArgb(75, 85, 99);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var fill = Fill(kind, title);
            g.Clear(fill);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            string glyph = kind == "success" ? "" : kind == "warning" ? "" : kind == "danger" ? "" : "";
            if (title.StartsWith("Do widzenia")) glyph = "";
            var icon = new RectangleF(22, 20, 52, 52);
            using (var b = new SolidBrush(Color.FromArgb(55, 255, 255, 255))) g.FillEllipse(b, icon);
            Theme.Glyph(g, glyph, 18, Color.White, icon);

            TextRenderer.DrawText(g, title, Theme.Semi(17f), new Rectangle(90, 14, Width - 102, 36), Color.White,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            // krótki komunikat (np. „WEJŚCIE 07:58”) – duży i wyraźny, żeby był czytelny z daleka
            bool big = text.Length <= 40 && text.IndexOf('\n') < 0;
            TextRenderer.DrawText(g, text, big ? Theme.Semi(14f) : Theme.UI(11.5f), new Rectangle(90, big ? 52 : 54, Width - 112, Height - 60),
                Color.FromArgb(big ? 255 : 235, 255, 255, 255), TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);

            double left = 1 - (DateTime.Now - shownAt).TotalMilliseconds / duration;
            if (left > 0)
                using (var b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillRectangle(b, 0, Height - 4, (float)(Width * left), 4);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Dispose();
            anim.Dispose();
            base.OnFormClosed(e);
        }
    }

    // ------------------------------------------------------------------ okna dialogowe

    class InputForm : ThemedForm
    {
        readonly TextBox box;

        InputForm(string title, string prompt, string value, bool password)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ClientSize = new Size(440, 230);
            var label = new Label { Text = prompt, Location = new Point(14, 12), Size = new Size(412, 126) };
            box = new TextBox { Text = value, Location = new Point(14, 142), Width = 412, UseSystemPasswordChar = password };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(244, 184), Size = new Size(88, 32) };
            var cancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Location = new Point(338, 184), Size = new Size(88, 32) };
            Controls.AddRange(new Control[] { label, box, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public static string Ask(string title, string prompt, string value, bool password = false)
        {
            using (var f = new InputForm(title, prompt, value, password))
            {
                f.Shown += (s, e) => { f.Activate(); f.box.Focus(); };
                return f.ShowDialog() == DialogResult.OK ? f.box.Text : null;
            }
        }
    }

    // Pracownicy i karty: lista osób, numery kart, urlop, kiedy karta była ostatnio użyta.
    class EmployeesForm : ThemedForm
    {
        const int ColUid = 0, ColName = 1, ColContract = 2, ColEtat = 3, ColActive = 4, ColLeave = 5, ColCarry = 6, ColLast = 7;
        readonly DataGridView grid;
        readonly Banner banner;
        readonly Data data;
        bool dirty, loading;

        public EmployeesForm()
        {
            Text = "Pracownicy i karty";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1080, 620);
            MinimumSize = new Size(880, 460);
            Padding = new Padding(24, 18, 24, 18);
            data = Data.Load();

            // nagłówek
            var header = new Panel { Dock = DockStyle.Top, Height = 70 };
            header.Controls.Add(new Label { Text = "Pracownicy i karty", Font = Theme.Semi(18), AutoSize = true, Location = new Point(0, 0) });
            header.Controls.Add(new Label { Text = "Numery kart, forma zatrudnienia, etat i urlop. Przy umowie zlecenie, o dzieło i B2B liczy się tylko przepracowany czas.", Tag = "muted", AutoSize = true, Location = new Point(2, 38) });
            var actions = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
            actions.Controls.Add(Theme.Button("  Dodaj ręcznie", "", (s, e) => AddManual()));
            actions.Controls.Add(Theme.Button("  Usuń kartę", "danger", (s, e) => DeleteSelected()));
            header.Controls.Add(actions);

            banner = new Banner { Dock = DockStyle.Fill, Kind = "accent", Glyph = "" };
            var bannerHost = new Panel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(0, 0, 0, 14) };
            bannerHost.Controls.Add(banner);
            SetHint();

            grid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false, EditMode = DataGridViewEditMode.EditOnEnter
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Numer karty", FillWeight = 105 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Imię i nazwisko", FillWeight = 165 });
            var contractCol = new DataGridViewComboBoxColumn { HeaderText = "Forma zatrudnienia", FillWeight = 140, FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing };
            contractCol.Items.AddRange(Contracts.Names);
            grid.Columns.Add(contractCol);
            var etatCol = new DataGridViewComboBoxColumn { HeaderText = "Etat", FillWeight = 90, FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing };
            etatCol.Items.AddRange(Contracts.EtatNames);
            foreach (var emp in data.Emps)
                if (!etatCol.Items.Contains(Contracts.EtatName(emp.Etat))) etatCol.Items.Add(Contracts.EtatName(emp.Etat));
            grid.Columns.Add(etatCol);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Aktywna", FillWeight = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Urlop (dni / rok)", FillWeight = 75 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Urlop zaległy", FillWeight = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ostatnio użyta", FillWeight = 100, ReadOnly = true });
            foreach (DataGridViewColumn c in grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns[ColActive].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns[ColLeave].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns[ColCarry].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.RowTemplate.Height = 38;
            Theme.StyleGrid(grid);
            grid.CellValueChanged += (s, e) =>
            {
                if (loading || e.RowIndex < 0) return;
                MarkDirty();
                if (e.ColumnIndex == ColContract) UpdateRowState(grid.Rows[e.RowIndex]);
            };
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty && (grid.CurrentCell is DataGridViewCheckBoxCell || grid.CurrentCell is DataGridViewComboBoxCell))
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.DataError += (s, e) => { e.ThrowException = false; };
            grid.EditingControlShowing += (s, e) =>
            {
                var cb = e.Control as ComboBox;
                if (cb == null) return;
                cb.BackColor = Theme.SurfaceAlt;
                cb.ForeColor = Theme.Text;
                cb.FlatStyle = FlatStyle.Flat;
            };
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var row = grid.Rows[e.RowIndex];
                bool active = row.Cells[ColActive].Value is bool && (bool)row.Cells[ColActive].Value;
                if (!active) e.CellStyle.ForeColor = Theme.Muted;
                if (e.ColumnIndex == ColLast || (e.ColumnIndex == ColUid && row.Cells[ColUid].ReadOnly)) e.CellStyle.ForeColor = Theme.Muted;
                // przy umowach cywilnoprawnych etat i urlop nie mają zastosowania
                if ((e.ColumnIndex == ColEtat || e.ColumnIndex == ColLeave || e.ColumnIndex == ColCarry) && !IsUop(row))
                {
                    e.Value = "—";
                    e.CellStyle.ForeColor = Theme.Muted;
                    e.FormattingApplied = true;
                }
            };
            var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(10) };
            card.Controls.Add(grid);

            // pasek dolny
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(0, 16, 0, 0) };
            var save = new Button { Text = "  Zapisz", Tag = "primary", Dock = DockStyle.Right, Width = 130 };
            var close = new Button { Text = "Zamknij", Dock = DockStyle.Right, Width = 110 };
            save.Click += (s, e) => Save(true);
            close.Click += (s, e) => Close();
            bottom.Controls.Add(new Label { Text = "Urlop: 20 dni (staż poniżej 10 lat) lub 26 dni (10 lat i więcej, wliczając szkołę).", Tag = "muted", AutoSize = true, Location = new Point(0, 26) });
            bottom.Controls.Add(close);
            bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10 });
            bottom.Controls.Add(save);

            Controls.Add(card);
            Controls.Add(bottom);
            Controls.Add(bannerHost);
            Controls.Add(header);

            loading = true;
            foreach (var emp in data.Emps)
            {
                int i = grid.Rows.Add(emp.Uid, emp.Name, Contracts.Name(emp.Contract), Contracts.EtatName(emp.Etat), emp.Active,
                    emp.LeaveDays.ToString(), emp.LeaveCarry.ToString(), LastUse(emp.Uid));
                grid.Rows[i].Cells[ColUid].ReadOnly = true; // numer karty zmienia się przez przyłożenie nowej karty
                UpdateRowState(grid.Rows[i]);
            }
            loading = false;
            Shown += (s, e) => grid.ClearSelection();
        }

        static bool IsUop(DataGridViewRow row)
        {
            return Contracts.Code(Convert.ToString(row.Cells[ColContract].Value)) == "UOP";
        }

        // etat i urlop można edytować tylko przy umowie o pracę
        void UpdateRowState(DataGridViewRow row)
        {
            bool uop = IsUop(row);
            row.Cells[ColEtat].ReadOnly = !uop;
            row.Cells[ColLeave].ReadOnly = !uop;
            row.Cells[ColCarry].ReadOnly = !uop;
            grid.InvalidateRow(row.Index);
        }

        string LastUse(string uid)
        {
            var last = data.Punches.LastOrDefault(p => Store.Norm(p.Uid) == Store.Norm(uid));
            if (last == null) return "nigdy";
            var days = (DateTime.Today - last.Time.Date).Days;
            return days == 0 ? "dziś " + last.Time.ToString("HH:mm") : days == 1 ? "wczoraj " + last.Time.ToString("HH:mm") : last.Time.ToString("d.MM.yyyy");
        }

        void SetHint()
        {
            banner.Kind = "accent";
            banner.Glyph = "";
            banner.Text = "Przyłóż kartę do czytnika – nowa karta pojawi się na liście. Osobę, która odeszła, odznacz w kolumnie „Aktywna” (historia zostaje).";
        }

        void MarkDirty()
        {
            if (dirty) return;
            dirty = true;
            Text = "Pracownicy i karty •  niezapisane zmiany";
        }

        public void AddOrSelectCard(string uid)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (Store.Norm(Convert.ToString(row.Cells[ColUid].Value)) != Store.Norm(uid)) continue;
                grid.ClearSelection();
                row.Selected = true;
                grid.CurrentCell = row.Cells[ColName];
                banner.Kind = "info";
                banner.Glyph = "";
                banner.Text = "Ta karta należy do: " + Convert.ToString(row.Cells[ColName].Value) + ".";
                Activate();
                return;
            }
            int i = grid.Rows.Add(uid, "", Contracts.Names[0], Contracts.EtatNames[0], true, "26", "0", "nigdy");
            grid.Rows[i].Cells[ColUid].ReadOnly = true;
            MarkDirty();
            banner.Kind = "success";
            banner.Glyph = "";
            banner.Text = "Dodano kartę " + uid + " – wpisz imię i nazwisko, a potem kliknij „Zapisz”.";
            FocusName(i);
        }

        void AddManual()
        {
            int i = grid.Rows.Add("", "", Contracts.Names[0], Contracts.EtatNames[0], true, "26", "0", "nigdy");
            MarkDirty();
            grid.ClearSelection();
            grid.Rows[i].Selected = true;
            grid.CurrentCell = grid.Rows[i].Cells[ColUid];
            Activate();
            grid.BeginEdit(true);
        }

        void FocusName(int i)
        {
            grid.ClearSelection();
            grid.Rows[i].Selected = true;
            grid.CurrentCell = grid.Rows[i].Cells[ColName];
            Activate();
            grid.BeginEdit(true);
        }

        void DeleteSelected()
        {
            if (grid.CurrentRow == null) { MessageBox.Show("Najpierw zaznacz osobę na liście.", Text); return; }
            var row = grid.CurrentRow;
            string uid = Convert.ToString(row.Cells[ColUid].Value).Trim();
            string name = Convert.ToString(row.Cells[ColName].Value).Trim();
            bool history = uid != "" && (data.Punches.Any(p => Store.Norm(p.Uid) == Store.Norm(uid)) || data.Absences.Any(a => Store.Norm(a.Uid) == Store.Norm(uid)));
            if (history)
            {
                MessageBox.Show(name + " ma już historię odbić lub nieobecności, więc nie można jej usunąć.\n\n" +
                    "Odznacz „Aktywna” – osoba zniknie z bieżących list, a jej historia zostanie w raportach.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("Usunąć kartę " + (uid == "" ? "(bez numeru)" : uid) + (name != "" ? " – " + name : "") + "?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            grid.Rows.Remove(row);
            MarkDirty();
        }

        bool Save(bool close)
        {
            grid.EndEdit();
            var list = new List<Employee>();
            var seen = new HashSet<string>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                string uid = Convert.ToString(row.Cells[ColUid].Value).Trim();
                string name = Convert.ToString(row.Cells[ColName].Value).Trim();
                if (uid == "" && name == "") continue;
                if (uid == "" || name == "")
                {
                    MessageBox.Show("Każdy wiersz musi mieć numer karty oraz imię i nazwisko.", Text);
                    grid.CurrentCell = row.Cells[uid == "" ? ColUid : ColName];
                    return false;
                }
                if (!uid.All(char.IsDigit))
                {
                    MessageBox.Show("Numer karty może zawierać tylko cyfry: " + uid, Text);
                    return false;
                }
                if (!seen.Add(Store.Norm(uid)))
                {
                    MessageBox.Show("Karta " + uid + " jest wpisana więcej niż raz.", Text);
                    return false;
                }
                int leave, carry;
                if (!int.TryParse(Convert.ToString(row.Cells[ColLeave].Value).Trim(), out leave) || leave < 0 || leave > 60 ||
                    !int.TryParse(Convert.ToString(row.Cells[ColCarry].Value).Trim(), out carry) || carry < 0 || carry > 120)
                {
                    MessageBox.Show("Nieprawidłowa liczba dni urlopu u: " + name, Text);
                    return false;
                }
                list.Add(new Employee
                {
                    Uid = uid, Name = name, Active = row.Cells[ColActive].Value is bool && (bool)row.Cells[ColActive].Value,
                    LeaveDays = leave, LeaveCarry = carry,
                    Contract = Contracts.Code(Convert.ToString(row.Cells[ColContract].Value)),
                    Etat = Contracts.EtatOf(Convert.ToString(row.Cells[ColEtat].Value))
                });
            }
            try
            {
                Store.SaveEmployees(list);
                Store.Audit("ZMIANA LISTY PRACOWNIKÓW", "", string.Join(", ", list.Select(e => e.Name)));
                dirty = false;
                if (close) Close();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się zapisać:\n" + ex.Message, Text);
                return false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!dirty || e.Cancel) return;
            var r = MessageBox.Show("Zapisać zmiany na liście pracowników?", "Pracownicy i karty", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel || (r == DialogResult.Yes && !Save(false))) e.Cancel = true;
        }
    }

    // Dodawanie nowego odbicia (existing == null) albo poprawianie istniejącego.
    class PunchForm : ThemedForm
    {
        public Punch Result;
        readonly ComboBox who, type;
        readonly DateBox date;
        readonly TimeBox time;
        readonly List<Employee> emps;

        public PunchForm(Punch existing, string defaultUid, DateTime defaultDate)
        {
            Text = existing == null ? "Dodaj odbicie ręcznie" : "Popraw odbicie";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 250);

            emps = Store.LoadEmployees().OrderBy(e => !e.Active).ThenBy(e => e.Name).ToList();
            string selUid = existing != null ? existing.Uid : defaultUid;
            if (existing != null && Store.FindEmployee(emps, existing.Uid) == null)
                emps.Add(new Employee { Uid = existing.Uid, Name = existing.Name, Active = false });

            who = new ThemedCombo { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(130, 16), Width = 250 };
            foreach (var e in emps) who.Items.Add(e.Active ? e.Name : e.Name + " (nieaktywny)");
            int idx = selUid == null ? -1 : emps.FindIndex(e => Store.Norm(e.Uid) == Store.Norm(selUid));
            if (idx < 0 && emps.Count > 0) idx = 0;
            who.SelectedIndex = idx;

            var start = existing != null ? existing.Time : defaultDate.Date + DateTime.Now.TimeOfDay;
            date = new DateBox(DateBox.Mode.Day, start) { Location = new Point(130, 54), Width = 250 };
            time = new TimeBox(new TimeSpan(start.Hour, start.Minute, 0)) { Location = new Point(130, 96) };
            type = new ThemedCombo { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(130, 136), Width = 250 };
            type.Items.AddRange(new object[] { Store.IN, Store.OUT });
            type.SelectedItem = existing != null ? existing.Type : Store.OUT;

            Controls.Add(new Label { Text = "Pracownik:", Location = new Point(14, 19), AutoSize = true });
            Controls.Add(new Label { Text = "Data:", Location = new Point(14, 59), AutoSize = true });
            Controls.Add(new Label { Text = "Godzina:", Location = new Point(14, 99), AutoSize = true });
            Controls.Add(new Label { Text = "Rodzaj:", Location = new Point(14, 139), AutoSize = true });
            var ok = new Button { Text = existing == null ? "Dodaj" : "Zapisz", Location = new Point(196, 200), Size = new Size(88, 32) };
            var cancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Location = new Point(292, 200), Size = new Size(88, 32) };
            ok.Click += (s, e) =>
            {
                if (who.SelectedIndex < 0) { MessageBox.Show("Brak pracowników na liście.", Text); return; }
                var emp = emps[who.SelectedIndex];
                if (!time.Time.HasValue) { MessageBox.Show("Wpisz godzinę w formacie GG:MM, np. 16:05.", Text); return; }
                var t = date.Value.Date + time.Time.Value;
                // przy poprawce bez zmiany godziny zachowujemy oryginalne sekundy
                if (existing != null && t == existing.Time.Date + new TimeSpan(existing.Time.Hour, existing.Time.Minute, 0)) t = existing.Time;
                string source = existing == null ? "RĘCZNIE"
                    : existing.Source == "KARTA" ? "KARTA-POPRAWIONE" : existing.Source;
                Result = new Punch { Time = t, Uid = emp.Uid, Name = emp.Name, Type = (string)type.SelectedItem, Source = source };
                DialogResult = DialogResult.OK;
            };
            Controls.AddRange(new Control[] { who, date, time, type, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }

    class AbsenceForm : ThemedForm
    {
        public List<Absence> Result;
        readonly ComboBox who, type;
        readonly DateBox from, to;
        readonly TextBox note;
        readonly CheckBox workOnly;
        readonly List<Employee> emps;

        public AbsenceForm(string defaultUid, DateTime defaultDate)
        {
            Text = "Dodaj nieobecność";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 320);

            // urlopy, L4 itp. dotyczą tylko umowy o pracę
            emps = Store.LoadEmployees().Where(e => e.Active && e.HasNorm).OrderBy(e => e.Name).ToList();
            who = new ThemedCombo { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(130, 16), Width = 320 };
            foreach (var e in emps) who.Items.Add(e.Name);
            int idx = defaultUid == null ? 0 : Math.Max(0, emps.FindIndex(e => Store.Norm(e.Uid) == Store.Norm(defaultUid)));
            if (emps.Count > 0) who.SelectedIndex = idx;
            type = new ThemedCombo { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(130, 56), Width = 320 };
            type.Items.AddRange(AbsTypes.All);
            type.SelectedIndex = 0;
            from = new DateBox(DateBox.Mode.Day, defaultDate) { Location = new Point(130, 94), Width = 145 };
            to = new DateBox(DateBox.Mode.Day, defaultDate) { Location = new Point(305, 94), Width = 145 };
            from.ValueChanged += (s, e) => { if (to.Value < from.Value) to.Value = from.Value; };
            workOnly = new CheckBox { Text = "Tylko dni robocze (pomiń weekendy i święta)", Location = new Point(130, 134), AutoSize = true, Checked = true };
            note = new TextBox { Location = new Point(130, 172), Width = 320 };
            type.SelectedIndexChanged += (s, e) => workOnly.Checked = ((AbsType)type.SelectedItem).Code != "CH";

            Controls.Add(new Label { Text = "Pracownik:", Location = new Point(14, 19), AutoSize = true });
            Controls.Add(new Label { Text = "Rodzaj:", Location = new Point(14, 59), AutoSize = true });
            Controls.Add(new Label { Text = "Od:", Location = new Point(14, 99), AutoSize = true });
            Controls.Add(new Label { Text = "–", Location = new Point(283, 101), AutoSize = true });
            Controls.Add(new Label { Text = "Uwagi:", Location = new Point(14, 175), AutoSize = true });
            Controls.Add(new Label
            {
                Text = "Jeśli w danym dniu jest już wpisana nieobecność tej osoby, zostanie zastąpiona.",
                Location = new Point(14, 214), Size = new Size(440, 40), ForeColor = Color.DimGray
            });
            var ok = new Button { Text = "Dodaj", Location = new Point(266, 270), Size = new Size(88, 32) };
            var cancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Location = new Point(362, 270), Size = new Size(88, 32) };
            ok.Click += (s, e) =>
            {
                if (who.SelectedIndex < 0) { MessageBox.Show("Brak aktywnych pracowników na umowie o pracę.", Text); return; }
                if ((to.Value.Date - from.Value.Date).TotalDays > 366) { MessageBox.Show("Maksymalnie rok naraz.", Text); return; }
                var emp = emps[who.SelectedIndex];
                var t = (AbsType)type.SelectedItem;
                Result = new List<Absence>();
                for (var d = from.Value.Date; d <= to.Value.Date; d = d.AddDays(1))
                    if (!workOnly.Checked || PlCalendar.IsWorkingDay(d))
                        Result.Add(new Absence { Day = d, Uid = emp.Uid, Name = emp.Name, Code = t.Code, Note = note.Text.Trim() });
                if (Result.Count == 0) { MessageBox.Show("W wybranym okresie nie ma dni roboczych.", Text); return; }
                DialogResult = DialogResult.OK;
            };
            Controls.AddRange(new Control[] { who, type, from, to, workOnly, note, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }

    class SettingsForm : ThemedForm
    {
        readonly TimeBox workFrom, workTo, nightFrom, nightTo, remindAt, awakeFrom, awakeTo;
        readonly NumericUpDown norm, tol, every;
        readonly CheckBox remind, awake;
        readonly TextBox folder;
        readonly Label lastLbl;
        readonly string originalFolder = Cfg.BackupFolder, originalMode = Cfg.ThemeMode, originalAccent = Cfg.Accent;
        readonly ThemedCombo mode;
        readonly List<Swatch> swatches = new List<Swatch>();

        void Section(string text, int y)
        {
            Controls.Add(new Label { Text = text, Location = new Point(20, y), AutoSize = true, Font = Theme.Semi(12) });
            Controls.Add(new Divider { Location = new Point(24 + TextRenderer.MeasureText(text, Theme.Semi(12)).Width + 8, y + 13), Size = new Size(600, 1) });
        }

        // podgląd motywu na żywo (cofany przy „Anuluj”)
        void Preview()
        {
            Cfg.ThemeMode = Theme.Modes[Math.Max(0, mode.SelectedIndex)];
            var sel = swatches.FirstOrDefault(x => x.Selected);
            Cfg.Accent = Theme.AccentNames[sel != null ? sel.Index : 0];
            Theme.Load();
            Theme.RaiseChanged();
        }

        static TimeBox TP(TimeSpan t, int x, int y) { return new TimeBox(t) { Location = new Point(x, y + 1) }; }

        static TimeSpan Of(TimeBox p) { return p.Time ?? TimeSpan.Zero; }

        Label L(string text, int x, int y)
        {
            var l = new Label { Text = text, Location = new Point(x, y + 4), AutoSize = true };
            Controls.Add(l);
            return l;
        }

        public SettingsForm()
        {
            Text = "Ustawienia";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(660, 760);

            int y = 16;
            Section("Wygląd", y); y += 34;
            L("Motyw:", 24, y);
            mode = new ThemedCombo { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(250, y), Width = 200 };
            mode.Items.AddRange(Theme.ModeNames);
            mode.SelectedIndex = Math.Max(0, Array.IndexOf(Theme.Modes, Cfg.ThemeMode));
            mode.SelectedIndexChanged += (s, e) => Preview();
            Controls.Add(mode);
            y += 42;
            L("Kolor akcentu:", 24, y + 5);
            for (int i = 0; i < Theme.AccentNames.Length; i++)
            {
                var sw = new Swatch(i) { Location = new Point(246 + i * 42, y) };
                sw.Selected = Theme.AccentNames[i] == Cfg.Accent;
                sw.Click += (s, e) => { foreach (var x in swatches) { x.Selected = x == s; x.Invalidate(); } Preview(); };
                swatches.Add(sw);
                Controls.Add(sw);
            }
            y += 52;
            Section("Czas pracy", y); y += 34;
            L("Godziny pracy:", 24, y); workFrom = TP(Cfg.WorkStart, 250, y); L("do", 338, y); workTo = TP(Cfg.WorkEnd, 368, y);
            y += 38;
            L("Norma dobowa (godz.):", 24, y);
            norm = new NumericUpDown { Location = new Point(250, y), Width = 80, Minimum = 1, Maximum = 16, DecimalPlaces = 1, Increment = 0.5m, Value = (decimal)Cfg.NormHours };
            y += 38;
            L("Spóźnienie liczone po (min):", 24, y);
            tol = new NumericUpDown { Location = new Point(250, y), Width = 80, Minimum = 0, Maximum = 120, Value = Cfg.LateToleranceMin };
            y += 38;
            L("Pora nocna:", 24, y); nightFrom = TP(Cfg.NightFrom, 250, y); L("do", 338, y); nightTo = TP(Cfg.NightTo, 368, y);
            y += 52;
            Section("Przypomnienia i komputer", y); y += 34;
            remind = new CheckBox { Text = "Przypomnienie o odbiciu wyjścia o godz.:", Location = new Point(24, y), AutoSize = true, Checked = Cfg.ReminderOn };
            remindAt = TP(Cfg.ReminderAt, 368, y);
            y += 38;
            awake = new CheckBox { Text = "Nie usypiaj komputera w dni robocze od", Location = new Point(24, y), AutoSize = true, Checked = Cfg.KeepAwake };
            awakeFrom = TP(Cfg.AwakeFrom, 368, y); L("do", 456, y); awakeTo = TP(Cfg.AwakeTo, 486, y);
            y += 52;
            Section("Kopia zapasowa", y); y += 34;
            L("Folder (np. Dysk Google):", 24, y);
            y += 28;
            folder = new TextBox { Location = new Point(24, y + 4), Width = 380, Text = Cfg.BackupFolder };
            var browse = new Button { Text = "  Wybierz", Location = new Point(412, y), Size = new Size(110, 36) };
            var detect = new Button { Text = "Dysk Google", Location = new Point(528, y), Size = new Size(110, 36) };
            y += 48;
            L("Kopia co (dni):", 24, y + 4);
            every = new NumericUpDown { Location = new Point(140, y + 2), Width = 60, Minimum = 1, Maximum = 60, Value = Cfg.BackupEveryDays };
            var last = CloudBackup.Last;
            lastLbl = L("Ostatnia: " + (last.HasValue ? last.Value.ToString("yyyy-MM-dd HH:mm") : "jeszcze nie było"), 216, y + 4);
            lastLbl.Tag = "muted";
            var now = new Button { Text = "  Zrób kopię teraz", Location = new Point(458, y), Size = new Size(180, 36) };
            y += 64;
            var pin = new Button { Text = "  Zmień PIN", Location = new Point(18, y), Size = new Size(140, 38), Tag = "ghost" };
            var ok = new Button { Text = "Zapisz", Location = new Point(428, y), Size = new Size(100, 38) };
            var cancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Location = new Point(538, y), Size = new Size(100, 38) };
            ClientSize = new Size(660, y + 58);

            browse.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog { Description = "Wybierz folder na kopie zapasowe", SelectedPath = folder.Text })
                    if (d.ShowDialog(this) == DialogResult.OK) folder.Text = d.SelectedPath;
            };
            detect.Click += (s, e) =>
            {
                var g = CloudBackup.DetectGoogleDrive();
                if (g != null) folder.Text = g;
                else MessageBox.Show("Nie znaleziono Dysku Google na tym komputerze.\n\nZainstaluj aplikację „Dysk Google na komputer” (Google Drive for desktop), " +
                    "zaloguj się na konto firmowe i kliknij ten przycisk ponownie.", Text);
            };
            now.Click += (s, e) =>
            {
                Cfg.BackupFolder = folder.Text.Trim();
                try
                {
                    var path = CloudBackup.Run();
                    lastLbl.Text = "Ostatnia kopia: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    MessageBox.Show("Kopia zapisana:\n" + path, Text);
                }
                catch (Exception ex) { MessageBox.Show("Nie udało się zrobić kopii:\n" + ex.Message, Text); }
            };
            pin.Click += (s, e) => Pin.Setup(true);
            ok.Click += (s, e) =>
            {
                if (new[] { workFrom, workTo, nightFrom, nightTo, remindAt, awakeFrom, awakeTo }.Any(x => !x.Time.HasValue))
                { MessageBox.Show("Wpisz wszystkie godziny w formacie GG:MM, np. 08:00.", Text); return; }
                if (Of(workTo) <= Of(workFrom)) { MessageBox.Show("Godzina zakończenia pracy musi być późniejsza niż rozpoczęcia.", Text); return; }
                Cfg.WorkStart = Of(workFrom);
                Cfg.WorkEnd = Of(workTo);
                Cfg.NormHours = (double)norm.Value;
                Cfg.LateToleranceMin = (int)tol.Value;
                Cfg.NightFrom = Of(nightFrom);
                Cfg.NightTo = Of(nightTo);
                Cfg.ReminderOn = remind.Checked;
                Cfg.ReminderAt = Of(remindAt);
                Cfg.KeepAwake = awake.Checked;
                Cfg.AwakeFrom = Of(awakeFrom);
                Cfg.AwakeTo = Of(awakeTo);
                Cfg.BackupFolder = folder.Text.Trim();
                Cfg.BackupEveryDays = (int)every.Value;
                try
                {
                    Cfg.Save(Store.CfgFile);
                    Store.Audit("ZMIANA USTAWIEŃ", "", string.Format("praca {0}-{1}, norma {2} h", Cfg.T(Cfg.WorkStart), Cfg.T(Cfg.WorkEnd), Cfg.NormHours));
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex) { MessageBox.Show("Nie udało się zapisać ustawień:\n" + ex.Message, Text); }
            };
            FormClosed += (s, e) =>
            {
                if (DialogResult == DialogResult.OK) return;
                Cfg.BackupFolder = originalFolder;
                if (Cfg.ThemeMode != originalMode || Cfg.Accent != originalAccent)
                {
                    Cfg.ThemeMode = originalMode;
                    Cfg.Accent = originalAccent;
                    Theme.Load();
                    Theme.RaiseChanged();
                }
            };

            Controls.AddRange(new Control[] { workFrom, workTo, norm, tol, nightFrom, nightTo, remind, remindAt, awake, awakeFrom, awakeTo,
                folder, browse, detect, every, now, pin, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
