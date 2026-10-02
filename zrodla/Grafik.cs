// Ewidencja Czasu Pracy – grafik miesięczny, roczny plan urlopów, dni wolne firmowe
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace EwidencjaCzasu
{
    // Grafik miesięczny: pracownicy × dni miesiąca, kolorowe kratki ze stanem dnia.
    class MonthSchedule : Control, IThemed
    {
        static readonly CultureInfo Pl = new CultureInfo("pl-PL");
        static readonly string[] Dow = { "nd", "pn", "wt", "śr", "cz", "pt", "sb" };
        const int NameW = 210, SumW = 86, HeadH = 48, RowH = 46, LegendH = 40;

        public event Action<string, DateTime, DayInfo> CellOpen;

        DateTime month;
        List<Employee> rows = new List<Employee>();
        readonly Dictionary<string, DayInfo> cells = new Dictionary<string, DayInfo>();
        readonly Dictionary<string, Summary> sums = new Dictionary<string, Summary>();
        readonly ToolTip tip = new ToolTip { InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 15000 };
        int hoverRow = -1, hoverDay = -1;

        public MonthSchedule()
        {
            Theme.DoubleBuffer(this);
            ResizeRedraw = true;
        }

        static string Key(string uid, int day) { return Store.Norm(uid) + "|" + day; }

        public void SetData(Data data, DateTime m)
        {
            month = new DateTime(m.Year, m.Month, 1);
            var to = month.AddMonths(1);
            var days = Calc.Range(data, month, to, null);
            cells.Clear();
            sums.Clear();
            foreach (var d in days) cells[Key(d.Uid, d.Day.Day)] = d;
            var withData = new HashSet<string>(days.Select(d => Store.Norm(d.Uid)));
            rows = data.Emps.Where(e => e.Active || withData.Contains(Store.Norm(e.Uid)))
                .OrderBy(e => e.Name, StringComparer.Create(Pl, true)).ToList();
            foreach (var e in rows)
                sums[Store.Norm(e.Uid)] = Calc.Summarize(e.Uid, e.Name, days.Where(d => Store.Norm(d.Uid) == Store.Norm(e.Uid)), month, to, data);
            Invalidate();
        }

        int DaysInMonth { get { return month == DateTime.MinValue ? 30 : DateTime.DaysInMonth(month.Year, month.Month); } }
        float CellW { get { return Math.Max(20f, (Width - NameW - SumW - 2f) / DaysInMonth); } }

        bool HitTest(Point p, out int row, out int day)
        {
            row = -1; day = -1;
            if (p.X < NameW || p.Y < HeadH) return false;
            row = (p.Y - HeadH) / RowH;
            day = (int)((p.X - NameW) / CellW) + 1;
            if (row >= rows.Count || day > DaysInMonth) { row = -1; day = -1; return false; }
            return true;
        }

        // stan kratki: kolor tła, kolor tekstu, krótki tekst
        void CellLook(Employee e, DateTime date, DayInfo d, out string kind, out string text)
        {
            kind = null; text = "";
            if (d == null) return;
            if (d.NoShow) { kind = "danger"; text = "—"; return; }
            if (d.Rows.Count > 0)
            {
                if (d.MissingPunch) { kind = "danger"; text = "!"; return; }
                if (d.OpenSince.HasValue) { kind = "accent"; text = "•"; return; }
                text = d.Total.TotalHours >= 10 ? ((int)d.Total.TotalHours).ToString() : Calc.Hm(d.Total);
                kind = d.HasNorm && d.WorkingDay && d.Total < d.DayNorm - TimeSpan.FromMinutes(15) ? "warning" : "success";
                return;
            }
            if (d.Abs != null)
            {
                text = d.Abs.Code;
                kind = d.Abs.Kind == 0 ? "info" : d.Abs.Kind == 3 ? "danger" : d.Abs.Code == "CH" ? "warning" : d.Abs.Kind == 2 || d.Abs.Code == "DW" ? "neutral" : "violet";
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Surface);
            if (month == DateTime.MinValue) return;
            int n = DaysInMonth;
            float cw = CellW;
            int gridBottom = HeadH + rows.Count * RowH;
            var today = DateTime.Today;
            var small = Theme.UI(8.5f);

            // kolumny dni wolnych i nagłówek
            for (int day = 1; day <= n; day++)
            {
                var date = new DateTime(month.Year, month.Month, day);
                float x = NameW + (day - 1) * cw;
                bool free = !PlCalendar.IsWorkingDay(date);
                if (free) using (var b = new SolidBrush(Theme.SurfaceAlt)) g.FillRectangle(b, x, 0, cw, gridBottom);
                var col = date == today ? Theme.Accent : free ? Theme.Muted : Theme.Text;
                TextRenderer.DrawText(g, day.ToString(), date == today ? Theme.Bold(9.5f) : Theme.Semi(9.5f),
                    new Rectangle((int)x, 6, (int)cw, 20), col, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Dow[(int)date.DayOfWeek], small, new Rectangle((int)x, 26, (int)cw, 16), Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }
            TextRenderer.DrawText(g, "Pracownik", Theme.Semi(9.5f), new Rectangle(12, 0, NameW, HeadH), Theme.Text, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "Suma", Theme.Semi(9.5f), new Rectangle(Width - SumW, 0, SumW - 10, HeadH), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);

            // wiersze
            for (int r = 0; r < rows.Count; r++)
            {
                var emp = rows[r];
                int y = HeadH + r * RowH;
                if (r == hoverRow) using (var b = new SolidBrush(Theme.Blend(Theme.Accent, Theme.Surface, 0.06))) g.FillRectangle(b, 0, y, Width, RowH);
                TextRenderer.DrawText(g, emp.Name, Theme.Semi(10f), new Rectangle(12, y + 5, NameW - 16, 20), emp.Active ? Theme.Text : Theme.Muted,
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Contracts.Short(emp.Contract), small, new Rectangle(12, y + 25, NameW - 16, 16), Theme.Muted,
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

                for (int day = 1; day <= n; day++)
                {
                    var date = new DateTime(month.Year, month.Month, day);
                    DayInfo d;
                    cells.TryGetValue(Key(emp.Uid, day), out d);
                    string kind, text;
                    CellLook(emp, date, d, out kind, out text);
                    if (kind == null) continue;
                    Color fg, bg;
                    Theme.StatusColors(kind, out fg, out bg);
                    var rc = new RectangleF(NameW + (day - 1) * cw + 2, y + 5, cw - 4, RowH - 10);
                    var st = g.Save();
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Theme.FillRound(g, bg, rc, 5);
                    if (r == hoverRow && day == hoverDay) Theme.DrawRound(g, fg, rc, 5);
                    g.Restore(st);
                    var f = text.Length > 3 ? Theme.UI(cw < 34 ? 7f : 8f) : Theme.Semi(cw < 26 ? 7.5f : 8.5f);
                    TextRenderer.DrawText(g, text, f, new Rectangle((int)rc.X - 4, (int)rc.Y, (int)rc.Width + 8, (int)rc.Height), fg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoClipping);
                }

                Summary s;
                if (sums.TryGetValue(Store.Norm(emp.Uid), out s))
                {
                    TextRenderer.DrawText(g, Calc.Hm(s.Total), Theme.Semi(10f), new Rectangle(Width - SumW, y + 5, SumW - 10, 20), Theme.Text,
                        TextFormatFlags.Right | TextFormatFlags.NoPadding);
                    string sub = s.HasNorm ? "z " + Calc.Hm(s.Norm) : "bez normy";
                    TextRenderer.DrawText(g, sub, small, new Rectangle(Width - SumW, y + 25, SumW - 10, 16), Theme.Muted,
                        TextFormatFlags.Right | TextFormatFlags.NoPadding);
                }
            }

            // linie siatki (ostre, 1 px)
            g.SmoothingMode = SmoothingMode.None;
            using (var pen = new Pen(Theme.Border))
            {
                g.DrawLine(pen, 0, HeadH - 1, Width, HeadH - 1);
                for (int r = 1; r <= rows.Count; r++) g.DrawLine(pen, 0, HeadH + r * RowH - 1, Width, HeadH + r * RowH - 1);
                for (int day = 0; day <= n; day++)
                {
                    int x = (int)(NameW + day * cw);
                    g.DrawLine(pen, x, 0, x, gridBottom - 1);
                }
                g.DrawLine(pen, Width - SumW, 0, Width - SumW, gridBottom - 1);
            }
            if (month.Year == today.Year && month.Month == today.Month)
                using (var pen = new Pen(Theme.Accent, 2)) g.DrawRectangle(pen, NameW + (today.Day - 1) * cw + 1, 1, cw - 2, gridBottom - 3);

            // legenda
            int ly = gridBottom + 14, lx = 12;
            foreach (var item in new[] {
                new[] { "success", "8:05", "obecność (godziny)" }, new[] { "warning", "6:30", "poniżej normy" }, new[] { "accent", "•", "teraz w pracy" },
                new[] { "violet", "UW", "urlop" }, new[] { "warning", "CH", "L4" }, new[] { "info", "PZ", "zdalnie / delegacja" },
                new[] { "neutral", "DW", "dzień wolny firmowy" }, new[] { "danger", "!", "brak odbicia / obecności" } })
            {
                Color fg, bg;
                Theme.StatusColors(item[0], out fg, out bg);
                var rc = new RectangleF(lx, ly, 34, 22);
                var st = g.Save();
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Theme.FillRound(g, bg, rc, 5);
                g.Restore(st);
                TextRenderer.DrawText(g, item[1], Theme.Semi(8f), Rectangle.Round(rc), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, item[2], small, new Point(lx + 40, ly + 4), Theme.Muted, TextFormatFlags.NoPadding);
                lx += 52 + TextRenderer.MeasureText(item[2], small).Width;
                if (lx > Width - 180) { lx = 12; ly += 30; }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int r, d;
            HitTest(e.Location, out r, out d);
            if (r == hoverRow && d == hoverDay) return;
            hoverRow = r; hoverDay = d;
            Invalidate();
            tip.SetToolTip(this, r >= 0 ? TipText(rows[r], d) : "");
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverRow = hoverDay = -1;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int r, d;
            if (!HitTest(e.Location, out r, out d)) return;
            DayInfo di;
            cells.TryGetValue(Key(rows[r].Uid, d), out di);
            var h = CellOpen;
            if (h != null) h(rows[r].Uid, new DateTime(month.Year, month.Month, d), di);
        }

        string TipText(Employee emp, int day)
        {
            var date = new DateTime(month.Year, month.Month, day);
            DayInfo d;
            cells.TryGetValue(Key(emp.Uid, day), out d);
            var lines = new List<string> { emp.Name + " – " + date.ToString("dddd, d MMMM", Pl) };
            if (d == null)
            {
                lines.Add(PlCalendar.Holiday(date) ?? (PlCalendar.IsWorkingDay(date) ? (date > DateTime.Today ? "" : "brak wpisów") : "dzień wolny"));
            }
            else
            {
                foreach (var r in d.Rows)
                    lines.Add((r.In.HasValue ? r.In.Value.ToString("HH:mm") : "?") + " – " + (r.Out.HasValue ? r.Out.Value.ToString("HH:mm") : "?") +
                        (r.Note != "" ? "  (" + r.Note + ")" : ""));
                if (d.Rows.Count > 0) lines.Add("Czas: " + Calc.Hm(d.Total) + (d.Overtime > TimeSpan.Zero ? "   nadgodziny: " + Calc.Hm(d.Overtime) : ""));
                var notes = d.Notes();
                if (notes != "") lines.Add(notes);
            }
            lines.Add("Dwuklik – popraw / uzupełnij");
            return string.Join("\n", lines.Where(l => l != ""));
        }
    }

    // Roczny plan urlopów: 12 miesięcy × 31 dni, w każdym dniu kolorowe paski osób nieobecnych.
    class YearPlanner : Control, IThemed
    {
        static readonly CultureInfo Pl = new CultureInfo("pl-PL");
        static readonly Color[] Palette =
        {
            Color.FromArgb(59, 130, 246), Color.FromArgb(16, 185, 129), Color.FromArgb(245, 158, 11), Color.FromArgb(168, 85, 247),
            Color.FromArgb(236, 72, 153), Color.FromArgb(20, 184, 166), Color.FromArgb(239, 68, 68), Color.FromArgb(132, 204, 22)
        };
        const int MonthW = 110, HeadH = 30, LegendH = 100;

        public event Action<DateTime> DayOpen;

        int year;
        List<Employee> people = new List<Employee>();
        readonly Dictionary<DateTime, List<KeyValuePair<int, Absence>>> byDay = new Dictionary<DateTime, List<KeyValuePair<int, Absence>>>();
        readonly Dictionary<DateTime, CompanyDay> company = new Dictionary<DateTime, CompanyDay>();
        readonly List<LeaveInfo> leave = new List<LeaveInfo>();
        readonly ToolTip tip = new ToolTip { InitialDelay = 250, ReshowDelay = 100, AutoPopDelay = 15000 };
        DateTime hover = DateTime.MinValue;

        public YearPlanner()
        {
            Theme.DoubleBuffer(this);
            ResizeRedraw = true;
        }

        public void SetData(Data data, int y)
        {
            year = y;
            people = data.Emps.Where(e => e.Active && e.HasNorm).OrderBy(e => e.Name, StringComparer.Create(Pl, true)).ToList();
            byDay.Clear();
            company.Clear();
            leave.Clear();
            foreach (var c in data.CompanyDays.Where(c => c.Day.Year == y)) company[c.Day] = c;
            for (int i = 0; i < people.Count; i++)
            {
                leave.Add(Calc.Leave(data, people[i].Uid, y));
                foreach (var a in data.Absences.Where(a => a.Day.Year == y && Store.Norm(a.Uid) == Store.Norm(people[i].Uid)))
                {
                    List<KeyValuePair<int, Absence>> l;
                    if (!byDay.TryGetValue(a.Day, out l)) byDay[a.Day] = l = new List<KeyValuePair<int, Absence>>();
                    l.Add(new KeyValuePair<int, Absence>(i, a));
                }
            }
            Invalidate();
        }

        float CellW { get { return (Width - MonthW - 2f) / 31f; } }
        float RowH { get { return Math.Max(28f, (Height - HeadH - LegendH) / 12f); } }

        DateTime Hit(Point p)
        {
            if (p.X < MonthW || p.Y < HeadH || year == 0) return DateTime.MinValue;
            int m = (int)((p.Y - HeadH) / RowH) + 1;
            int d = (int)((p.X - MonthW) / CellW) + 1;
            if (m < 1 || m > 12 || d < 1 || d > DateTime.DaysInMonth(year, m)) return DateTime.MinValue;
            return new DateTime(year, m, d);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Surface);
            if (year == 0) return;
            float cw = CellW, rh = RowH;
            var small = Theme.UI(8.5f);
            var today = DateTime.Today;
            for (int d = 1; d <= 31; d++)
                TextRenderer.DrawText(g, d.ToString(), small, new Rectangle((int)(MonthW + (d - 1) * cw), 6, (int)cw, 18), Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

            for (int m = 1; m <= 12; m++)
            {
                float y = HeadH + (m - 1) * rh;
                string mn = new DateTime(year, m, 1).ToString("MMMM", Pl);
                TextRenderer.DrawText(g, char.ToUpper(mn[0]) + mn.Substring(1), Theme.Semi(9.5f), new Rectangle(12, (int)y, MonthW - 12, (int)rh), Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                int dim = DateTime.DaysInMonth(year, m);
                for (int d = 1; d <= 31; d++)
                {
                    var rc = new RectangleF(MonthW + (d - 1) * cw, y, cw, rh);
                    if (d > dim)
                    {
                        using (var hb = new HatchBrush(HatchStyle.BackwardDiagonal, Theme.Border, Theme.Surface)) g.FillRectangle(hb, rc);
                        continue;
                    }
                    var date = new DateTime(year, m, d);
                    CompanyDay cd;
                    company.TryGetValue(date, out cd);
                    if (!PlCalendar.IsWorkingDay(date) || cd != null)
                        using (var b = new SolidBrush(cd != null ? Theme.NeutralSoft : Theme.SurfaceAlt)) g.FillRectangle(b, rc);
                    if (cd != null)
                        TextRenderer.DrawText(g, "DW", Theme.UI(7f), Rectangle.Round(rc), Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);

                    List<KeyValuePair<int, Absence>> list;
                    if (byDay.TryGetValue(date, out list) && list.Count > 0)
                    {
                        float bh = Math.Min(7f, (rh - 8) / list.Count);
                        var st = g.Save();
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        for (int k = 0; k < list.Count; k++)
                        {
                            var col = Palette[list[k].Key % Palette.Length];
                            if (list[k].Value.Code == "CH" || list[k].Value.Code == "NN") col = Theme.Blend(col, Theme.Surface, 0.55);
                            Theme.FillRound(g, col, new RectangleF(rc.X + 2, rc.Bottom - 4 - (k + 1) * bh, cw - 4, bh - 1), 2);
                        }
                        g.Restore(st);
                        // 3 i więcej osób naraz – czerwona ramka (kto zostaje w biurze?)
                        if (list.Count >= 3) using (var p = new Pen(Theme.Danger, 2)) g.DrawRectangle(p, rc.X + 1, rc.Y + 1, cw - 2, rh - 2);
                    }
                    if (date == today) using (var p = new Pen(Theme.Accent, 2)) g.DrawRectangle(p, rc.X + 1, rc.Y + 1, cw - 2, rh - 2);
                    if (date == hover) using (var p = new Pen(Theme.Text)) g.DrawRectangle(p, rc.X + 0.5f, rc.Y + 0.5f, cw - 1, rh - 1);
                }
            }

            using (var pen = new Pen(Theme.Border))
            {
                for (int m = 0; m <= 12; m++) g.DrawLine(pen, 0, HeadH + m * rh, Width, HeadH + m * rh);
                for (int d = 0; d <= 31; d++) g.DrawLine(pen, MonthW + d * cw, HeadH, MonthW + d * cw, HeadH + 12 * rh);
            }

            // legenda: kolor osoby + pozostały urlop
            float ly = HeadH + 12 * rh + 14, lx = 12;
            for (int i = 0; i < people.Count; i++)
            {
                string label = people[i].Name + "  (zostało " + leave[i].Remaining + " dni)";
                int w = TextRenderer.MeasureText(label, small).Width + 40;
                if (lx + w > Width - 10) { lx = 12; ly += 24; }
                var st = g.Save();
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Theme.FillRound(g, Palette[i % Palette.Length], new RectangleF(lx, ly + 3, 18, 10), 3);
                g.Restore(st);
                TextRenderer.DrawText(g, label, small, new Point((int)lx + 24, (int)ly), Theme.Text, TextFormatFlags.NoPadding);
                lx += w;
            }
            TextRenderer.DrawText(g, "Jaśniejszy pasek = L4 / nieusprawiedliwiona.  Czerwona ramka = 3 lub więcej osób nieobecnych jednego dnia.  Dwuklik w dzień – dodaj nieobecność.",
                small, new Point(12, (int)ly + 26), Theme.Muted, TextFormatFlags.NoPadding);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var d = Hit(e.Location);
            if (d == hover) return;
            hover = d;
            Invalidate();
            tip.SetToolTip(this, d == DateTime.MinValue ? "" : TipText(d));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = DateTime.MinValue;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            var d = Hit(e.Location);
            var h = DayOpen;
            if (d != DateTime.MinValue && h != null) h(d);
        }

        string TipText(DateTime d)
        {
            var lines = new List<string> { char.ToUpper(d.ToString("dddd", Pl)[0]) + d.ToString("dddd, d MMMM yyyy", Pl).Substring(1) };
            var h = PlCalendar.Holiday(d);
            if (h != null) lines.Add(h);
            CompanyDay cd;
            if (company.TryGetValue(d, out cd)) lines.Add("Dzień wolny firmowy" + (cd.Name != "" ? ": " + cd.Name : ""));
            List<KeyValuePair<int, Absence>> list;
            if (byDay.TryGetValue(d, out list))
                foreach (var kv in list) lines.Add("• " + people[kv.Key].Name + " – " + AbsTypes.Find(kv.Value.Code).Name);
            else if (cd == null && h == null) lines.Add("wszyscy w pracy");
            return string.Join("\n", lines);
        }
    }

    // Dni wolne dla całej firmy (np. za święto w sobotę, firmowa Wigilia).
    class CompanyDaysForm : ThemedForm
    {
        static readonly CultureInfo Pl = new CultureInfo("pl-PL");
        readonly DataGridView grid;
        readonly DateBox date;
        readonly ThemedCombo kind;
        readonly TextBox name;
        readonly Label hint;
        List<CompanyDay> days;
        public bool Changed;

        public CompanyDaysForm(int year)
        {
            Text = "Dni wolne firmowe";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 560);
            MinimumSize = new Size(640, 420);
            Padding = new Padding(24, 18, 24, 18);
            days = Store.LoadCompanyDays();

            var header = new Panel { Dock = DockStyle.Top, Height = 64 };
            header.Controls.Add(new Label { Text = "Dni wolne firmowe", Font = Theme.Semi(18), AutoSize = true, Location = new Point(0, 0) });
            header.Controls.Add(new Label { Text = "Dzień wolny dla wszystkich na umowie o pracę – nie trzeba wpisywać nieobecności każdemu osobno.", Tag = "muted", AutoSize = true, Location = new Point(2, 38) });

            hint = new Label { Dock = DockStyle.Top, Height = 46, Tag = "muted" };
            var sat = PlCalendar.Holidays(year).Where(h => h.Key.DayOfWeek == DayOfWeek.Saturday).OrderBy(h => h.Key).ToList();
            hint.Text = sat.Count == 0
                ? "W " + year + " r. żadne święto nie wypada w sobotę."
                : "W " + year + " r. święto w sobotę: " + string.Join(", ", sat.Select(h => h.Key.ToString("d.MM") + " (" + h.Value + ")")) +
                  ".\nZa każde takie święto pracownikom należy się inny dzień wolny – wybierz rodzaj „za święto w sobotę”.";

            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, WrapContents = false, Padding = new Padding(0, 6, 0, 8) };
            date = new DateBox(DateBox.Mode.Day, DateTime.Today) { Width = 190, Margin = new Padding(0, 2, 8, 0) };
            kind = new ThemedCombo { Width = 230, Margin = new Padding(0, 5, 8, 0) };
            kind.Items.AddRange(new object[] { "Płatny dzień wolny", "Za święto w sobotę" });
            kind.SelectedIndex = 0;
            name = new TextBox { Width = 170, Margin = new Padding(0, 6, 8, 0) };
            var add = Theme.Button("  Dodaj", "primary", (s, e) => Add());
            add.Margin = new Padding(0, 2, 0, 0);
            form.Controls.AddRange(new Control[] { date, kind, name, add });
            var nameHint = new Label { Dock = DockStyle.Top, Height = 22, Tag = "muted", Text = "Data  •  rodzaj  •  nazwa (np. „Wigilia firmowa”, „za 15.08”)" };

            grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            foreach (var c in new[] { "Data", "Dzień", "Rodzaj", "Nazwa" }) grid.Columns.Add(c, c);
            grid.Columns[0].FillWeight = 70; grid.Columns[1].FillWeight = 70; grid.Columns[2].FillWeight = 120; grid.Columns[3].FillWeight = 160;
            Theme.StyleGrid(grid);
            grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); };
            var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(10) };
            card.Controls.Add(grid);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(0, 14, 0, 0) };
            var close = new Button { Text = "Zamknij", Dock = DockStyle.Right, Width = 110 };
            close.Click += (s, e) => Close();
            var del = Theme.Button("  Usuń zaznaczone", "danger", (s, e) => DeleteSelected());
            del.Dock = DockStyle.Left;
            bottom.Controls.Add(close);
            bottom.Controls.Add(del);

            Controls.Add(card);
            Controls.Add(bottom);
            Controls.Add(form);
            Controls.Add(nameHint);
            Controls.Add(hint);
            Controls.Add(header);
            Fill();
        }

        void Fill()
        {
            grid.Rows.Clear();
            foreach (var d in days.OrderBy(x => x.Day))
                grid.Rows.Add(d.Day.ToString("yyyy-MM-dd"), d.Day.ToString("dddd", Pl), d.Code == "WS" ? "za święto w sobotę" : "płatny dzień wolny", d.Name);
            grid.ClearSelection();
        }

        void Add()
        {
            var d = date.Value.Date;
            if (!PlCalendar.IsWorkingDay(d))
            {
                MessageBox.Show("Ten dzień i tak jest wolny (weekend lub święto).", Text);
                return;
            }
            if (days.Any(x => x.Day == d))
            {
                MessageBox.Show("Ten dzień jest już na liście.", Text);
                return;
            }
            var cd = new CompanyDay { Day = d, Code = kind.SelectedIndex == 1 ? "WS" : "DW", Name = name.Text.Trim() };
            var list = new List<CompanyDay>(days) { cd };
            try
            {
                Store.SaveCompanyDays(list);
                Store.Audit("DZIEŃ WOLNY FIRMOWY DODANY", "", d.ToString("yyyy-MM-dd") + " " + cd.Code + " " + cd.Name);
                days = list;
                Changed = true;
                name.Text = "";
                Fill();
            }
            catch (Exception ex) { MessageBox.Show("Nie udało się zapisać:\n" + ex.Message, Text); }
        }

        void DeleteSelected()
        {
            var sel = grid.SelectedRows.Cast<DataGridViewRow>().Select(r => DateTime.Parse(Convert.ToString(r.Cells[0].Value), CultureInfo.InvariantCulture)).ToList();
            if (sel.Count == 0) { MessageBox.Show("Najpierw zaznacz dzień na liście.", Text); return; }
            if (MessageBox.Show("Usunąć zaznaczone dni wolne (" + sel.Count + ")?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var list = days.Where(x => !sel.Contains(x.Day)).ToList();
            try
            {
                Store.SaveCompanyDays(list);
                foreach (var d in sel) Store.Audit("DZIEŃ WOLNY FIRMOWY USUNIĘTY", d.ToString("yyyy-MM-dd"), "");
                days = list;
                Changed = true;
                Fill();
            }
            catch (Exception ex) { MessageBox.Show("Nie udało się usunąć:\n" + ex.Message, Text); }
        }
    }
}
