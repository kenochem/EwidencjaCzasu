// Ewidencja Czasu Pracy – wygląd: motywy (jasny / ciemny / jak w Windows), kolory akcentu,
// nowoczesne przyciski, karty, nawigacja i stylowanie standardowych kontrolek.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EwidencjaCzasu
{
    static class Theme
    {
        public static bool Dark;
        public static Color Bg, Sidebar, Surface, SurfaceAlt, Border, Text, Muted, Hover,
            Accent, AccentHover, AccentSoft, OnAccent,
            Success, SuccessSoft, Warning, WarningSoft, Danger, DangerSoft, Info, InfoSoft, Violet, VioletSoft, Neutral, NeutralSoft;

        public static readonly string[] Modes = { "system", "jasny", "ciemny" };
        public static readonly string[] ModeNames = { "Jak w Windows", "Jasny", "Ciemny" };
        public static readonly string[] AccentNames = { "niebieski", "indygo", "fioletowy", "różowy", "pomarańczowy", "zielony", "turkusowy", "grafitowy" };
        static readonly Color[] AccentLight = { C(0x2563EB), C(0x4F46E5), C(0x7C3AED), C(0xDB2777), C(0xEA580C), C(0x16A34A), C(0x0D9488), C(0x475569) };
        static readonly Color[] AccentDark = { C(0x60A5FA), C(0x818CF8), C(0xA78BFA), C(0xF472B6), C(0xFB923C), C(0x4ADE80), C(0x2DD4BF), C(0xCBD5E1) };

        public static event Action Changed;

        static Color C(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static Color Blend(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R * t + b.R * (1 - t)), (int)(a.G * t + b.G * (1 - t)), (int)(a.B * t + b.B * (1 - t)));
        }

        public static Color AccentPreview(int i) { return Dark ? AccentDark[i] : AccentLight[i]; }

        public static bool SystemDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        public static void Load()
        {
            Dark = Cfg.ThemeMode == "ciemny" || (Cfg.ThemeMode == "system" && SystemDark());
            int ai = Math.Max(0, Array.IndexOf(AccentNames, Cfg.Accent));
            if (!Dark)
            {
                Bg = C(0xF3F5F9); Sidebar = C(0xFFFFFF); Surface = C(0xFFFFFF); SurfaceAlt = C(0xF8FAFC);
                Border = C(0xE3E7EE); Text = C(0x111827); Muted = C(0x6B7280); Hover = C(0xF1F4F9);
                Accent = AccentLight[ai]; AccentHover = Blend(Color.Black, Accent, 0.12); OnAccent = Color.White;
                Success = C(0x15803D); Warning = C(0xB45309); Danger = C(0xDC2626); Info = C(0x0369A1); Violet = C(0x6D28D9); Neutral = C(0x4B5563);
                SuccessSoft = C(0xDCFCE7); WarningSoft = C(0xFEF3C7); DangerSoft = C(0xFEE2E2); InfoSoft = C(0xE0F2FE); VioletSoft = C(0xEDE9FE); NeutralSoft = C(0xF1F3F6);
                AccentSoft = Blend(Accent, Surface, 0.12);
            }
            else
            {
                Bg = C(0x0F1116); Sidebar = C(0x14171D); Surface = C(0x1A1E25); SurfaceAlt = C(0x20252D);
                Border = C(0x2B313B); Text = C(0xE6E8EC); Muted = C(0x9AA3AF); Hover = C(0x242A33);
                Accent = AccentDark[ai]; AccentHover = Blend(Color.White, Accent, 0.12); OnAccent = C(0x0B1220);
                Success = C(0x4ADE80); Warning = C(0xFBBF24); Danger = C(0xF87171); Info = C(0x38BDF8); Violet = C(0xA78BFA); Neutral = C(0x9CA3AF);
                SuccessSoft = Blend(C(0x22C55E), Surface, 0.18); WarningSoft = Blend(C(0xF59E0B), Surface, 0.18);
                DangerSoft = Blend(C(0xEF4444), Surface, 0.20); InfoSoft = Blend(C(0x0EA5E9), Surface, 0.18);
                VioletSoft = Blend(C(0x8B5CF6), Surface, 0.20); NeutralSoft = C(0x252A33);
                AccentSoft = Blend(Accent, Surface, 0.20);
            }
        }

        public static void RaiseChanged()
        {
            var h = Changed;
            if (h != null) h();
        }

        // ---------------------------------------------------------- czcionki

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static string iconFamily;

        static Font Get(string family, float size, FontStyle style)
        {
            string key = family + "|" + size + "|" + style;
            Font f;
            if (!fonts.TryGetValue(key, out f)) fonts[key] = f = new Font(family, size, style);
            return f;
        }

        public static Font UI(float size) { return Get("Segoe UI", size, FontStyle.Regular); }
        public static Font Semi(float size) { return Get("Segoe UI Semibold", size, FontStyle.Regular); }
        public static Font Bold(float size) { return Get("Segoe UI", size, FontStyle.Bold); }

        public static Font Icon(float size)
        {
            if (iconFamily == null)
            {
                var names = new InstalledFontCollection().Families.Select(f => f.Name).ToList();
                iconFamily = names.Contains("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            }
            return Get(iconFamily, size, FontStyle.Regular);
        }

        public static bool IsGlyph(char c) { return c >= '' && c <= ''; }

        // ---------------------------------------------------------- rysowanie

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var p = Round(r, radius))
            using (var pen = new Pen(c)) g.DrawPath(pen, p);
        }

        public static void Glyph(Graphics g, string glyph, float size, Color c, RectangleF r)
        {
            TextRenderer.DrawText(g, glyph, Icon(size), Rectangle.Round(r), c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        public static void StatusColors(string kind, out Color fg, out Color bg)
        {
            switch (kind)
            {
                case "success": fg = Success; bg = SuccessSoft; break;
                case "info": fg = Info; bg = InfoSoft; break;
                case "warning": fg = Warning; bg = WarningSoft; break;
                case "danger": fg = Danger; bg = DangerSoft; break;
                case "violet": fg = Violet; bg = VioletSoft; break;
                case "accent": fg = Accent; bg = AccentSoft; break;
                default: fg = Muted; bg = NeutralSoft; break;
            }
        }

        public static void Pill(Graphics g, string text, string kind, Rectangle cell, Font font)
        {
            Color fg, bg;
            StatusColors(kind, out fg, out bg);
            var sz = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding);
            var r = new RectangleF(cell.X + 8, cell.Y + (cell.Height - 24) / 2f, sz.Width + 22, 24);
            var state = g.Save(); // nie zostawiamy wygładzania dla dalszego rysowania tabeli
            g.SmoothingMode = SmoothingMode.AntiAlias;
            FillRound(g, bg, r, 12);
            using (var dot = new SolidBrush(fg)) g.FillEllipse(dot, r.X + 9, r.Y + 9, 6, 6);
            g.Restore(state);
            TextRenderer.DrawText(g, text, font, new Rectangle((int)r.X + 18, (int)r.Y, sz.Width + 4, (int)r.Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        // Kolor tła rodzica (do wygładzania krawędzi zaokrąglonych kontrolek).
        public static Color BackOf(Control c)
        {
            while (c != null)
            {
                if (c.BackColor.A == 255) return c.BackColor;
                c = c.Parent;
            }
            return Bg;
        }

        // ---------------------------------------------------------- okna (pasek tytułu Windows 11)

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string id);

        static int Rgb(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        public static void TitleBar(Form f, Color caption)
        {
            if (!f.IsHandleCreated) return;
            try
            {
                int dark = Dark ? 1 : 0;
                DwmSetWindowAttribute(f.Handle, 20, ref dark, 4);      // ciemny pasek tytułu
                int cap = Rgb(caption), txt = Rgb(Text);
                DwmSetWindowAttribute(f.Handle, 35, ref cap, 4);       // kolor paska (Windows 11)
                DwmSetWindowAttribute(f.Handle, 36, ref txt, 4);       // kolor tytułu
                int round = 2;
                DwmSetWindowAttribute(f.Handle, 33, ref round, 4);     // zaokrąglone rogi
            }
            catch { }
        }

        public static void RoundCorners(Form f)
        {
            try { int round = 2; DwmSetWindowAttribute(f.Handle, 33, ref round, 4); } catch { }
        }

        public static void DoubleBuffer(Control c)
        {
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c, true, null);
        }

        // ---------------------------------------------------------- stylowanie kontrolek

        public static void Apply(Control root)
        {
            Style(root);
            foreach (Control c in root.Controls) Apply(c);
        }

        static void Style(Control c)
        {
            string tag = c.Tag as string ?? "";
            if (c is Form)
            {
                c.BackColor = Bg;
                c.ForeColor = Text;
            }
            else if (c is IThemed) { c.Invalidate(); }
            else if (c is DataGridView) StyleGrid((DataGridView)c);
            else if (c is Button) StyleButton((Button)c);
            else if (c is LinkLabel) { var l = (LinkLabel)c; l.LinkColor = Accent; l.ActiveLinkColor = AccentHover; l.BackColor = BackOf(c.Parent); }
            else if (c is Label)
            {
                c.BackColor = tag == "surface" ? Surface : BackOf(c.Parent);
                c.ForeColor = tag == "muted" ? Muted : Text;
            }
            else if (c is TextBoxBase) { var t = (TextBoxBase)c; t.BorderStyle = BorderStyle.FixedSingle; t.BackColor = SurfaceAlt; t.ForeColor = Text; }
            else if (c is ComboBox)
            {
                var cb = (ComboBox)c;
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = SurfaceAlt;
                cb.ForeColor = Text;
                if (!combos.Contains(cb))
                {
                    combos.Add(cb);
                    cb.DrawMode = DrawMode.OwnerDrawFixed;
                    cb.ItemHeight = 24;
                    cb.DrawItem += DrawComboItem;
                    cb.Disposed += (s, e) => combos.Remove(cb);
                }
                cb.Invalidate();
            }
            else if (c is NumericUpDown) { var n = (NumericUpDown)c; n.BorderStyle = BorderStyle.FixedSingle; n.BackColor = SurfaceAlt; n.ForeColor = Text; }
            else if (c is DateTimePicker)
            {
                var d = (DateTimePicker)c;
                d.CalendarMonthBackground = Surface; d.CalendarForeColor = Text; d.CalendarTitleBackColor = Accent; d.CalendarTitleForeColor = OnAccent;
            }
            else if (c is CheckBox) { c.ForeColor = Text; c.BackColor = BackOf(c.Parent); }
            else if (c is SplitContainer) { c.BackColor = BackOf(c.Parent); }
            else if (c is SplitterPanel) { c.BackColor = BackOf(c.Parent.Parent); }
            else if (c is Panel || c is FlowLayoutPanel || c is TableLayoutPanel)
            {
                if (tag == "sidebar") c.BackColor = Sidebar;
                else if (tag == "surface") c.BackColor = Surface;
                else if (tag == "bg") c.BackColor = Bg;
                else c.BackColor = BackOf(c.Parent);
            }
        }

        static readonly HashSet<ComboBox> combos = new HashSet<ComboBox>();

        static void DrawComboItem(object s, DrawItemEventArgs e)
        {
            var cb = (ComboBox)s;
            if (e.Index < 0) return;
            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool sel = (e.State & DrawItemState.Selected) != 0 && !edit;
            using (var b = new SolidBrush(sel ? AccentSoft : SurfaceAlt)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, cb.GetItemText(cb.Items[e.Index]), cb.Font,
                new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        // Ciemne / jasne paski przewijania i kalendarz bez stylu systemowego (żeby przyjął nasze kolory).
        public static void ThemeScroll(Control c)
        {
            if (c.IsHandleCreated) SetWindowTheme(c.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null);
        }

        public static void PlainStyle(Control c)
        {
            if (c.IsHandleCreated) SetWindowTheme(c.Handle, "", "");
        }

        static void ScrollHandleCreated(object s, EventArgs e) { ThemeScroll((Control)s); }

        // Siatka tabeli: ramka zewnętrzna, linia pod nagłówkiem, linie między kolumnami i wierszami.
        static void GridLines(object s, PaintEventArgs e)
        {
            var g = (DataGridView)s;
            int left = int.MaxValue, right = 0;
            var xs = new List<int>();
            foreach (DataGridViewColumn c in g.Columns)
            {
                if (!c.Visible) continue;
                var cr = g.GetColumnDisplayRectangle(c.Index, false);
                if (cr.Width == 0) continue;
                left = Math.Min(left, cr.Left);
                right = Math.Max(right, cr.Right);
                xs.Add(cr.Right - 1);
            }
            if (right == 0) return;
            int header = g.ColumnHeadersVisible ? g.ColumnHeadersHeight : 0;
            int bottom = header;
            var ys = new List<int>();
            int first = g.Rows.Count > 0 ? g.FirstDisplayedScrollingRowIndex : -1;
            if (first >= 0)
                for (int r = first; r < g.Rows.Count; r++)
                {
                    if (!g.Rows[r].Visible) continue;
                    var rr = g.GetRowDisplayRectangle(r, false);
                    if (rr.Height == 0) break;
                    ys.Add(rr.Bottom - 1);
                    bottom = rr.Bottom;
                }
            // zawsze ostre linie 1 px, niezależnie od ustawień pozostawionych przez rysowanie komórek
            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.None;
            using (var pen = new Pen(g.GridColor))
            {
                foreach (var y in ys) e.Graphics.DrawLine(pen, left, y, right - 1, y);
                foreach (var x in xs) e.Graphics.DrawLine(pen, x, 0, x, bottom - 1);
                e.Graphics.DrawLine(pen, left, 0, left, bottom - 1);
                e.Graphics.DrawLine(pen, left, 0, right - 1, 0);
                if (header > 0) e.Graphics.DrawLine(pen, left, header - 1, right - 1, header - 1);
            }
        }

        // bez kropkowanej ramki wokół aktywnej komórki – wszystkie ramki wyglądają tak samo
        static void NoFocusFrame(object s, DataGridViewRowPrePaintEventArgs e) { e.PaintParts &= ~DataGridViewPaintParts.Focus; }

        public static void StyleGrid(DataGridView g)
        {
            DoubleBuffer(g);
            // klasyczna tabela: pełna siatka z ramkami komórek, wyraźny nagłówek
            g.EnableHeadersVisualStyles = false;
            g.BorderStyle = BorderStyle.None;
            // Wbudowane ramki DataGridView potrafią się rysować raz jaśniej, raz ciemniej (przy kliknięciu,
            // odświeżaniu pojedynczych komórek) – wyłączamy je i całą siatkę rysujemy sami, zawsze tak samo.
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.Paint -= GridLines;
            g.Paint += GridLines;
            g.GridColor = Dark ? Blend(Text, Surface, 0.17) : C(0xD3D9E2);
            g.BackgroundColor = Surface;
            g.DefaultCellStyle.BackColor = Surface;
            g.DefaultCellStyle.ForeColor = Text;
            g.DefaultCellStyle.SelectionBackColor = AccentSoft;
            g.DefaultCellStyle.SelectionForeColor = Text;
            g.DefaultCellStyle.Font = UI(10);
            g.DefaultCellStyle.Padding = new Padding(6, 0, 4, 0);
            var head = Dark ? C(0x232831) : C(0xEEF1F6);
            g.ColumnHeadersDefaultCellStyle.BackColor = head;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Text;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = head;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;
            g.ColumnHeadersDefaultCellStyle.Font = Semi(9.5f);
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 7, 4, 7);
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            g.RowHeadersVisible = false;
            g.RowPrePaint -= NoFocusFrame;
            g.RowPrePaint += NoFocusFrame;
            if (g.RowTemplate.Height < 34) g.RowTemplate.Height = 36;
            foreach (DataGridViewRow r in g.Rows) if (r.Height < g.RowTemplate.Height) r.Height = g.RowTemplate.Height;
            foreach (Control sb in g.Controls)
            {
                if (!(sb is ScrollBar)) continue;
                ThemeScroll(sb);
                sb.HandleCreated -= ScrollHandleCreated;
                sb.HandleCreated += ScrollHandleCreated;
                sb.VisibleChanged -= ScrollHandleCreated;
                sb.VisibleChanged += ScrollHandleCreated;
            }
            g.Invalidate();
        }

        // ---------------------------------------------------------- przyciski
        // Tag przycisku: "primary" (wyróżniony), "danger" (usuwanie), "ghost" (bez tła).
        // AcceptButton okna jest automatycznie wyróżniony. Pierwszy znak z czcionki ikon = ikona.

        class BtnState { public bool Hover, Down; }
        static readonly Dictionary<Button, BtnState> buttons = new Dictionary<Button, BtnState>();

        public static void StyleButton(Button b)
        {
            if (buttons.ContainsKey(b)) { b.Invalidate(); return; }
            var st = new BtnState();
            buttons[b] = st;
            DoubleBuffer(b);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.Font = UI(10);
            if (b.Height < 34) b.Height = 34;
            b.MouseEnter += (s, e) => { st.Hover = true; b.Invalidate(); };
            b.MouseLeave += (s, e) => { st.Hover = false; st.Down = false; b.Invalidate(); };
            b.MouseDown += (s, e) => { st.Down = true; b.Invalidate(); };
            b.MouseUp += (s, e) => { st.Down = false; b.Invalidate(); };
            b.Paint += (s, e) => PaintButton(b, st, e.Graphics);
            b.Disposed += (s, e) => buttons.Remove(b);
        }

        static void PaintButton(Button b, BtnState st, Graphics g)
        {
            var form = b.FindForm();
            string v = b.Tag as string ?? "";
            if (v == "" && form != null && form.AcceptButton == b) v = "primary";
            Color fill, text, border = Color.Empty;
            switch (v)
            {
                case "primary":
                    fill = st.Hover ? AccentHover : Accent; text = OnAccent; break;
                case "danger":
                    fill = st.Hover ? Blend(Danger, Surface, 0.28) : DangerSoft; text = Danger; break;
                case "ghost":
                    fill = st.Hover ? Hover : BackOf(b.Parent); text = Text; break;
                default:
                    fill = st.Hover ? Hover : Surface; text = Text; border = Border; break;
            }
            if (st.Down) fill = Blend(Dark ? Color.White : Color.Black, fill, 0.08);
            if (!b.Enabled) { text = Muted; }

            g.Clear(BackOf(b.Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, b.Width - 1.5f, b.Height - 1.5f);
            FillRound(g, fill, r, 8);
            if (border != Color.Empty) DrawRound(g, border, r, 8);

            string label = b.Text;
            string glyph = null;
            if (label.Length > 0 && IsGlyph(label[0])) { glyph = label.Substring(0, 1); label = label.Substring(1).Trim(); }
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            int tw = TextRenderer.MeasureText(g, label, b.Font, Size.Empty, flags).Width;
            int gw = glyph != null ? 16 + (label.Length > 0 ? 8 : 0) : 0;
            int x = (b.Width - tw - gw) / 2;
            if (glyph != null)
            {
                TextRenderer.DrawText(g, glyph, Icon(11), new Rectangle(x, 0, 16, b.Height), text, flags | TextFormatFlags.HorizontalCenter);
                x += gw;
            }
            TextRenderer.DrawText(g, label, b.Font, new Rectangle(x, 0, tw + 2, b.Height), text, flags);
        }

        public static Button Button(string text, string variant, EventHandler click)
        {
            var b = new Button { Text = text, Tag = variant, AutoSize = true, Height = 36, Padding = new Padding(12, 0, 12, 0), Margin = new Padding(4, 4, 4, 4) };
            if (click != null) b.Click += click;
            StyleButton(b);
            return b;
        }
    }

    // Pole daty w stylu aplikacji: ‹ wartość ›, kółko myszy zmienia, klik otwiera kalendarz.
    class DateBox : Control, IThemed
    {
        public enum Mode { Day, Month, Year }
        static readonly System.Globalization.CultureInfo Pl = new System.Globalization.CultureInfo("pl-PL");
        readonly Mode mode;
        DateTime value;
        int hoverZone = -1; // 0 = lewa strzałka, 1 = środek, 2 = prawa strzałka
        public event EventHandler ValueChanged;

        public DateBox(Mode mode, DateTime initial)
        {
            this.mode = mode;
            value = Normalize(initial);
            Size = new Size(mode == Mode.Year ? 120 : mode == Mode.Month ? 190 : 200, 34);
            Cursor = Cursors.Hand;
            Theme.DoubleBuffer(this);
            ResizeRedraw = true;
        }

        DateTime Normalize(DateTime d)
        {
            return mode == Mode.Day ? d.Date : mode == Mode.Month ? new DateTime(d.Year, d.Month, 1) : new DateTime(d.Year, 1, 1);
        }

        public DateTime Value
        {
            get { return value; }
            set
            {
                var v = Normalize(value);
                if (v == this.value) return;
                this.value = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        void Step(int dir)
        {
            Value = mode == Mode.Day ? value.AddDays(dir) : mode == Mode.Month ? value.AddMonths(dir) : value.AddYears(dir);
        }

        int Zone(Point p) { return p.X < 30 ? 0 : p.X > Width - 30 ? 2 : 1; }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int z = Zone(e.Location); if (z != hoverZone) { hoverZone = z; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverZone = -1; Invalidate(); }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Step(e.Delta > 0 ? 1 : -1); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int z = Zone(e.Location);
            if (z == 0) Step(-1);
            else if (z == 2) Step(1);
            else if (mode == Mode.Day) OpenCalendar();
        }

        void OpenCalendar()
        {
            var cal = new MonthCalendar { MaxSelectionCount = 1, ShowToday = true, ShowTodayCircle = true };
            cal.SetDate(value);
            var hostItem = new ToolStripControlHost(cal) { Margin = Padding.Empty, Padding = Padding.Empty };
            var drop = new ToolStripDropDown { Padding = Padding.Empty, BackColor = Theme.Surface };
            drop.Items.Add(hostItem);
            cal.HandleCreated += (s, e) =>
            {
                Theme.PlainStyle(cal);
                cal.BackColor = Theme.Surface;
                cal.ForeColor = Theme.Text;
                cal.TitleBackColor = Theme.Accent;
                cal.TitleForeColor = Theme.OnAccent;
                cal.TrailingForeColor = Theme.Muted;
            };
            cal.DateSelected += (s, e) => { Value = e.Start; drop.Close(); };
            drop.Closed += (s, e) => drop.Dispose();
            drop.Show(this, new Point(0, Height + 2));
        }

        string Display()
        {
            if (mode == Mode.Year) return value.Year.ToString();
            if (mode == Mode.Month) { var s = value.ToString("MMMM yyyy", Pl); return char.ToUpper(s[0]) + s.Substring(1); }
            return value.ToString("d.MM.yyyy", Pl) + (Width >= 185 ? "  " + value.ToString("ddd", Pl) : "");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(Parent));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRound(g, Theme.SurfaceAlt, r, 8);
            Theme.DrawRound(g, hoverZone >= 0 || Focused ? Theme.Accent : Theme.Border, r, 8);
            if (hoverZone == 0) Theme.FillRound(g, Theme.Hover, new RectangleF(3, 3, 26, Height - 7), 6);
            if (hoverZone == 2) Theme.FillRound(g, Theme.Hover, new RectangleF(Width - 30, 3, 26, Height - 7), 6);
            Theme.Glyph(g, "", 9, Theme.Muted, new RectangleF(3, 0, 26, Height));
            Theme.Glyph(g, "", 9, Theme.Muted, new RectangleF(Width - 30, 0, 26, Height));
            string text = Display();
            var font = Theme.UI(10);
            int tw = TextRenderer.MeasureText(text, font).Width;
            int gx = (Width - tw - (mode == Mode.Day ? 22 : 0)) / 2;
            if (mode == Mode.Day)
            {
                Theme.Glyph(g, "", 10, Theme.Muted, new RectangleF(gx, 0, 18, Height));
                gx += 22;
            }
            TextRenderer.DrawText(g, text, font, new Rectangle(gx, 0, tw + 4, Height), Theme.Text, TextFormatFlags.VerticalCenter);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Step(-1);
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Step(1);
        }
    }

    // Lista rozwijana z obramowaniem i strzałką w kolorach motywu (systemowa ma białą ramkę).
    class ThemedCombo : ComboBox
    {
        bool hover;

        public ThemedCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000F || !IsHandleCreated) return; // WM_PAINT
            using (var g = Graphics.FromHwnd(Handle))
            {
                var arrow = new Rectangle(Width - 24, 1, 23, Height - 2);
                using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, arrow);
                Theme.Glyph(g, "", 8, Theme.Muted, arrow);
                using (var p = new Pen(BackColor)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
                using (var p = new Pen(hover || Focused ? Theme.Accent : Theme.Border)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    // Pole godziny (GG:MM) – można wpisać dokładną godzinę z klawiatury.
    class TimeBox : MaskedTextBox
    {
        public TimeBox(TimeSpan t)
        {
            Mask = "00:00";
            PromptChar = '_';
            TextAlign = HorizontalAlignment.Center;
            Width = 72;
            Font = Theme.UI(10.5f);
            Time = t;
        }

        public TimeSpan? Time
        {
            get
            {
                var p = Text.Split(':');
                int h, m;
                if (p.Length == 2 && int.TryParse(p[0], out h) && int.TryParse(p[1], out m) && h >= 0 && h < 24 && m >= 0 && m < 60)
                    return new TimeSpan(h, m, 0);
                return null;
            }
            set { Text = value.HasValue ? value.Value.ToString(@"hh\:mm") : ""; }
        }
    }

    interface IThemed { }

    // Okno z motywem: kolory, ciemny pasek tytułu, przeładowanie po zmianie motywu.
    class ThemedForm : Form
    {
        public ThemedForm()
        {
            Font = Theme.UI(10);
        }

        protected virtual Color CaptionColor { get { return Theme.Bg; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.TitleBar(this, CaptionColor);
        }

        protected override void OnLoad(EventArgs e)
        {
            Theme.Apply(this);
            Theme.Changed += ThemeChanged;
            base.OnLoad(e);
        }

        void ThemeChanged()
        {
            if (IsDisposed) return;
            Theme.TitleBar(this, CaptionColor);
            Theme.Apply(this);
            OnThemeChanged();
            Invalidate(true);
        }

        protected virtual void OnThemeChanged() { }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Theme.Changed -= ThemeChanged;
            base.OnFormClosed(e);
        }
    }

    // Karta: zaokrąglony kontener z obramowaniem.
    class Card : Panel, IThemed
    {
        public Card()
        {
            Padding = new Padding(8);
            Theme.DoubleBuffer(this);
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRound(g, Theme.Surface, r, 12);
            Theme.DrawRound(g, Theme.Border, r, 12);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            e.Control.BackColor = Theme.Surface;
        }

        public override Color BackColor { get { return Theme.Surface; } set { } }
    }

    // Kafelek z liczbą (np. „W biurze 5 / 6”).
    class StatCard : Control, IThemed
    {
        public string Glyph = "", Caption = "", Value = "", Hint = "", Kind = "accent";
        bool hover;

        public StatCard()
        {
            Theme.DoubleBuffer(this);
            ResizeRedraw = true;
            Height = 92;
        }

        public void Set(string value, string hint, string kind)
        {
            Value = value; Hint = hint; Kind = kind;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (Cursor == Cursors.Hand) { hover = true; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRound(g, hover ? Theme.Hover : Theme.Surface, r, 12);
            Theme.DrawRound(g, Theme.Border, r, 12);
            Color fg, bg;
            Theme.StatusColors(Kind, out fg, out bg);
            var icon = new RectangleF(16, (Height - 44) / 2f, 44, 44);
            Theme.FillRound(g, bg, icon, 12);
            Theme.Glyph(g, Glyph, 16, fg, icon);
            TextRenderer.DrawText(g, Caption, Theme.UI(9.5f), new Point(74, 16), Theme.Muted, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Value, Theme.Semi(19), new Point(72, 34), Theme.Text, TextFormatFlags.NoPadding);
            if (Hint != "")
            {
                int vw = TextRenderer.MeasureText(g, Value, Theme.Semi(19), Size.Empty, TextFormatFlags.NoPadding).Width;
                TextRenderer.DrawText(g, Hint, Theme.UI(9.5f), new Rectangle(80 + vw, 40, Width - 90 - vw, 22), Theme.Muted,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            }
        }
    }

    // Pasek z komunikatem (ostrzeżenie / informacja).
    class Banner : Control, IThemed
    {
        public string Kind = "warning", Glyph = "";

        public Banner()
        {
            Theme.DoubleBuffer(this);
            ResizeRedraw = true;
            Height = 40;
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fg, bg;
            Theme.StatusColors(Kind, out fg, out bg);
            Theme.FillRound(g, bg, new RectangleF(0, 0, Width - 1, Height - 1), 10);
            Theme.Glyph(g, Glyph, 12, fg, new RectangleF(12, 0, 20, Height));
            TextRenderer.DrawText(g, Text, Theme.UI(10), new Rectangle(42, 0, Width - 52, Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    // Pozycja menu bocznego.
    class NavButton : Control, IThemed
    {
        public string Glyph;
        public bool Selected;
        bool hover;

        public NavButton(string glyph, string text)
        {
            Glyph = glyph;
            Text = text;
            Height = 42;
            Dock = DockStyle.Top;
            Cursor = Cursors.Hand;
            Theme.DoubleBuffer(this);
            ResizeRedraw = true;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Sidebar);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(10, 3, Width - 20, Height - 6);
            if (Selected) Theme.FillRound(g, Theme.AccentSoft, r, 8);
            else if (hover) Theme.FillRound(g, Theme.Hover, r, 8);
            if (Selected) Theme.FillRound(g, Theme.Accent, new RectangleF(10, Height / 2f - 9, 3.5f, 18), 2);
            var c = Selected ? Theme.Accent : Theme.Text;
            Theme.Glyph(g, Glyph, 12.5f, Selected ? Theme.Accent : Theme.Muted, new RectangleF(24, 0, 22, Height));
            TextRenderer.DrawText(g, Text, Selected ? Theme.Semi(10.5f) : Theme.UI(10.5f), new Rectangle(56, 0, Width - 60, Height), c,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    // Kółko z kolorem akcentu (wybór w ustawieniach).
    class Swatch : Control, IThemed
    {
        public int Index;
        public bool Selected;

        public Swatch(int index)
        {
            Index = index;
            Size = new Size(34, 34);
            Cursor = Cursors.Hand;
            Theme.DoubleBuffer(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var c = Theme.AccentPreview(Index);
            if (Selected) using (var p = new Pen(c, 2)) g.DrawEllipse(p, 1, 1, Width - 3, Height - 3);
            using (var b = new SolidBrush(c)) g.FillEllipse(b, 6, 6, Width - 13, Height - 13);
            if (Selected) Theme.Glyph(g, "", 9, Theme.Dark ? Color.Black : Color.White, new RectangleF(6, 6, Width - 13, Height - 13));
        }
    }

    // Cienka linia oddzielająca sekcje.
    class Divider : Control, IThemed
    {
        protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(Theme.Border); }
    }

    // Kolory menu przy ikonce zegara.
    class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.AccentSoft; } }
        public override Color MenuItemSelected { get { return Theme.AccentSoft; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.AccentSoft; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.AccentSoft; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Surface; } }
        public override Color CheckBackground { get { return Theme.AccentSoft; } }
        public override Color CheckSelectedBackground { get { return Theme.AccentSoft; } }
        public override Color CheckPressedBackground { get { return Theme.AccentSoft; } }
    }
}
