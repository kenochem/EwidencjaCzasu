// Ewidencja Czasu Pracy – panel zarządzania
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace EwidencjaCzasu
{
    class ComboItem
    {
        public string Text, Uid;
        public override string ToString() { return Text; }
    }

    class PanelForm : ThemedForm
    {
        static readonly CultureInfo Pl = new CultureInfo("pl-PL");
        const int TabToday = 0, TabSchedule = 1, TabPunches = 2, TabAbsences = 3, TabYear = 4, TabSummary = 5, TabStats = 6;

        class Page
        {
            public string Title, Glyph;
            public Panel Content;
            public Control[] Actions = new Control[0];
            public NavButton Nav;
        }

        readonly List<Page> pages = new List<Page>();
        int current = -1;
        readonly Label title, subtitle;
        readonly FlowLayoutPanel actions;
        readonly NavButton themeNav;
        readonly WinTimer refreshTimer, tickTimer, lockTimer;
        bool loading;

        // obecność dziś
        readonly DataGridView todayGrid;
        readonly StatCard kIn, kAbs, kLate, kIssues;
        readonly Banner issuesBanner, downBanner;
        readonly Panel issuesHost, downHost;
        DateTime issuesMonth;
        class LiveRow { public DataGridViewRow Row; public TimeSpan Done; public DateTime OpenSince; public bool WorkingDay, HasNorm; public TimeSpan Norm; }
        readonly List<LiveRow> live = new List<LiveRow>();
        DateTime liveDay;

        // grafik miesięczny i roczny plan urlopów
        readonly DateBox schMonth, planYear;
        readonly MonthSchedule schedule;
        readonly YearPlanner planner;

        // odbicia
        readonly DateBox pFrom, pTo;
        readonly ComboBox pWho;
        readonly DataGridView pGrid;
        List<Punch> pShown = new List<Punch>();

        // nieobecności
        readonly DateBox aYear;
        readonly ComboBox aWho;
        readonly DataGridView aGrid;
        readonly Label aInfo;
        readonly Card aInfoCard;
        List<Absence> aShown = new List<Absence>();

        // podsumowanie
        readonly DateBox sMonth;
        readonly ComboBox sWho;
        readonly CheckBox sProblems;
        readonly DataGridView sGrid, sSumGrid;
        List<DayInfo> sShown = new List<DayInfo>();

        // statystyki
        readonly ComboBox stPeriod;
        readonly DataGridView stGrid;
        readonly BarChart chart;
        readonly Label chartTitle;
        List<Summary> stShown = new List<Summary>();
        bool fillingStats;

        protected override Color CaptionColor { get { return Theme.Sidebar; } }

        public PanelForm(Action openEmployees)
        {
            Text = "Ewidencja czasu pracy";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1240, 760);
            MinimumSize = new Size(980, 600);
            Theme.DoubleBuffer(this);

            // ---------------- menu boczne
            var sidebar = new SidebarPanel { Dock = DockStyle.Left, Width = 232 };
            var bottomNav = new Panel { Dock = DockStyle.Bottom, Height = 4 * 42 + 16, Tag = "sidebar", Padding = new Padding(0, 0, 1, 12) };
            themeNav = new NavButton("", "");
            var lockNav = new NavButton("", "Zablokuj panel");
            var setNav = new NavButton("", "Ustawienia");
            var empNav = new NavButton("", "Pracownicy i karty");
            themeNav.Click += (s, e) => ToggleTheme();
            lockNav.Click += (s, e) => { Pin.Lock(); Close(); };
            setNav.Click += (s, e) => OpenSettings();
            empNav.Click += (s, e) => openEmployees();
            bottomNav.Controls.Add(lockNav);
            bottomNav.Controls.Add(themeNav);
            bottomNav.Controls.Add(setNav);
            bottomNav.Controls.Add(empNav);
            var navHost = new Panel { Dock = DockStyle.Fill, Tag = "sidebar", Padding = new Padding(0, 4, 1, 0) };
            sidebar.Controls.Add(navHost);
            sidebar.Controls.Add(bottomNav);
            sidebar.Controls.Add(new LogoBlock { Dock = DockStyle.Top });

            // ---------------- obszar główny
            var main = new Panel { Dock = DockStyle.Fill, Tag = "bg", Padding = new Padding(28, 18, 28, 22) };
            var header = new Panel { Dock = DockStyle.Top, Height = 78 };
            title = new Label { Font = Theme.Semi(20), AutoSize = true, Location = new Point(0, 0) };
            subtitle = new Label { Tag = "muted", AutoSize = true, Location = new Point(2, 42), Font = Theme.UI(10.5f) };
            actions = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 8, 0, 0) };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(actions);
            var host = new Panel { Dock = DockStyle.Fill };
            main.Controls.Add(host);
            main.Controls.Add(header);

            // ---------------- obecność dziś
            kIn = new StatCard { Glyph = "", Caption = "W biurze", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
            kAbs = new StatCard { Glyph = "", Caption = "Urlopy, L4, zdalnie", Dock = DockStyle.Fill, Margin = new Padding(5, 0, 5, 0) };
            kLate = new StatCard { Glyph = "", Caption = "Spóźnienia dziś", Dock = DockStyle.Fill, Margin = new Padding(5, 0, 5, 0) };
            kIssues = new StatCard { Glyph = "", Caption = "Do uzupełnienia", Dock = DockStyle.Fill, Margin = new Padding(10, 0, 0, 0), Cursor = Cursors.Hand };
            kIssues.Click += (s, e) => ShowProblems();
            var kpi = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++) kpi.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            kpi.Controls.Add(kIn, 0, 0);
            kpi.Controls.Add(kAbs, 1, 0);
            kpi.Controls.Add(kLate, 2, 0);
            kpi.Controls.Add(kIssues, 3, 0);
            issuesBanner = new Banner { Dock = DockStyle.Top, Kind = "warning", Glyph = "", Cursor = Cursors.Hand };
            issuesBanner.Click += (s, e) => ShowProblems();
            issuesHost = BannerHost(issuesBanner);
            downBanner = new Banner { Dock = DockStyle.Top, Kind = "info", Glyph = "" };
            downHost = BannerHost(downBanner);
            todayGrid = MakeGrid("Pracownik", "Status", "Wejście", "Ostatnie odbicie", "Czas dziś", "Do normy", "Uwagi");
            Weights(todayGrid, 210, 150, 70, 110, 90, 125, 140);
            todayGrid.RowTemplate.Height = 46;
            todayGrid.CellPainting += PaintTodayCell;
            todayGrid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                ShowPunchesFor((string)todayGrid.Rows[e.RowIndex].Tag, DateTime.Today, DateTime.Today);
            };
            var todayPage = Stack(null, new Control[] { kpi, Gap(16), downHost, issuesHost }, CardOf(todayGrid), null);
            AddPage("Obecność dziś", "\uE80F", todayPage, new Control[0]);

            // ---------------- grafik miesięczny
            schMonth = new DateBox(DateBox.Mode.Month, DateTime.Today);
            schMonth.ValueChanged += (s, e) => RefreshSchedule();
            schedule = new MonthSchedule { Dock = DockStyle.Fill };
            schedule.CellOpen += FixCell;
            var schHint = Lbl("Najedź na kratkę, aby zobaczyć szczegóły; dwuklik – popraw lub uzupełnij.");
            AddPage("Grafik miesięczny", "\uE8C0", Stack(Toolbar(Lbl("Miesiąc"), schMonth, schHint), null, CardOf(schedule), null),
                new Control[] { Theme.Button("\uE749  Karta ewidencji (PDF)", "", (s, e) => PrintCardsFor(schMonth.Value)) });

            // ---------------- odbicia
            pFrom = new DateBox(DateBox.Mode.Day, new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
            pTo = new DateBox(DateBox.Mode.Day, DateTime.Today);
            pWho = Combo();
            pFrom.ValueChanged += (s, e) => RefreshPunches();
            pTo.ValueChanged += (s, e) => RefreshPunches();
            pWho.SelectedIndexChanged += (s, e) => RefreshPunches();
            pGrid = MakeGrid("Data", "Dzień", "Godzina", "Pracownik", "Rodzaj", "Źródło");
            pGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditPunch(); };
            pGrid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) DeletePunch(); };
            pGrid.CellPainting += PaintTypeCell;
            Weights(pGrid, 100, 60, 90, 180, 110, 120);
            AddPage("Odbicia i poprawki", "",
                Stack(Toolbar(Lbl("Od"), pFrom, Lbl("do"), pTo, Lbl("Pracownik"), pWho), null, CardOf(pGrid), null),
                new Control[] {
                    Theme.Button("  Dodaj odbicie", "primary", (s, e) => AddPunch()),
                    Theme.Button("  Popraw", "", (s, e) => EditPunch()),
                    Theme.Button("  Usuń", "danger", (s, e) => DeletePunch()),
                    Theme.Button("  Historia zmian", "ghost", (s, e) => OpenFile(Store.AuditFile)) });

            // ---------------- nieobecności
            aYear = new DateBox(DateBox.Mode.Year, DateTime.Today);
            aWho = Combo();
            aYear.ValueChanged += (s, e) => RefreshAbsences();
            aWho.SelectedIndexChanged += (s, e) => RefreshAbsences();
            aGrid = MakeGrid("Data", "Dzień", "Pracownik", "Rodzaj", "Uwagi");
            aGrid.MultiSelect = true;
            aGrid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) DeleteAbsences(); };
            aGrid.CellPainting += PaintAbsenceCell;
            Weights(aGrid, 100, 60, 170, 250, 200);
            aInfo = new Label { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 8, 4), Font = Theme.UI(10) };
            aInfoCard = new Card { Dock = DockStyle.Bottom, Height = 150 };
            aInfoCard.Controls.Add(aInfo);
            AddPage("Urlopy i nieobecności", "",
                Stack(Toolbar(Lbl("Rok"), aYear, Lbl("Pracownik"), aWho), null, CardOf(aGrid), new Control[] { Gap(14), aInfoCard }),
                new Control[] {
                    Theme.Button("\uE710  Dodaj nieobecność", "primary", (s, e) => AddAbsence(SelectedUid(aWho), DateTime.Today)),
                    Theme.Button("\uE74D  Usuń zaznaczone", "danger", (s, e) => DeleteAbsences()),
                    Theme.Button("\uE8BF  Dni wolne firmowe", "ghost", (s, e) => OpenCompanyDays()) });

            // ---------------- roczny plan urlopów
            planYear = new DateBox(DateBox.Mode.Year, DateTime.Today);
            planYear.ValueChanged += (s, e) => RefreshYear();
            planner = new YearPlanner { Dock = DockStyle.Fill };
            planner.DayOpen += d => AddAbsence(null, d);
            AddPage("Plan urlopów", "\uE8BF", Stack(Toolbar(Lbl("Rok"), planYear, Lbl("Kto i kiedy jest nieobecny – żeby urlopy się nie nakładały.")), null, CardOf(planner), null),
                new Control[] {
                    Theme.Button("\uE710  Dodaj nieobecność", "primary", (s, e) => AddAbsence(null, DateTime.Today)),
                    Theme.Button("\uE8BF  Dni wolne firmowe", "", (s, e) => OpenCompanyDays()) });

            // ---------------- podsumowanie miesiąca
            var start = DateTime.Today.Day <= 7 ? DateTime.Today.AddMonths(-1) : DateTime.Today;
            sMonth = new DateBox(DateBox.Mode.Month, start);
            sWho = Combo();
            sProblems = new CheckBox { Text = "tylko problemy", AutoSize = true, Margin = new Padding(14, 9, 4, 0) };
            sMonth.ValueChanged += (s, e) => RefreshSummary();
            sWho.SelectedIndexChanged += (s, e) => RefreshSummary();
            sProblems.CheckedChanged += (s, e) => RefreshSummary();
            sGrid = MakeGrid("Pracownik", "Data", "Dzień", "Wejście", "Wyjście", "Czas", "Suma dnia", "Nadgodziny", "Uwagi");
            Weights(sGrid, 150, 100, 55, 70, 70, 60, 75, 90, 230);
            sGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && e.RowIndex < sShown.Count) FixDay(sShown[e.RowIndex]); };
            sSumGrid = MakeGrid("Pracownik", "Wymiar", "Przepracowano", "Nieob. uspr.", "Saldo (do dziś)", "Nadgodziny", "Praca w dni wolne", "Spóźnienia", "Braki", "Urlop pozostały");
            Weights(sSumGrid, 200, 90, 105, 85, 110, 95, 90, 100, 70, 90);
            sSumGrid.CellPainting += PaintSumCell;
            var sumCard = CardOf(sSumGrid);
            sumCard.Dock = DockStyle.Bottom;
            sumCard.Height = 290;
            AddPage("Podsumowanie miesiąca", "",
                Stack(Toolbar(Lbl("Miesiąc"), sMonth, Lbl("Pracownik"), sWho, sProblems), null, CardOf(sGrid), new Control[] { Gap(14), sumCard }),
                new Control[] {
                    Theme.Button("  Karta ewidencji (PDF)", "primary", (s, e) => PrintCards()),
                    Theme.Button("  Eksport do Excela", "", (s, e) => ExportExcel()) });

            // ---------------- statystyki
            stPeriod = new ThemedCombo { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
            stPeriod.Items.AddRange(new object[] { "Bieżący miesiąc", "Poprzedni miesiąc", "Ostatnie 3 miesiące", "Ostatnie 6 miesięcy", "Ostatnie 12 miesięcy", "Bieżący rok" });
            stPeriod.SelectedIndex = 2;
            stPeriod.SelectedIndexChanged += (s, e) => RefreshStats();
            stGrid = MakeGrid("Pracownik", "Dni", "Przyjście", "Wyjście", "Dziennie", "Spóźn.", "Min spóźn.", "Nadgodz.", "Saldo", "Urlop wyk./poz.", "L4", "Zdalnie", "Absencja", "Bradford");
            Weights(stGrid, 175, 48, 78, 74, 78, 62, 82, 86, 98, 104, 44, 68, 86, 92);
            stGrid.SelectionChanged += (s, e) => { if (!fillingStats) UpdateChart(); };
            stGrid.CellPainting += PaintStatCell;
            chartTitle = new Label { Dock = DockStyle.Top, Height = 30, Padding = new Padding(8, 8, 0, 0), Font = Theme.Semi(10.5f) };
            chart = new BarChart { Dock = DockStyle.Fill };
            var chartCard = new Card { Dock = DockStyle.Bottom, Height = 320, Padding = new Padding(12, 8, 12, 10) };
            chartCard.Controls.Add(chart);
            chartCard.Controls.Add(chartTitle);
            var hint = Lbl("Absencja i Bradford: L4, na żądanie, opieka, nieusprawiedliwione (najedź na wynik).");
            hint.Tag = "muted";
            AddPage("Statystyki", "",
                Stack(Toolbar(Lbl("Okres"), stPeriod, hint), null, CardOf(stGrid), new Control[] { Gap(14), chartCard }),
                new Control[0]);

            foreach (var p in pages) host.Controls.Add(p.Content);
            for (int i = pages.Count - 1; i >= 0; i--) navHost.Controls.Add(pages[i].Nav);

            Controls.Add(main);
            Controls.Add(sidebar);

            UpdateThemeNav();
            SelectPage(TabToday);

            refreshTimer = new WinTimer { Interval = 30000 };
            refreshTimer.Tick += (s, e) => RefreshToday();
            refreshTimer.Start();
            tickTimer = new WinTimer { Interval = 1000 };
            tickTimer.Tick += (s, e) => TickLive();
            tickTimer.Start();

            // panel zamyka się sam po 10 minutach nieużywania (PIN trzeba będzie podać ponownie)
            lockTimer = new WinTimer { Interval = 10 * 60 * 1000 };
            lockTimer.Tick += (s, e) =>
            {
                if (Form.ActiveForm != null) return; // użytkownik pracuje w innym oknie programu
                lockTimer.Stop();
                Pin.Lock();
                Close();
            };
            Deactivate += (s, e) => { lockTimer.Stop(); lockTimer.Start(); };
            Activated += (s, e) => { lockTimer.Stop(); Pin.Extend(); };
            Shown += (s, e) => ClearSelections();

            RefreshAll();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            refreshTimer.Dispose();
            tickTimer.Dispose();
            lockTimer.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnThemeChanged()
        {
            UpdateThemeNav();
            RefreshAll();
        }

        // ---------------------------------------------------------- układ

        void AddPage(string pageTitle, string glyph, Panel content, Control[] pageActions)
        {
            var p = new Page { Title = pageTitle, Glyph = glyph, Content = content, Actions = pageActions };
            p.Nav = new NavButton(glyph, pageTitle);
            int idx = pages.Count;
            p.Nav.Click += (s, e) => SelectPage(idx);
            content.Visible = false;
            pages.Add(p);
        }

        public void SelectTab(int i) { SelectPage(i); }

        void SelectPage(int i)
        {
            if (current == i) return;
            current = i;
            for (int k = 0; k < pages.Count; k++)
            {
                pages[k].Content.Visible = k == i;
                pages[k].Nav.Selected = k == i;
                pages[k].Nav.Invalidate();
            }
            title.Text = pages[i].Title;
            UpdateSubtitle();
            actions.SuspendLayout();
            actions.Controls.Clear();
            actions.Controls.AddRange(pages[i].Actions);
            Theme.Apply(actions);
            actions.ResumeLayout();
            ClearSelections();
            TickLive();
            if (i == TabPunches) BeginInvokeSafe(ScrollPunchesToEnd);
        }

        void BeginInvokeSafe(Action a)
        {
            if (IsHandleCreated) BeginInvoke(a);
        }

        void UpdateSubtitle()
        {
            if (current < 0) return;
            var now = DateTime.Now;
            switch (current)
            {
                case TabToday:
                    var h = PlCalendar.Holiday(now);
                    subtitle.Text = Cap(now.ToString("dddd, d MMMM yyyy", Pl)) + (h != null ? "  •  " + h : "") + "  •  stan na " + now.ToString("HH:mm");
                    break;
                case TabSchedule: subtitle.Text = "Cały zespół i cały miesiąc na jednym ekranie."; break;
                case TabYear: subtitle.Text = "Urlopy i nieobecności w całym roku."; break;
                case TabPunches: subtitle.Text = "Wejścia i wyjścia – dwuklik poprawia odbicie."; break;
                case TabAbsences: subtitle.Text = "Urlopy, L4, praca zdalna i pozostały urlop."; break;
                case TabSummary: subtitle.Text = "Dzień po dniu – dwuklik w wiersz, aby poprawić."; break;
                default: subtitle.Text = "Średnie, spóźnienia, nadgodziny i godziny w miesiącach."; break;
            }
        }

        static string Cap(string s) { return s.Length == 0 ? s : char.ToUpper(s[0]) + s.Substring(1); }

        // Strona: pasek narzędzi u góry, elementy górne, główna karta (wypełnia), elementy dolne.
        static Panel Stack(Control toolbar, Control[] tops, Control fill, Control[] bottoms)
        {
            var page = new Panel { Dock = DockStyle.Fill };
            page.Controls.Add(fill);
            if (bottoms != null) for (int i = bottoms.Length - 1; i >= 0; i--) { bottoms[i].Dock = DockStyle.Bottom; page.Controls.Add(bottoms[i]); }
            if (tops != null) for (int i = tops.Length - 1; i >= 0; i--) { tops[i].Dock = DockStyle.Top; page.Controls.Add(tops[i]); }
            if (toolbar != null) page.Controls.Add(toolbar);
            return page;
        }

        static Panel Gap(int h) { return new Panel { Height = h, Dock = DockStyle.Top }; }

        static Panel BannerHost(Banner b)
        {
            var host = new Panel { Height = 52, Padding = new Padding(0, 0, 0, 12), Visible = false };
            host.Controls.Add(b);
            return host;
        }

        static Card CardOf(Control inner)
        {
            var c = new Card { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 8) };
            inner.Dock = DockStyle.Fill;
            c.Controls.Add(inner);
            return c;
        }

        static FlowLayoutPanel Toolbar(params Control[] items)
        {
            var f = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(0, 2, 0, 10), WrapContents = false };
            foreach (var c in items) c.Margin = c is Label ? new Padding(c == items[0] ? 0 : 14, 9, 6, 0) : new Padding(0, 4, 4, 0);
            f.Controls.AddRange(items);
            return f;
        }

        static DataGridView MakeGrid(params string[] cols)
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false, RowHeadersVisible = false, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BorderStyle = BorderStyle.None
            };
            g.RowTemplate.Height = 36;
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            foreach (var c in cols) g.Columns.Add(c, c);
            foreach (DataGridViewColumn c in g.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            Theme.StyleGrid(g);
            return g;
        }

        static void Weights(DataGridView g, params float[] w)
        {
            for (int i = 0; i < w.Length && i < g.Columns.Count; i++) g.Columns[i].FillWeight = w[i];
        }

        static ComboBox Combo() { return new ThemedCombo { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 }; }

        static Label Lbl(string t) { return new Label { Text = t, AutoSize = true, Tag = "muted" }; }

        static void OpenFile(string path)
        {
            if (!File.Exists(path)) { MessageBox.Show("Nie ma jeszcze żadnych zmian.", "Ewidencja Czasu"); return; }
            Process.Start(path);
        }

        static void FillWhoCombo(ComboBox box, List<Employee> emps)
        {
            string prev = SelectedUid(box);
            box.Items.Clear();
            box.Items.Add(new ComboItem { Text = "(wszyscy)", Uid = null });
            foreach (var e in emps.OrderBy(x => !x.Active).ThenBy(x => x.Name))
                box.Items.Add(new ComboItem { Text = e.Active ? e.Name : e.Name + " (nieaktywny)", Uid = e.Uid });
            SelectUid(box, prev);
        }

        static void SelectUid(ComboBox box, string uid)
        {
            int idx = 0;
            for (int i = 1; i < box.Items.Count; i++)
                if (uid != null && Store.Norm(((ComboItem)box.Items[i]).Uid) == Store.Norm(uid)) idx = i;
            box.SelectedIndex = box.Items.Count > 0 ? idx : -1;
        }

        static string SelectedUid(ComboBox box)
        {
            var item = box.SelectedItem as ComboItem;
            return item == null ? null : item.Uid;
        }

        static string Hms(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            return string.Format("{0}:{1:00}:{2:00}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
        }

        static string ToNorm(TimeSpan total, TimeSpan norm)
        {
            var rest = norm - total;
            if (rest > TimeSpan.Zero) return "zostało " + Hms(rest);
            if (rest > TimeSpan.FromMinutes(-1)) return "norma wykonana";
            return "+" + Hms(-rest) + " nadg.";
        }

        void ClearSelections()
        {
            todayGrid.ClearSelection();
            sGrid.ClearSelection();
            sSumGrid.ClearSelection();
        }

        // ---------------------------------------------------------- motyw

        void UpdateThemeNav()
        {
            themeNav.Glyph = Theme.Dark ? "" : "";
            themeNav.Text = Theme.Dark ? "Motyw jasny" : "Motyw ciemny";
            themeNav.Invalidate();
        }

        void ToggleTheme()
        {
            Cfg.ThemeMode = Theme.Dark ? "jasny" : "ciemny";
            try { Cfg.Save(Store.CfgFile); } catch { }
            Theme.Load();
            Theme.RaiseChanged();
        }

        // ---------------------------------------------------------- rysowanie komórek

        // Tło komórki rysowane przez tabelę (siatkę dorysowuje Theme.GridLines).
        static void PaintCellBase(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground);
        }

        static readonly string[] AvatarKinds = { "accent", "success", "violet", "warning", "info", "danger" };

        void PaintTodayCell(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || (e.ColumnIndex != 0 && e.ColumnIndex != 1)) return;
            PaintCellBase(e);
            var g = e.Graphics;
            string text = Convert.ToString(e.FormattedValue);
            if (e.ColumnIndex == 0)
            {
                // kółko z inicjałami
                var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string ini = parts.Length == 0 ? "?" : (parts[0].Substring(0, 1) + (parts.Length > 1 ? parts[parts.Length - 1].Substring(0, 1) : "")).ToUpper();
                Color fg, bg;
                Theme.StatusColors(AvatarKinds[Math.Abs(text.GetHashCode()) % AvatarKinds.Length], out fg, out bg);
                var r = new RectangleF(e.CellBounds.X + 10, e.CellBounds.Y + (e.CellBounds.Height - 32) / 2f, 32, 32);
                var state = g.Save(); // wygładzanie tylko dla kółka – nie może wpływać na linie tabeli
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(bg)) g.FillEllipse(b, r);
                g.Restore(state);
                TextRenderer.DrawText(g, ini, Theme.Semi(9.5f), Rectangle.Round(r), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                string sub = todayGrid.Rows[e.RowIndex].Cells[0].Tag as string ?? "";
                int mid = e.CellBounds.Y + e.CellBounds.Height / 2;
                TextRenderer.DrawText(g, text, Theme.Semi(10.5f), new Rectangle(e.CellBounds.X + 52, mid - 20, e.CellBounds.Width - 56, 21),
                    Theme.Text, TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, sub, Theme.UI(8.5f), new Rectangle(e.CellBounds.X + 52, mid + 1, e.CellBounds.Width - 56, 18),
                    Theme.Muted, TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            else Theme.Pill(g, text, todayGrid.Rows[e.RowIndex].Cells[1].Tag as string ?? "", e.CellBounds, Theme.Semi(9.5f));
            e.Handled = true;
        }

        void PaintTypeCell(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 4) return;
            PaintCellBase(e);
            string text = Convert.ToString(e.FormattedValue);
            Theme.Pill(e.Graphics, text.ToLower(), text == Store.IN ? "success" : "info", e.CellBounds, Theme.Semi(9));
            e.Handled = true;
        }

        void PaintAbsenceCell(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 3 || e.RowIndex >= aShown.Count) return;
            PaintCellBase(e);
            var t = AbsTypes.Find(aShown[e.RowIndex].Code);
            string kind = t.Kind == 0 ? "info" : t.Kind == 3 ? "danger" : t.Code == "CH" ? "warning" : t.Kind == 2 ? "neutral" : "violet";
            Theme.Pill(e.Graphics, t.ToString(), kind, e.CellBounds, Theme.Semi(9));
            e.Handled = true;
        }

        void PaintSumCell(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || (e.ColumnIndex != 4 && e.ColumnIndex != 8)) return;
            string text = Convert.ToString(e.FormattedValue);
            if (text == "—") return;
            bool bad = e.ColumnIndex == 4 ? text.StartsWith("-") : text != "0";
            if (!bad && e.ColumnIndex == 8) return;
            PaintCellBase(e);
            Theme.Pill(e.Graphics, text, bad ? "danger" : text.StartsWith("+") ? "success" : "neutral", e.CellBounds, Theme.Semi(9));
            e.Handled = true;
        }

        void PaintStatCell(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || (e.ColumnIndex != 8 && e.ColumnIndex != 13)) return;
            string text = Convert.ToString(e.FormattedValue);
            if (text == "—") return;
            if (e.ColumnIndex == 13)
            {
                // współczynnik Bradforda: zielony / żółty / pomarańczowy / czerwony
                int b;
                int.TryParse(text, out b);
                PaintCellBase(e);
                Theme.Pill(e.Graphics, text, b < 50 ? "success" : b < 200 ? "warning" : "danger", e.CellBounds, Theme.Semi(9));
                e.Handled = true;
                return;
            }
            PaintCellBase(e);
            Theme.Pill(e.Graphics, text, text.StartsWith("-") ? "danger" : text.StartsWith("+") ? "success" : "neutral", e.CellBounds, Theme.Semi(9));
            e.Handled = true;
        }

        // ---------------------------------------------------------- odświeżanie

        public void RefreshAll()
        {
            loading = true;
            try
            {
                var emps = Store.LoadEmployees();
                FillWhoCombo(pWho, emps);
                FillWhoCombo(aWho, emps);
                FillWhoCombo(sWho, emps);
            }
            finally { loading = false; }
            RefreshToday();
            RefreshSchedule();
            RefreshPunches();
            RefreshAbsences();
            RefreshYear();
            RefreshSummary();
            RefreshStats();
        }

        void RefreshSchedule()
        {
            if (loading) return;
            try { schedule.SetData(Data.Load(), schMonth.Value); }
            catch (Exception ex) { MessageBox.Show("Błąd grafiku:\n" + ex.Message, Text); }
        }

        void RefreshYear()
        {
            if (loading) return;
            try { planner.SetData(Data.Load(), planYear.Value.Year); }
            catch (Exception ex) { MessageBox.Show("Błąd planu urlopów:\n" + ex.Message, Text); }
        }

        // dwuklik w kratkę grafiku
        void FixCell(string uid, DateTime day, DayInfo d)
        {
            if (d != null && (d.Rows.Count > 0 || d.Abs != null || d.NoShow)) { FixDay(d); return; }
            var data = Data.Load();
            if (!data.HasNorm(uid)) { ShowPunchesFor(uid, day, day); return; }
            var r = MessageBox.Show(string.Format("{0} – {1:yyyy-MM-dd}\n\nTAK – wpisz nieobecność (urlop, L4, praca zdalna…)\nNIE – dodaj odbicia ręcznie",
                data.NameOf(uid), day), Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) AddAbsence(uid, day);
            else if (r == DialogResult.No) ShowPunchesFor(uid, day, day);
        }

        void OpenCompanyDays()
        {
            using (var f = new CompanyDaysForm(current == TabYear ? planYear.Value.Year : DateTime.Today.Year))
            {
                f.ShowDialog(this);
                if (f.Changed) RefreshAll();
            }
        }

        void PrintCardsFor(DateTime month)
        {
            try { Process.Start(Reports.TimeCards(month.Year, month.Month, null)); }
            catch (Exception ex) { MessageBox.Show("Nie udało się utworzyć karty ewidencji:\n" + ex.Message, Text); }
        }

        void RefreshToday()
        {
            try
            {
                var now = DateTime.Now;
                var today = now.Date;
                var data = Data.Load();
                int inside = 0, active = 0, away = 0, late = 0;
                todayGrid.Rows.Clear();
                live.Clear();
                todayGrid.Columns[5].HeaderText = "Do normy " + Cfg.NormHours.ToString("0.#", Pl) + " h";
                foreach (var e in data.Emps.OrderBy(x => x.Name))
                {
                    var di = Calc.Day(data, e.Uid, today);
                    bool hasPunch = di.Rows.Count > 0;
                    if (!e.Active && !hasPunch) continue;
                    active++;
                    string status, kind, last = "", first = "", worked = "", toNorm = "";
                    var punches = data.DayPunches(e.Uid, today);
                    if (hasPunch)
                    {
                        var lp = punches[punches.Count - 1];
                        last = lp.Time.ToString("HH:mm") + "  " + lp.Type.ToLower();
                        if (di.OpenSince.HasValue) { status = "W biurze"; kind = "success"; inside++; }
                        else { status = "Wyszedł / wyszła"; kind = "info"; }
                    }
                    else if (di.Abs != null) { status = di.Abs.Name; kind = di.Abs.Kind == 0 ? "accent" : di.Abs.Kind == 3 ? "danger" : "violet"; away++; }
                    else if (!di.WorkingDay) { status = "Dzień wolny"; kind = "neutral"; }
                    else if (!di.HasNorm) { status = "Nie pracuje dziś"; kind = "neutral"; }
                    else { status = "Brak odbicia"; kind = "neutral"; }
                    if (di.FirstIn.HasValue) first = di.FirstIn.Value.ToString("HH:mm");
                    if (di.LateMin > 0) late++;

                    var total = di.Total + (di.OpenSince.HasValue ? now - di.OpenSince.Value : TimeSpan.Zero);
                    if (hasPunch || di.Remote > TimeSpan.Zero) worked = Hms(total);
                    if (!di.HasNorm) toNorm = hasPunch ? "bez normy" : "";
                    else if (di.WorkingDay && (hasPunch || di.Remote > TimeSpan.Zero)) toNorm = ToNorm(total, di.DayNorm);
                    else if (!di.WorkingDay && hasPunch) toNorm = "praca w dzień wolny";

                    var notes = new List<string>();
                    if (di.LateMin > 0) notes.Add("spóźnienie " + di.LateMin + " min");
                    if (hasPunch && di.Abs != null) notes.Add(di.Abs.Name);
                    var prev = data.Punches.LastOrDefault(p => Store.Norm(p.Uid) == Store.Norm(e.Uid) && p.Time.Date < today);
                    if (prev != null && prev.Type == Store.IN) notes.Add("brak wyjścia " + prev.Time.ToString("dd.MM"));

                    int i = todayGrid.Rows.Add(e.Name, status, first, last, worked, toNorm, string.Join(", ", notes));
                    var row = todayGrid.Rows[i];
                    row.Tag = e.Uid;
                    row.Cells[0].Tag = Contracts.Short(e.Contract) + (e.HasNorm && e.Etat < 1 ? ", " + Contracts.EtatName(e.Etat) : "");
                    row.Cells[1].Tag = kind;
                    if (di.OpenSince.HasValue)
                    {
                        row.Cells[4].Style.Font = Theme.Semi(10.5f);
                        live.Add(new LiveRow { Row = row, Done = di.Total, OpenSince = di.OpenSince.Value, WorkingDay = di.WorkingDay, HasNorm = di.HasNorm, Norm = di.DayNorm });
                    }
                    row.Cells[5].Style.ForeColor = toNorm.StartsWith("+") ? Theme.Success : Theme.Muted;
                    if (notes.Count > 0) row.Cells[6].Style.ForeColor = notes.Any(n => n.StartsWith("brak")) ? Theme.Danger : Theme.Warning;
                }
                liveDay = today;

                var downs = Store.Downtimes(today).Where(x => Store.DowntimeInWorkHours(x.Key, x.Value).TotalMinutes >= 2)
                    .Select(x => (x.Key.Date < today ? "od wczoraj" : x.Key.ToString("HH:mm")) + "–" + x.Value.ToString("HH:mm")).ToList();
                downHost.Visible = downs.Count > 0;
                downBanner.Text = "Dziś program nie działał: " + string.Join(", ", downs) + " – odbicia z tego czasu mogą brakować.";

                var issues = Calc.Range(data, today.AddDays(-31), today, null).Where(d => d.Problem).ToList();
                issuesHost.Visible = issues.Count > 0;
                if (issues.Count > 0)
                {
                    issuesMonth = issues.Min(d => d.Day);
                    issuesBanner.Text = string.Format("Do uzupełnienia z ostatniego miesiąca: {0} (braki odbić / dni bez obecności) – kliknij, aby poprawić.", issues.Count);
                }

                kIn.Set(inside.ToString(), "z " + active, inside > 0 ? "success" : "neutral");
                kAbs.Set(away.ToString(), away == 1 ? "osoba" : "osób", away > 0 ? "violet" : "neutral");
                kLate.Set(late.ToString(), "", late > 0 ? "warning" : "neutral");
                kIssues.Set(issues.Count.ToString(), issues.Count > 0 ? "kliknij, aby poprawić" : "wszystko w porządku", issues.Count > 0 ? "danger" : "success");
                UpdateSubtitle();
                todayGrid.ClearSelection();
            }
            catch (Exception ex) { subtitle.Text = "Błąd odczytu: " + ex.Message; }
        }

        // Licznik na żywo: przepracowany czas = zamknięte odcinki + (teraz – ostatnie wejście).
        // Nic nie jest "doliczane" co sekundę – wynik zawsze wynika z godzin odbić.
        void TickLive()
        {
            if (WindowState == FormWindowState.Minimized || current != TabToday) return;
            if (DateTime.Now.Date != liveDay) { RefreshToday(); return; }
            var now = DateTime.Now;
            foreach (var l in live)
            {
                var total = l.Done + (now - l.OpenSince);
                l.Row.Cells[4].Value = Hms(total);
                var tn = !l.HasNorm ? "bez normy" : l.WorkingDay ? ToNorm(total, l.Norm) : "praca w dzień wolny";
                l.Row.Cells[5].Value = tn;
                l.Row.Cells[5].Style.ForeColor = tn.StartsWith("+") ? Theme.Success : Theme.Muted;
            }
        }

        void RefreshPunches()
        {
            if (loading) return;
            try
            {
                var from = pFrom.Value.Date;
                var to = pTo.Value.Date.AddDays(1);
                string uid = SelectedUid(pWho);
                var emps = Store.LoadEmployees();
                pShown = Store.LoadPunches().Where(p => p.Time >= from && p.Time < to &&
                    (uid == null || Store.Norm(p.Uid) == Store.Norm(uid))).ToList();
                pGrid.Rows.Clear();
                foreach (var p in pShown)
                {
                    var e = Store.FindEmployee(emps, p.Uid);
                    int i = pGrid.Rows.Add(p.Time.ToString("yyyy-MM-dd"), p.Time.ToString("ddd", Pl), p.Time.ToString("HH:mm:ss"),
                        e != null ? e.Name : p.Name, p.Type, p.Source == "KARTA" ? "karta" : p.Source.ToLower());
                    if (p.Source != "KARTA") pGrid.Rows[i].Cells[5].Style.ForeColor = Theme.Warning;
                }
                ScrollPunchesToEnd();
            }
            catch (Exception ex) { MessageBox.Show("Błąd odczytu odbić:\n" + ex.Message, Text); }
        }

        // Przewinięcie do najnowszych odbić. Tylko gdy tabela ma już wysokość –
        // inaczej DataGridView potrafi się zawiesić (błąd Windows Forms).
        void ScrollPunchesToEnd()
        {
            if (pGrid.Rows.Count == 0 || !pGrid.IsHandleCreated || !pGrid.Visible ||
                pGrid.DisplayRectangle.Height < pGrid.ColumnHeadersHeight + pGrid.RowTemplate.Height) return;
            try { pGrid.FirstDisplayedScrollingRowIndex = pGrid.Rows.Count - 1; }
            catch (InvalidOperationException) { }
        }

        void RefreshAbsences()
        {
            if (loading) return;
            try
            {
                int year = aYear.Value.Year;
                string uid = SelectedUid(aWho);
                var data = Data.Load();
                aShown = data.Absences.Where(a => a.Day.Year == year && (uid == null || Store.Norm(a.Uid) == Store.Norm(uid))).ToList();
                aGrid.Rows.Clear();
                foreach (var a in aShown)
                    aGrid.Rows.Add(a.Day.ToString("yyyy-MM-dd"), a.Day.ToString("ddd", Pl), data.NameOf(a.Uid), AbsTypes.Find(a.Code).ToString(), a.Note);
                aGrid.ClearSelection();

                var lines = new List<string>();
                foreach (var e in data.Emps.Where(x => x.Active && x.HasNorm && (uid == null || Store.Norm(x.Uid) == Store.Norm(uid))).OrderBy(x => x.Name))
                {
                    var li = Calc.Leave(data, e.Uid, year);
                    lines.Add(string.Format("{0}:   pozostało {1} z {2} dni   (wykorzystano {3}, na żądanie {4}/4)   •   L4: {5} dni",
                        e.Name, li.Remaining, li.Entitled + li.Carry, li.Used, li.UZ, li.Sick));
                }
                aInfo.Text = lines.Count == 0 ? "" : "Urlop w " + year + " r.\n" + string.Join("\n", lines);
                aInfoCard.Height = 30 + (lines.Count + 1) * (aInfo.Font.Height + 3);
            }
            catch (Exception ex) { aInfo.Text = "Błąd odczytu: " + ex.Message; }
        }

        void RefreshSummary()
        {
            if (loading) return;
            try
            {
                var from = new DateTime(sMonth.Value.Year, sMonth.Value.Month, 1);
                var to = from.AddMonths(1);
                string uid = SelectedUid(sWho);
                var data = Data.Load();
                var days = Calc.Range(data, from, to, uid);
                var shownDays = sProblems.Checked ? days.Where(d => d.Problem || d.LateMin > 0 || d.EarlyMin > 0).ToList() : days;

                sShown = new List<DayInfo>();
                sGrid.Rows.Clear();
                foreach (var d in shownDays)
                {
                    var rows = d.Rows.Count > 0 ? d.Rows : new List<WorkRow> { new WorkRow() };
                    for (int k = 0; k < rows.Count; k++)
                    {
                        var r = rows[k];
                        bool last = k == rows.Count - 1;
                        string notes = string.Join(", ", new[] { r.Note, last ? d.Notes() : "" }.Where(x => x != ""));
                        int i = sGrid.Rows.Add(d.Name, d.Day.ToString("yyyy-MM-dd"), d.Day.ToString("ddd", Pl),
                            r.In.HasValue ? r.In.Value.ToString("HH:mm") : "",
                            r.Out.HasValue ? r.Out.Value.ToString("HH:mm") : "",
                            r.In.HasValue && r.Out.HasValue ? Calc.Hm(r.Duration) : "",
                            last && d.Total > TimeSpan.Zero ? Calc.Hm(d.Total) : "",
                            last && d.Overtime > TimeSpan.Zero ? Calc.Hm(d.Overtime) : "",
                            notes);
                        var style = sGrid.Rows[i].DefaultCellStyle;
                        if (d.Problem || r.Note.Contains("BRAK")) { style.BackColor = Theme.DangerSoft; style.SelectionBackColor = Theme.Blend(Theme.Danger, Theme.Surface, 0.3); }
                        else if (d.Abs != null) style.BackColor = Theme.VioletSoft;
                        else if (!d.WorkingDay) style.BackColor = Theme.SurfaceAlt;
                        if (d.LateMin > 0 || d.EarlyMin > 0) sGrid.Rows[i].Cells[8].Style.ForeColor = Theme.Warning;
                        if (d.Overtime > TimeSpan.Zero) sGrid.Rows[i].Cells[7].Style.ForeColor = Theme.Success;
                        sShown.Add(d);
                    }
                }
                sGrid.ClearSelection();

                sSumGrid.Columns[1].HeaderText = "Wymiar (" + Calc.Hm(PlCalendar.MonthNorm(from.Year, from.Month)) + ")";
                sSumGrid.Rows.Clear();
                foreach (var s in Calc.SummarizeAll(days, from, to, data).Where(s => uid == null || Store.Norm(s.Uid) == Store.Norm(uid)))
                {
                    var li = Calc.Leave(data, s.Uid, from.Year);
                    if (s.HasNorm)
                        sSumGrid.Rows.Add(s.Name, Calc.Hm(s.Norm), Calc.Hm(s.Total), Calc.Hm(s.Excused), Calc.Signed(s.Balance),
                            Calc.Hm(s.Overtime), Calc.Hm(s.FreeDayWork), s.Late > 0 ? s.Late + " (" + s.LateMinutes + " min)" : "0",
                            (s.Missing + s.NoShow).ToString(), li.Remaining + " dni");
                    else // umowa cywilnoprawna – liczy się tylko przepracowany czas
                        sSumGrid.Rows.Add(s.Name + " (" + Contracts.Short(s.Contract) + ")", "—", Calc.Hm(s.Total), "—", "—", "—", "—", "—",
                            s.Missing.ToString(), "—");
                }
                sSumGrid.ClearSelection();
            }
            catch (Exception ex) { MessageBox.Show("Błąd podsumowania:\n" + ex.Message, Text); }
        }

        void Period(out DateTime from, out DateTime to)
        {
            var m = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            switch (stPeriod.SelectedIndex)
            {
                case 0: from = m; to = m.AddMonths(1); break;
                case 1: from = m.AddMonths(-1); to = m; break;
                case 2: from = m.AddMonths(-2); to = m.AddMonths(1); break;
                case 3: from = m.AddMonths(-5); to = m.AddMonths(1); break;
                case 4: from = m.AddMonths(-11); to = m.AddMonths(1); break;
                default: from = new DateTime(m.Year, 1, 1); to = from.AddYears(1); break;
            }
        }

        void RefreshStats()
        {
            if (loading) return;
            try
            {
                DateTime from, to;
                Period(out from, out to);
                var data = Data.Load();
                var days = Calc.Range(data, from, to, null);
                stShown = Calc.SummarizeAll(days, from, to, data);
                int year = to.AddDays(-1).Year;
                fillingStats = true;
                stGrid.Rows.Clear();
                foreach (var s in stShown)
                {
                    var li = Calc.Leave(data, s.Uid, year);
                    var avg = s.Days > 0 ? TimeSpan.FromTicks(s.Worked.Ticks / s.Days) : TimeSpan.Zero;
                    int spells, bdays, score;
                    Calc.Bradford(data, s.Uid, DateTime.Today.AddDays(1), out spells, out bdays, out score);
                    double rate = Calc.AbsenceRate(data, s.Uid, from, to);
                    int i = s.HasNorm
                        ? stGrid.Rows.Add(s.Name, s.Days, s.AvgArrive, s.AvgLeave, s.Days > 0 ? Calc.Hm(avg) : "",
                            s.Late, s.LateMinutes + " min", Calc.Hm(s.Overtime), Calc.Signed(s.Balance),
                            li.Used + " / " + li.Remaining, s.Abs("CH"), s.Abs("PZ"), rate.ToString("0.0", Pl) + " %", score.ToString())
                        : stGrid.Rows.Add(s.Name + " (" + Contracts.Short(s.Contract) + ")", s.Days, s.AvgArrive, s.AvgLeave, s.Days > 0 ? Calc.Hm(avg) : "",
                            "—", "—", "—", "—", "—", "—", "—", "—", "—");
                    if (s.HasNorm) stGrid.Rows[i].Cells[13].ToolTipText = string.Format("{0} nieobecności, łącznie {1} dni roboczych w ostatnich 12 miesiącach\n" +
                        "0–49: w normie   50–199: do obserwacji   200–499: niepokojący   500+: wysoki", spells, bdays);
                    stGrid.Rows[i].Tag = s.Uid;
                }
                if (stGrid.Rows.Count > 0) stGrid.Rows[0].Selected = true;
                fillingStats = false;
                UpdateChart();
            }
            catch (Exception ex) { MessageBox.Show("Błąd statystyk:\n" + ex.Message, Text); }
            finally { fillingStats = false; }
        }

        void UpdateChart()
        {
            if (stGrid.SelectedRows.Count == 0) { chart.SetData(new List<BarChart.Bar>()); chartTitle.Text = ""; return; }
            string uid = (string)stGrid.SelectedRows[0].Tag;
            DateTime from, to;
            Period(out from, out to);
            if ((to.Year - from.Year) * 12 + to.Month - from.Month < 6) from = to.AddMonths(-6);
            var data = Data.Load();
            var bars = new List<BarChart.Bar>();
            for (var m = from; m < to; m = m.AddMonths(1))
            {
                var days = Calc.Range(data, m, m.AddMonths(1), uid);
                var s = Calc.Summarize(uid, "", days, m, m.AddMonths(1), data);
                bars.Add(new BarChart.Bar { Label = m.ToString("MMM yy", Pl), Value = s.Total.TotalHours, Extra = s.Excused.TotalHours, Target = s.Norm.TotalHours });
            }
            chartTitle.Text = "Godziny w miesiącach – " + data.NameOf(uid);
            chart.SetData(bars);
        }

        // ---------------------------------------------------------- nawigacja

        void ShowPunchesFor(string uid, DateTime from, DateTime to)
        {
            loading = true;
            try
            {
                pFrom.Value = from;
                pTo.Value = to;
                SelectUid(pWho, uid);
            }
            finally { loading = false; }
            RefreshPunches();
            SelectPage(TabPunches);
        }

        void ShowProblems()
        {
            loading = true;
            try
            {
                if (issuesMonth != DateTime.MinValue) sMonth.Value = issuesMonth;
                sWho.SelectedIndex = 0;
                sProblems.Checked = true;
            }
            finally { loading = false; }
            RefreshSummary();
            SelectPage(TabSummary);
        }

        void FixDay(DayInfo d)
        {
            if (d.NoShow)
            {
                var r = MessageBox.Show(string.Format("{0} – {1:yyyy-MM-dd}: brak odbić i nieobecności.\n\nTAK – wpisz nieobecność (urlop, L4, praca zdalna…)\nNIE – dodaj odbicia ręcznie",
                    d.Name, d.Day), Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Yes) AddAbsence(d.Uid, d.Day);
                else if (r == DialogResult.No) ShowPunchesFor(d.Uid, d.Day, d.Day);
                return;
            }
            if (d.Absence != null && d.Absence.Company && d.Rows.Count == 0) { OpenCompanyDays(); return; }
            if (d.Abs != null && d.Rows.Count == 0)
            {
                loading = true;
                try { aYear.Value = d.Day; SelectUid(aWho, d.Uid); }
                finally { loading = false; }
                RefreshAbsences();
                SelectPage(TabAbsences);
                return;
            }
            ShowPunchesFor(d.Uid, d.Day, d.Day);
        }

        // ---------------------------------------------------------- zmiany

        Punch SelectedPunch()
        {
            if (pGrid.CurrentRow == null || pGrid.CurrentRow.Index >= pShown.Count)
            {
                MessageBox.Show("Najpierw zaznacz odbicie na liście.", Text);
                return null;
            }
            return pShown[pGrid.CurrentRow.Index];
        }

        void AddPunch()
        {
            using (var f = new PunchForm(null, SelectedUid(pWho), pTo.Value))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    bool saved = Store.AppendPunch(f.Result);
                    Store.Audit("DODANIE ODBICIA", "", Store.Describe(f.Result));
                    if (!saved) MessageBox.Show("Plik odbicia.csv jest otwarty w Excelu – odbicie zapisano tymczasowo.", Text);
                }
                catch (Exception ex) { MessageBox.Show("Nie udało się dodać:\n" + ex.Message, Text); }
            }
            RefreshAll();
        }

        void EditPunch()
        {
            var p = SelectedPunch();
            if (p == null) return;
            using (var f = new PunchForm(p, null, p.Time))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try { Store.ChangePunch(p, f.Result); }
                catch (Exception ex) { MessageBox.Show("Nie udało się zapisać:\n" + ex.Message, Text); }
            }
            RefreshAll();
        }

        void DeletePunch()
        {
            var p = SelectedPunch();
            if (p == null) return;
            if (MessageBox.Show(string.Format("Usunąć odbicie?\n\n{0}\n{1} {2:yyyy-MM-dd HH:mm}\n\nZmiana zostanie zapisana w historii zmian.", p.Name, p.Type, p.Time),
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { Store.ChangePunch(p, null); }
            catch (Exception ex) { MessageBox.Show("Nie udało się usunąć:\n" + ex.Message, Text); }
            RefreshAll();
        }

        void AddAbsence(string uid, DateTime day)
        {
            using (var f = new AbsenceForm(uid, day))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try { Store.AddAbsences(f.Result); }
                catch (Exception ex) { MessageBox.Show("Nie udało się zapisać:\n" + ex.Message, Text); }
            }
            RefreshAll();
        }

        void DeleteAbsences()
        {
            var sel = aGrid.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Index < aShown.Count).Select(r => aShown[r.Index]).ToList();
            if (sel.Count == 0) { MessageBox.Show("Najpierw zaznacz wpisy na liście (Ctrl/Shift = kilka).", Text); return; }
            if (MessageBox.Show("Usunąć zaznaczone nieobecności (" + sel.Count + ")?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { Store.RemoveAbsences(sel); }
            catch (Exception ex) { MessageBox.Show("Nie udało się usunąć:\n" + ex.Message, Text); }
            RefreshAll();
        }

        void ExportExcel()
        {
            try { Process.Start(Reports.MonthReport(sMonth.Value.Year, sMonth.Value.Month)); }
            catch (Exception ex) { MessageBox.Show("Nie udało się utworzyć raportu:\n" + ex.Message, Text); }
        }

        void PrintCards()
        {
            try { Process.Start(Reports.TimeCards(sMonth.Value.Year, sMonth.Value.Month, SelectedUid(sWho))); }
            catch (Exception ex) { MessageBox.Show("Nie udało się utworzyć karty ewidencji:\n" + ex.Message, Text); }
        }

        void OpenSettings()
        {
            using (var f = new SettingsForm())
                if (f.ShowDialog(this) == DialogResult.OK) RefreshAll();
        }
    }

    // Tło menu bocznego z linią oddzielającą.
    class SidebarPanel : Panel, IThemed
    {
        public SidebarPanel() { Theme.DoubleBuffer(this); ResizeRedraw = true; }
        public override Color BackColor { get { return Theme.Sidebar; } set { } }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, Width - 1, 0, Width - 1, Height);
        }
    }

    // Logo w menu bocznym.
    class LogoBlock : Control, IThemed
    {
        public LogoBlock() { Height = 84; Theme.DoubleBuffer(this); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Sidebar);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(22, 22, 40, 40);
            using (var br = new LinearGradientBrush(r, Theme.Accent, Theme.Blend(Theme.Violet, Theme.Accent, 0.45), 45f))
            using (var path = Theme.Round(r, 11)) g.FillPath(br, path);
            Theme.Glyph(g, "", 15, Theme.OnAccent, r);
            TextRenderer.DrawText(g, "Ewidencja", Theme.Semi(13), new Point(72, 22), Theme.Text, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "czasu pracy", Theme.UI(9.5f), new Point(73, 45), Theme.Muted, TextFormatFlags.NoPadding);
        }
    }

    // Wykres słupkowy: przepracowane + nieobecności usprawiedliwione na tle wymiaru.
    class BarChart : Control, IThemed
    {
        public class Bar { public string Label; public double Value, Extra, Target; }
        List<Bar> bars = new List<Bar>();

        public BarChart()
        {
            Theme.DoubleBuffer(this);
            ResizeRedraw = true;
        }

        public void SetData(List<Bar> data) { bars = data; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (bars.Count == 0) return;
            int left = 48, right = 12, top = 34, bottom = 28;
            var area = new Rectangle(left, top, Width - left - right, Height - top - bottom);
            if (area.Width < 50 || area.Height < 40) return;
            double max = Math.Max(10, bars.Max(b => Math.Max(b.Value + b.Extra, b.Target)) * 1.12);
            double step = max > 150 ? 40 : max > 60 ? 20 : 10;
            var font = Theme.UI(8.5f);

            // legenda
            int lx = area.Left;
            foreach (var item in new[] { new { C = Theme.Accent, T = "przepracowane" }, new { C = Theme.Violet, T = "urlopy / L4" }, new { C = Theme.Danger, T = "wymiar" } })
            {
                Theme.FillRound(g, item.C, new RectangleF(lx, 8, 12, 12), 3);
                TextRenderer.DrawText(g, item.T, font, new Point(lx + 17, 7), Theme.Muted, TextFormatFlags.NoPadding);
                lx += 30 + TextRenderer.MeasureText(item.T, font).Width;
            }

            using (var grid = new Pen(Theme.Border) { DashStyle = DashStyle.Dot })
            {
                for (double v = 0; v <= max; v += step)
                {
                    int y = area.Bottom - (int)(v / max * area.Height);
                    g.DrawLine(grid, area.Left, y, area.Right, y);
                    TextRenderer.DrawText(g, v.ToString("0") + " h", font, new Rectangle(0, y - 8, left - 8, 16), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.NoPadding);
                }
            }
            float slot = (float)area.Width / bars.Count;
            float bw = Math.Min(44, slot * 0.55f);
            using (var target = new Pen(Theme.Danger, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                for (int i = 0; i < bars.Count; i++)
                {
                    var b = bars[i];
                    float x = area.Left + i * slot + (slot - bw) / 2;
                    float hw = (float)(b.Value / max * area.Height);
                    float he = (float)(b.Extra / max * area.Height);
                    if (hw + he > 0)
                    {
                        // zaokrąglona góra słupka
                        var full = new RectangleF(x, area.Bottom - hw - he, bw, hw + he + 6);
                        g.SetClip(new RectangleF(x - 1, area.Top - 40, bw + 2, area.Bottom - area.Top + 40));
                        if (he > 0) Theme.FillRound(g, Theme.Violet, full, 6);
                        if (hw > 0) Theme.FillRound(g, Theme.Accent, new RectangleF(x, area.Bottom - hw, bw, hw + 6), he > 0 ? 0 : 6);
                        g.ResetClip();
                    }
                    float ty = area.Bottom - (float)(b.Target / max * area.Height);
                    if (b.Target > 0) g.DrawLine(target, x - 6, ty, x + bw + 6, ty);
                    string val = b.Value.ToString("0");
                    var sz = TextRenderer.MeasureText(val, Theme.Semi(9));
                    TextRenderer.DrawText(g, val, Theme.Semi(9), new Point((int)(x + (bw - sz.Width) / 2), (int)(Math.Min(area.Bottom - hw - he, ty) - sz.Height - 2)), Theme.Text);
                    var lz = TextRenderer.MeasureText(b.Label, font);
                    TextRenderer.DrawText(g, b.Label, font, new Point((int)(area.Left + i * slot + (slot - lz.Width) / 2), area.Bottom + 7), Theme.Muted);
                }
            }
        }
    }
}
