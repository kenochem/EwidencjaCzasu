// Ewidencja Czasu Pracy – dane, ustawienia, kalendarz, wyliczenia, raporty, kopie, PIN
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace EwidencjaCzasu
{
    static class Program
    {
        static int crashing;
        public static bool CustomDataDir, TestCrash;

        [STAThread]
        static void Main(string[] args)
        {
            bool restart = false;
            foreach (var a in args)
            {
                if (a.StartsWith("/dane:", StringComparison.OrdinalIgnoreCase)) Store.Dir = a.Substring(6);
                if (a.Equals("/restart", StringComparison.OrdinalIgnoreCase)) restart = true;
                if (a.Equals("/test-awaria", StringComparison.OrdinalIgnoreCase)) TestCrash = true;
            }
            // osobny folder danych (np. do testów) = osobna instancja, bez autostartu
            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EwidencjaCzasu");
            CustomDataDir = !string.Equals(Path.GetFullPath(Store.Dir).TrimEnd('\\'), defaultDir, StringComparison.OrdinalIgnoreCase);
            string mutexName = "EwidencjaCzasuPracy_RCP" + (CustomDataDir ? "_" + Path.GetFullPath(Store.Dir).ToLowerInvariant().GetHashCode() : "");

            // nieoczekiwany błąd: zapis do bledy.log i automatyczne ponowne uruchomienie
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Crash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Crash(e.ExceptionObject as Exception);

            bool created;
            using (var mutex = new System.Threading.Mutex(true, mutexName, out created))
            {
                if (!created && restart)
                {
                    // poprzednia instancja właśnie się zamyka – czekamy na nią
                    try { created = mutex.WaitOne(20000); }
                    catch (System.Threading.AbandonedMutexException) { created = true; }
                }
                if (!created)
                {
                    if (!restart) MessageBox.Show("Program Ewidencja Czasu już działa – ikonka jest przy zegarku.", "Ewidencja Czasu");
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { Store.Init(); }
                catch (Exception ex)
                {
                    MessageBox.Show("Nie można przygotować folderu danych:\n" + ex.Message, "Ewidencja Czasu");
                    return;
                }
                Application.Run(new TrayApp(restart));
            }
        }

        static void Crash(Exception ex)
        {
            if (System.Threading.Interlocked.Exchange(ref crashing, 1) == 1) return;
            int recent = 0;
            try
            {
                File.AppendAllText(Path.Combine(Store.Dir, "bledy.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + "\r\n\r\n", Encoding.UTF8);
                Store.Heartbeat();
                recent = Store.RegisterCrash();
            }
            catch { }
            if (recent < 3)
            {
                try { System.Diagnostics.Process.Start(Application.ExecutablePath, "/restart \"/dane:" + Store.Dir + "\""); }
                catch { }
            }
            else
            {
                MessageBox.Show("Program Ewidencja Czasu kilka razy w krótkim czasie napotkał błąd i został zamknięty.\n" +
                    "Uruchom go ponownie ręcznie. Szczegóły: plik bledy.log w folderze danych.", "Ewidencja Czasu",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            Environment.Exit(1);
        }
    }

    // ------------------------------------------------------------------ ustawienia

    static class Cfg
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static int DebounceSeconds = 60, MinDigits = 8, MaxGapMs = 50;
        public static bool AcceptInjected = false; // tylko do testów
        public static TimeSpan WorkStart = new TimeSpan(8, 0, 0), WorkEnd = new TimeSpan(16, 0, 0);
        public static double NormHours = 8;
        public static int LateToleranceMin = 5;
        public static TimeSpan NightFrom = new TimeSpan(22, 0, 0), NightTo = new TimeSpan(6, 0, 0);
        public static bool ReminderOn = true;
        public static TimeSpan ReminderAt = new TimeSpan(16, 30, 0);
        public static bool KeepAwake = true;
        public static TimeSpan AwakeFrom = new TimeSpan(7, 0, 0), AwakeTo = new TimeSpan(18, 0, 0);
        public static string BackupFolder = "";
        public static int BackupEveryDays = 7;
        public static bool AutoStart = true;
        public static string ThemeMode = "system", Accent = "niebieski";

        public static TimeSpan Norm { get { return TimeSpan.FromHours(NormHours); } }

        static readonly string[] Keys = {
            "blokada_sekund", "min_cyfr", "max_odstep_ms", "godzina_od", "godzina_do", "norma_godzin", "tolerancja_min",
            "noc_od", "noc_do", "przypomnienie", "przypomnienie_o", "nie_usypiaj", "nie_usypiaj_od", "nie_usypiaj_do",
            "folder_kopii", "kopia_co_dni", "autostart", "motyw", "akcent" };

        public static void Load(string path)
        {
            var map = new Dictionary<string, string>();
            foreach (var raw in Store.ReadLinesSmart(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                map[line.Substring(0, eq).Trim().ToLowerInvariant()] = line.Substring(eq + 1).Trim();
            }
            Int(map, "blokada_sekund", ref DebounceSeconds, 0, 3600);
            Int(map, "min_cyfr", ref MinDigits, 4, 24);
            Int(map, "max_odstep_ms", ref MaxGapMs, 10, 500);
            Time(map, "godzina_od", ref WorkStart);
            Time(map, "godzina_do", ref WorkEnd);
            string s;
            double d;
            if (map.TryGetValue("norma_godzin", out s) && double.TryParse(s.Replace(',', '.'), NumberStyles.Float, Inv, out d) && d >= 1 && d <= 16)
                NormHours = d;
            Int(map, "tolerancja_min", ref LateToleranceMin, 0, 120);
            Time(map, "noc_od", ref NightFrom);
            Time(map, "noc_do", ref NightTo);
            Bool(map, "przypomnienie", ref ReminderOn);
            Time(map, "przypomnienie_o", ref ReminderAt);
            Bool(map, "nie_usypiaj", ref KeepAwake);
            Time(map, "nie_usypiaj_od", ref AwakeFrom);
            Time(map, "nie_usypiaj_do", ref AwakeTo);
            if (map.TryGetValue("folder_kopii", out s)) BackupFolder = s;
            Int(map, "kopia_co_dni", ref BackupEveryDays, 1, 60);
            Bool(map, "autostart", ref AutoStart);
            if (map.TryGetValue("motyw", out s) && Theme.Modes.Contains(s.ToLowerInvariant())) ThemeMode = s.ToLowerInvariant();
            if (map.TryGetValue("akcent", out s) && Theme.AccentNames.Contains(s.ToLowerInvariant())) Accent = s.ToLowerInvariant();
            int test = 0;
            Int(map, "tryb_testowy", ref test, 0, 1);
            AcceptInjected = test == 1;

            // starszy plik ustawień – dopisujemy nowe opcje z wartościami domyślnymi
            if (Keys.Any(k => !map.ContainsKey(k))) Save(path);
        }

        static void Int(Dictionary<string, string> m, string k, ref int v, int min, int max)
        {
            string s; int x;
            if (m.TryGetValue(k, out s) && int.TryParse(s, out x)) v = Math.Max(min, Math.Min(max, x));
        }

        static void Bool(Dictionary<string, string> m, string k, ref bool v)
        {
            string s;
            if (m.TryGetValue(k, out s)) v = s == "1" || s.Equals("tak", StringComparison.OrdinalIgnoreCase);
        }

        static void Time(Dictionary<string, string> m, string k, ref TimeSpan v)
        {
            string s; TimeSpan t;
            if (m.TryGetValue(k, out s) && TimeSpan.TryParseExact(s, new[] { @"h\:mm", @"hh\:mm" }, Inv, out t) && t < TimeSpan.FromDays(1)) v = t;
        }

        public static string T(TimeSpan t) { return t.ToString(@"hh\:mm"); }

        public static void Save(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Ustawienia programu Ewidencja Czasu");
            sb.AppendLine("# Najwygodniej zmieniać je w programie: Panel -> Ustawienia.");
            sb.AppendLine("# Po ręcznej zmianie tego pliku uruchom program ponownie.");
            sb.AppendLine();
            sb.AppendLine("# --- czas pracy");
            sb.AppendLine("godzina_od=" + T(WorkStart));
            sb.AppendLine("godzina_do=" + T(WorkEnd));
            sb.AppendLine("norma_godzin=" + NormHours.ToString("0.##", Inv));
            sb.AppendLine("# po ilu minutach od rozpoczęcia pracy liczy się spóźnienie");
            sb.AppendLine("tolerancja_min=" + LateToleranceMin);
            sb.AppendLine("# pora nocna (Kodeks pracy: 8 kolejnych godzin między 21:00 a 7:00)");
            sb.AppendLine("noc_od=" + T(NightFrom));
            sb.AppendLine("noc_do=" + T(NightTo));
            sb.AppendLine();
            sb.AppendLine("# --- przypomnienie o odbiciu wyjścia (1 = włączone, 0 = wyłączone)");
            sb.AppendLine("przypomnienie=" + (ReminderOn ? 1 : 0));
            sb.AppendLine("przypomnienie_o=" + T(ReminderAt));
            sb.AppendLine();
            sb.AppendLine("# --- komputer nie usypia w dni robocze w tych godzinach");
            sb.AppendLine("nie_usypiaj=" + (KeepAwake ? 1 : 0));
            sb.AppendLine("nie_usypiaj_od=" + T(AwakeFrom));
            sb.AppendLine("nie_usypiaj_do=" + T(AwakeTo));
            sb.AppendLine();
            sb.AppendLine("# --- kopia zapasowa (np. folder w Dysku Google: G:\\Mój dysk\\EwidencjaCzasu-kopie)");
            sb.AppendLine("folder_kopii=" + BackupFolder);
            sb.AppendLine("kopia_co_dni=" + BackupEveryDays);
            sb.AppendLine();
            sb.AppendLine("# --- wygląd: motyw = system / jasny / ciemny; akcent = " + string.Join(" / ", Theme.AccentNames));
            sb.AppendLine("motyw=" + ThemeMode);
            sb.AppendLine("akcent=" + Accent);
            sb.AppendLine();
            sb.AppendLine("# --- uruchamianie razem z Windows (1 = tak; program sam pilnuje wpisu, także po przeniesieniu pliku .exe)");
            sb.AppendLine("autostart=" + (AutoStart ? 1 : 0));
            sb.AppendLine();
            sb.AppendLine("# --- czytnik kart");
            sb.AppendLine("# przez ile sekund ignorować ponowne przyłożenie tej samej karty");
            sb.AppendLine("blokada_sekund=" + DebounceSeconds);
            sb.AppendLine("# minimalna liczba cyfr numeru karty");
            sb.AppendLine("min_cyfr=" + MinDigits);
            sb.AppendLine("# maks. odstęp (ms) między znakami z czytnika; jeśli karta czasem wpisuje się do okna – zwiększ np. do 80");
            sb.AppendLine("max_odstep_ms=" + MaxGapMs);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }
    }

    // ------------------------------------------------------------------ dane

    class Employee
    {
        public string Uid, Name;
        public bool Active;
        public int LeaveDays = 26, LeaveCarry;
        public string Contract = "UOP";   // forma zatrudnienia (Contracts.Codes)
        public double Etat = 1;           // wymiar etatu – tylko umowa o pracę
        public bool HasNorm { get { return Contract == "UOP"; } }
    }

    // Formy zatrudnienia. Tylko umowa o pracę ma normę, nadgodziny, urlopy i L4 –
    // przy pozostałych liczy się po prostu przepracowany czas.
    static class Contracts
    {
        public static readonly string[] Codes = { "UOP", "ZLECENIE", "DZIELO", "B2B" };
        public static readonly string[] Names = { "Umowa o pracę", "Umowa zlecenie", "Umowa o dzieło", "B2B / działalność" };
        public static readonly double[] Etaty = { 1, 0.875, 0.8, 0.75, 0.5, 0.25 };
        public static readonly string[] EtatNames = { "pełny etat", "7/8 etatu", "4/5 etatu", "3/4 etatu", "1/2 etatu", "1/4 etatu" };

        public static string Name(string code)
        {
            int i = Array.IndexOf(Codes, code);
            return i < 0 ? Names[0] : Names[i];
        }

        public static string Code(string name)
        {
            int i = Array.IndexOf(Names, name);
            return i < 0 ? "UOP" : Codes[i];
        }

        public static string EtatName(double e)
        {
            for (int i = 0; i < Etaty.Length; i++) if (Math.Abs(Etaty[i] - e) < 0.001) return EtatNames[i];
            return e.ToString("0.###", CultureInfo.InvariantCulture) + " etatu";
        }

        public static double EtatOf(string name)
        {
            int i = Array.IndexOf(EtatNames, name);
            return i < 0 ? 1 : Etaty[i];
        }

        public static string Short(string code)
        {
            return code == "ZLECENIE" ? "zlecenie" : code == "DZIELO" ? "dzieło" : code == "B2B" ? "B2B" : "umowa o pracę";
        }
    }
    class Punch { public DateTime Time; public string Uid; public string Name; public string Type; public string Source; }
    class Absence { public DateTime Day; public string Uid; public string Name; public string Code; public string Note = ""; public bool Company; }

    // Dzień wolny dla całej firmy: DW = płatny dzień wolny (zalicza normę),
    // WS = dzień wolny za święto przypadające w sobotę (wymiar jest już obniżony).
    class CompanyDay { public DateTime Day; public string Code = "DW"; public string Name = ""; }

    static class Store
    {
        public const string IN = "WEJŚCIE", OUT = "WYJŚCIE";
        public static string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EwidencjaCzasu");
        public static string EmpFile { get { return Path.Combine(Dir, "pracownicy.csv"); } }
        public static string PunchFile { get { return Path.Combine(Dir, "odbicia.csv"); } }
        public static string PendingFile { get { return Path.Combine(Dir, "oczekujace.csv"); } }
        public static string AbsFile { get { return Path.Combine(Dir, "nieobecnosci.csv"); } }
        public static string CompanyFile { get { return Path.Combine(Dir, "dni_wolne_firmowe.csv"); } }
        public static string CfgFile { get { return Path.Combine(Dir, "ustawienia.txt"); } }
        public static string AuditFile { get { return Path.Combine(Dir, "historia_zmian.csv"); } }
        public static string HeartbeatFile { get { return Path.Combine(Dir, "ostatnio_aktywny.txt"); } }
        public static string DowntimeFile { get { return Path.Combine(Dir, "przerwy_w_dzialaniu.csv"); } }
        public static string ReportDir { get { return Path.Combine(Dir, "raporty"); } }
        public static string BackupDir { get { return Path.Combine(Dir, "kopie"); } }

        public static readonly Encoding Bom = new UTF8Encoding(true);
        static readonly Encoding NoBom = new UTF8Encoding(false);
        static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const string EmpHeader = "UID;Imię i nazwisko;Aktywny;Urlop roczny (dni);Urlop zaległy (dni);Forma zatrudnienia;Etat";
        const string PunchHeader = "Data;Godzina;UID;Pracownik;Typ;Źródło";
        const string AbsHeader = "Data;UID;Pracownik;Rodzaj;Uwagi";

        public static void Init()
        {
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(ReportDir);
            Directory.CreateDirectory(BackupDir);
            if (!File.Exists(EmpFile)) File.WriteAllText(EmpFile, EmpHeader + "\r\n", Bom);
            if (!File.Exists(PunchFile)) File.WriteAllText(PunchFile, PunchHeader + "\r\n", Bom);
            if (!File.Exists(AbsFile)) File.WriteAllText(AbsFile, AbsHeader + "\r\n", Bom);
            Cfg.Load(CfgFile);
            Theme.Load();
            DailyBackup();
        }

        public static void DailyBackup()
        {
            try
            {
                string stamp = DateTime.Today.ToString("yyyy-MM-dd");
                foreach (var f in new[] { PunchFile, EmpFile, AbsFile, CompanyFile })
                {
                    if (!File.Exists(f)) continue;
                    string dst = Path.Combine(BackupDir, Path.GetFileNameWithoutExtension(f) + "_" + stamp + ".csv");
                    if (!File.Exists(dst)) File.WriteAllBytes(dst, ReadBytesShared(f));
                }
                // kopie starsze niż 60 dni są zbędne – główny plik i tak zawiera całą historię
                foreach (var f in Directory.GetFiles(BackupDir, "*_????-??-??.csv"))
                    if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-60)) File.Delete(f);
            }
            catch { }
        }

        public static byte[] ReadBytesShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                return ms.ToArray();
            }
        }

        // Excel potrafi zapisać CSV w kodowaniu Windows-1250 – wykrywamy to.
        static Encoding DetectEncoding(byte[] data)
        {
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) return Encoding.UTF8;
            try { StrictUtf8.GetString(data); return Encoding.UTF8; }
            catch (DecoderFallbackException) { return Encoding.GetEncoding(1250); }
        }

        public static List<string> ReadLinesSmart(string path)
        {
            var result = new List<string>();
            if (!File.Exists(path)) return result;
            var data = ReadBytesShared(path);
            string text = DetectEncoding(data).GetString(data).TrimStart('\uFEFF');
            foreach (var l in text.Split('\n')) result.Add(l.TrimEnd('\r'));
            return result;
        }

        public static string Norm(string uid)
        {
            var t = (uid ?? "").Trim().TrimStart('0');
            return t.Length == 0 ? "0" : t;
        }

        public static string Clean(string s)
        {
            return (s ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        static void WriteLocked(string path, string text, string friendly)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, Bom);
            try { File.Copy(tmp, path, true); }
            catch (IOException) { throw new IOException("Plik " + friendly + " jest otwarty w innym programie (np. Excelu). Zamknij go i spróbuj ponownie."); }
            finally { File.Delete(tmp); }
        }

        // ---- pracownicy

        public static List<Employee> LoadEmployees()
        {
            var list = new List<Employee>();
            var lines = ReadLinesSmart(EmpFile);
            for (int i = 1; i < lines.Count; i++)
            {
                var p = lines[i].Split(';');
                if (p.Length < 2 || p[0].Trim() == "") continue;
                var e = new Employee
                {
                    Uid = p[0].Trim(),
                    Name = p[1].Trim(),
                    Active = p.Length < 3 || !p[2].Trim().Equals("nie", StringComparison.OrdinalIgnoreCase)
                };
                int x;
                if (p.Length > 3 && int.TryParse(p[3].Trim(), out x)) e.LeaveDays = x;
                if (p.Length > 4 && int.TryParse(p[4].Trim(), out x)) e.LeaveCarry = x;
                if (p.Length > 5 && Contracts.Codes.Contains(p[5].Trim().ToUpperInvariant())) e.Contract = p[5].Trim().ToUpperInvariant();
                double et;
                if (p.Length > 6 && double.TryParse(p[6].Trim().Replace(',', '.'), NumberStyles.Float, Inv, out et) && et > 0 && et <= 1) e.Etat = et;
                list.Add(e);
            }
            return list;
        }

        public static void SaveEmployees(List<Employee> list)
        {
            var sb = new StringBuilder(EmpHeader + "\r\n");
            foreach (var e in list)
                sb.AppendFormat("{0};{1};{2};{3};{4};{5};{6}\r\n", Clean(e.Uid), Clean(e.Name), e.Active ? "tak" : "nie", e.LeaveDays, e.LeaveCarry,
                    e.Contract, e.Etat.ToString("0.###", Inv));
            WriteLocked(EmpFile, sb.ToString(), "pracownicy.csv");
        }

        public static Employee FindEmployee(List<Employee> list, string uid)
        {
            string n = Norm(uid);
            return list.FirstOrDefault(e => Norm(e.Uid) == n);
        }

        // ---- odbicia

        static readonly string[] DateFormats = { "yyyy-MM-dd", "dd.MM.yyyy", "d.MM.yyyy", "d.M.yyyy", "dd-MM-yyyy", "yyyy.MM.dd" };
        static readonly string[] TimeFormats = { "HH:mm:ss", "H:mm:ss", "HH:mm", "H:mm" };

        static bool ParseDate(string s, out DateTime d)
        {
            return DateTime.TryParseExact(s.Trim(), DateFormats, Inv, DateTimeStyles.None, out d);
        }

        static Punch ParsePunch(string line)
        {
            var p = line.Split(';');
            if (p.Length < 5) return null;
            DateTime d, t;
            if (!ParseDate(p[0], out d)) return null;
            if (!DateTime.TryParseExact(p[1].Trim(), TimeFormats, Inv, DateTimeStyles.None, out t)) return null;
            string type = p[4].Trim().ToUpperInvariant();
            if (type.StartsWith("WE")) type = IN;
            else if (type.StartsWith("WY")) type = OUT;
            else return null;
            return new Punch
            {
                Time = d.Date + t.TimeOfDay,
                Uid = p[2].Trim(),
                Name = p[3].Trim(),
                Type = type,
                Source = p.Length > 5 ? p[5].Trim() : ""
            };
        }

        static string FormatPunch(Punch p)
        {
            return string.Format("{0};{1};{2};{3};{4};{5}\r\n",
                p.Time.ToString("yyyy-MM-dd", Inv), p.Time.ToString("HH:mm:ss", Inv),
                Clean(p.Uid), Clean(p.Name), p.Type, Clean(p.Source));
        }

        public static List<Punch> LoadPunches()
        {
            var list = new List<Punch>();
            foreach (var file in new[] { PunchFile, PendingFile })
                foreach (var l in ReadLinesSmart(file))
                {
                    var p = ParsePunch(l);
                    if (p != null) list.Add(p);
                }
            return list.OrderBy(p => p.Time).ToList();
        }

        // true = zapisano do odbicia.csv; false = plik zablokowany (np. otwarty w Excelu),
        // odbicie zapisane tymczasowo w oczekujace.csv i zostanie przeniesione przy następnym zapisie.
        public static bool AppendPunch(Punch p)
        {
            string line = FormatPunch(p);
            try
            {
                var pendingLines = ReadLinesSmart(PendingFile).Where(x => x.Trim() != "").ToList();
                string text = string.Join("", pendingLines.Select(x => x + "\r\n")) + line;

                byte[] existing = ReadBytesShared(PunchFile);
                Encoding enc = DetectEncoding(existing) == Encoding.UTF8 ? NoBom : Encoding.GetEncoding(1250);
                if (existing.Length > 0 && existing[existing.Length - 1] != (byte)'\n') text = "\r\n" + text;

                using (var fs = new FileStream(PunchFile, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    var bytes = enc.GetBytes(text);
                    fs.Write(bytes, 0, bytes.Length);
                }
                if (pendingLines.Count > 0) File.Delete(PendingFile);
                return true;
            }
            catch (IOException)
            {
                File.AppendAllText(PendingFile, line, NoBom);
                return false;
            }
        }

        public static void SavePunches(List<Punch> list)
        {
            var sb = new StringBuilder(PunchHeader + "\r\n");
            foreach (var p in list.OrderBy(x => x.Time)) sb.Append(FormatPunch(p));
            WriteLocked(PunchFile, sb.ToString(), "odbicia.csv");
            if (File.Exists(PendingFile)) File.Delete(PendingFile);
        }

        static bool Same(Punch a, Punch b)
        {
            return a.Time == b.Time && Norm(a.Uid) == Norm(b.Uid) && a.Type == b.Type;
        }

        // updated == null oznacza usunięcie odbicia
        public static void ChangePunch(Punch old, Punch updated)
        {
            var all = LoadPunches();
            int i = all.FindIndex(p => Same(p, old));
            if (i < 0) throw new InvalidOperationException("Nie znaleziono tego odbicia – odśwież listę.");
            if (updated == null) all.RemoveAt(i); else all[i] = updated;
            SavePunches(all);
            Audit(updated == null ? "USUNIĘCIE ODBICIA" : "POPRAWKA ODBICIA", Describe(old), Describe(updated));
        }

        // ---- nieobecności

        public static List<Absence> LoadAbsences()
        {
            var list = new List<Absence>();
            var lines = ReadLinesSmart(AbsFile);
            for (int i = 1; i < lines.Count; i++)
            {
                var p = lines[i].Split(';');
                DateTime d;
                if (p.Length < 4 || !ParseDate(p[0], out d)) continue;
                var type = AbsTypes.Find(p[3].Trim());
                if (type == null) continue;
                list.Add(new Absence { Day = d.Date, Uid = p[1].Trim(), Name = p[2].Trim(), Code = type.Code, Note = p.Length > 4 ? p[4].Trim() : "" });
            }
            return list.OrderBy(a => a.Day).ToList();
        }

        static void SaveAbsences(List<Absence> list)
        {
            var sb = new StringBuilder(AbsHeader + "\r\n");
            foreach (var a in list.OrderBy(x => x.Day).ThenBy(x => x.Name))
                sb.AppendFormat("{0};{1};{2};{3};{4}\r\n", a.Day.ToString("yyyy-MM-dd", Inv), Clean(a.Uid), Clean(a.Name), a.Code, Clean(a.Note));
            WriteLocked(AbsFile, sb.ToString(), "nieobecnosci.csv");
        }

        // Dodaje nieobecności; istniejący wpis tej samej osoby z tego samego dnia jest zastępowany.
        public static void AddAbsences(List<Absence> add)
        {
            var all = LoadAbsences();
            foreach (var a in add)
            {
                var old = all.FirstOrDefault(x => x.Day == a.Day && Norm(x.Uid) == Norm(a.Uid));
                if (old != null) all.Remove(old);
                all.Add(a);
            }
            SaveAbsences(all);
            foreach (var a in add) Audit("NIEOBECNOŚĆ DODANA", "", Describe(a));
        }

        public static void RemoveAbsences(List<Absence> remove)
        {
            var all = LoadAbsences();
            int n = all.RemoveAll(x => remove.Any(r => r.Day == x.Day && Norm(r.Uid) == Norm(x.Uid) && r.Code == x.Code));
            if (n == 0) throw new InvalidOperationException("Nie znaleziono tych wpisów – odśwież listę.");
            SaveAbsences(all);
            foreach (var a in remove) Audit("NIEOBECNOŚĆ USUNIĘTA", Describe(a), "");
        }

        // ---- dni wolne firmowe

        public static List<CompanyDay> LoadCompanyDays()
        {
            var list = new List<CompanyDay>();
            var lines = ReadLinesSmart(CompanyFile);
            for (int i = 1; i < lines.Count; i++)
            {
                var p = lines[i].Split(';');
                DateTime d;
                if (p.Length < 2 || !ParseDate(p[0], out d)) continue;
                string code = p[1].Trim().ToUpperInvariant() == "WS" ? "WS" : "DW";
                list.Add(new CompanyDay { Day = d.Date, Code = code, Name = p.Length > 2 ? p[2].Trim() : "" });
            }
            return list.OrderBy(c => c.Day).ToList();
        }

        public static void SaveCompanyDays(List<CompanyDay> list)
        {
            var sb = new StringBuilder("Data;Rodzaj;Nazwa\r\n");
            foreach (var c in list.OrderBy(x => x.Day))
                sb.AppendFormat("{0};{1};{2}\r\n", c.Day.ToString("yyyy-MM-dd", Inv), c.Code, Clean(c.Name));
            WriteLocked(CompanyFile, sb.ToString(), "dni_wolne_firmowe.csv");
        }

        public static bool IsCompanyDayOff(DateTime day)
        {
            try { return LoadCompanyDays().Any(c => c.Day == day.Date); }
            catch { return false; }
        }

        // ---- ciągłość działania: znacznik "żyję" co 30 s pozwala wykryć, kiedy program nie działał
        // (restart, wyłączenie, uśpienie komputera) i ostrzec, że odbicia z tego czasu mogą brakować.

        public static void Heartbeat()
        {
            File.WriteAllText(HeartbeatFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv));
        }

        public static DateTime? LastHeartbeat()
        {
            try
            {
                DateTime d;
                if (File.Exists(HeartbeatFile) && DateTime.TryParseExact(File.ReadAllText(HeartbeatFile).Trim(), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, out d))
                    return d;
            }
            catch { }
            return null;
        }

        public static void LogDowntime(DateTime from, DateTime to)
        {
            if (!File.Exists(DowntimeFile)) File.WriteAllText(DowntimeFile, "Od;Do;Minuty\r\n", Bom);
            File.AppendAllText(DowntimeFile, string.Format("{0};{1};{2}\r\n",
                from.ToString("yyyy-MM-dd HH:mm", Inv), to.ToString("yyyy-MM-dd HH:mm", Inv), (int)(to - from).TotalMinutes), NoBom);
        }

        public static List<KeyValuePair<DateTime, DateTime>> Downtimes(DateTime day)
        {
            var result = new List<KeyValuePair<DateTime, DateTime>>();
            foreach (var l in ReadLinesSmart(DowntimeFile))
            {
                var p = l.Split(';');
                DateTime a, b;
                if (p.Length < 2 ||
                    !DateTime.TryParseExact(p[0], "yyyy-MM-dd HH:mm", Inv, DateTimeStyles.None, out a) ||
                    !DateTime.TryParseExact(p[1], "yyyy-MM-dd HH:mm", Inv, DateTimeStyles.None, out b)) continue;
                if (a < day.Date.AddDays(1) && b > day.Date) result.Add(new KeyValuePair<DateTime, DateTime>(a, b));
            }
            return result;
        }

        // Część przerwy, która wypadła w godzinach pracy (od 15 min przed rozpoczęciem do 30 min po zakończeniu).
        public static TimeSpan DowntimeInWorkHours(DateTime from, DateTime to)
        {
            var total = TimeSpan.Zero;
            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                if (!PlCalendar.IsWorkingDay(d)) continue;
                var ws = d + Cfg.WorkStart - TimeSpan.FromMinutes(15);
                var we = d + Cfg.WorkEnd + TimeSpan.FromMinutes(30);
                var s = from > ws ? from : ws;
                var e = to < we ? to : we;
                if (e > s) total += e - s;
            }
            return total;
        }

        // Zwraca liczbę awarii w ostatnich 10 minutach (ochrona przed pętlą restartów).
        public static int RegisterCrash()
        {
            string path = Path.Combine(Dir, "awarie.txt");
            var now = DateTime.Now;
            var times = new List<DateTime>();
            if (File.Exists(path))
                foreach (var l in File.ReadAllLines(path))
                {
                    DateTime d;
                    if (DateTime.TryParseExact(l.Trim(), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, out d) && (now - d).TotalMinutes < 10) times.Add(d);
                }
            times.Add(now);
            File.WriteAllLines(path, times.Select(t => t.ToString("yyyy-MM-dd HH:mm:ss", Inv)));
            return times.Count - 1;
        }

        // ---- historia zmian: kto, kiedy, co było, co jest

        public static void Audit(string operation, string before, string after)
        {
            try
            {
                if (!File.Exists(AuditFile)) File.WriteAllText(AuditFile, "Kiedy;Operacja;Przed;Po;Użytkownik Windows\r\n", Bom);
                File.AppendAllText(AuditFile, string.Format("{0:yyyy-MM-dd HH:mm:ss};{1};{2};{3};{4}\r\n",
                    DateTime.Now, operation, Clean(before), Clean(after), Environment.UserName), NoBom);
            }
            catch { }
        }

        public static string Describe(Punch p)
        {
            return p == null ? "" : string.Format("{0} {1} {2} {3}", p.Name, p.Time.ToString("yyyy-MM-dd HH:mm:ss", Inv), p.Type, p.Source);
        }

        public static string Describe(Absence a)
        {
            return a == null ? "" : string.Format("{0} {1} {2} {3}", a.Name, a.Day.ToString("yyyy-MM-dd", Inv), a.Code, a.Note).Trim();
        }
    }

    // ------------------------------------------------------------------ rodzaje nieobecności

    class AbsType
    {
        public readonly string Code, Name;
        // 0 = praca poza biurem (zalicza się do przepracowanych),
        // 1 = usprawiedliwiona (obniża wymiar czasu pracy),
        // 2 = dzień wolny bez obniżenia wymiaru (np. odbiór nadgodzin),
        // 3 = nieusprawiedliwiona
        public readonly int Kind;
        public AbsType(string code, string name, int kind) { Code = code; Name = name; Kind = kind; }
        public override string ToString() { return Code + " – " + Name; }
    }

    static class AbsTypes
    {
        public static readonly AbsType[] All =
        {
            new AbsType("UW", "Urlop wypoczynkowy", 1),
            new AbsType("UŻ", "Urlop na żądanie", 1),
            new AbsType("CH", "Choroba (L4)", 1),
            new AbsType("OP", "Opieka nad dzieckiem (art. 188)", 1),
            new AbsType("UO", "Urlop okolicznościowy", 1),
            new AbsType("UB", "Urlop bezpłatny", 1),
            new AbsType("NU", "Inna nieobecność usprawiedliwiona", 1),
            new AbsType("PZ", "Praca zdalna", 0),
            new AbsType("DL", "Delegacja / praca poza biurem", 0),
            new AbsType("WS", "Dzień wolny za święto w sobotę", 2),
            new AbsType("DW", "Dzień wolny firmowy (płatny)", 1),
            new AbsType("ON", "Odbiór nadgodzin", 2),
            new AbsType("NN", "Nieobecność nieusprawiedliwiona", 3),
        };

        public static AbsType Find(string code)
        {
            return All.FirstOrDefault(t => t.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ------------------------------------------------------------------ kalendarz (święta w Polsce)

    static class PlCalendar
    {
        static readonly Dictionary<int, Dictionary<DateTime, string>> cache = new Dictionary<int, Dictionary<DateTime, string>>();

        public static DateTime Easter(int y)
        {
            int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
            return new DateTime(y, month, day);
        }

        public static Dictionary<DateTime, string> Holidays(int y)
        {
            Dictionary<DateTime, string> h;
            if (cache.TryGetValue(y, out h)) return h;
            var e = Easter(y);
            h = new Dictionary<DateTime, string>
            {
                { new DateTime(y, 1, 1), "Nowy Rok" },
                { new DateTime(y, 1, 6), "Trzech Króli" },
                { e, "Wielkanoc" },
                { e.AddDays(1), "Poniedziałek Wielkanocny" },
                { new DateTime(y, 5, 1), "Święto Pracy" },
                { new DateTime(y, 5, 3), "Święto Konstytucji 3 Maja" },
                { e.AddDays(49), "Zielone Świątki" },
                { e.AddDays(60), "Boże Ciało" },
                { new DateTime(y, 8, 15), "Wniebowzięcie NMP" },
                { new DateTime(y, 11, 1), "Wszystkich Świętych" },
                { new DateTime(y, 11, 11), "Święto Niepodległości" },
                { new DateTime(y, 12, 25), "Boże Narodzenie" },
                { new DateTime(y, 12, 26), "Drugi dzień Bożego Narodzenia" },
            };
            if (y >= 2025) h[new DateTime(y, 12, 24)] = "Wigilia";
            cache[y] = h;
            return h;
        }

        public static string Holiday(DateTime d)
        {
            string n;
            return Holidays(d.Year).TryGetValue(d.Date, out n) ? n : null;
        }

        public static bool IsWorkingDay(DateTime d)
        {
            return d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && Holiday(d) == null;
        }

        // Wymiar czasu pracy (art. 130 KP): norma × dni pon–pt, minus norma za każde święto
        // przypadające w dniu innym niż niedziela (także w sobotę).
        public static TimeSpan NormBetween(DateTime from, DateTime to, double etat = 1)
        {
            int days = 0;
            for (var d = from.Date; d < to.Date; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) days++;
                if (d.DayOfWeek != DayOfWeek.Sunday && Holiday(d) != null) days--;
            }
            return TimeSpan.FromTicks((long)(Cfg.Norm.Ticks * days * etat));
        }

        public static TimeSpan MonthNorm(int y, int m)
        {
            var from = new DateTime(y, m, 1);
            return NormBetween(from, from.AddMonths(1));
        }
    }

    // ------------------------------------------------------------------ wyliczenia

    class WorkRow
    {
        public DateTime? In, Out;
        public string Note = "";
        public TimeSpan Duration { get { return In.HasValue && Out.HasValue ? Out.Value - In.Value : TimeSpan.Zero; } }
    }

    // Wszystko, co wiadomo o jednym dniu jednej osoby.
    class DayInfo
    {
        public string Uid, Name;
        public DateTime Day;
        public List<WorkRow> Rows = new List<WorkRow>();
        public TimeSpan Worked;          // z odbić (zamknięte odcinki)
        public DateTime? OpenSince;      // dziś nadal w biurze od tej godziny
        public Absence Absence;
        public AbsType Abs;
        public bool WorkingDay;
        public string Holiday;
        public TimeSpan Remote;          // praca zdalna / delegacja zaliczona do przepracowanych
        public TimeSpan Excused;         // nieobecność usprawiedliwiona (obniża wymiar)
        public TimeSpan Overtime, FreeDayWork, Night;
        public int LateMin, EarlyMin;
        public bool MissingPunch, NoShow;
        public DateTime? FirstIn, LastOut;
        public bool HasNorm = true;       // umowa o pracę: norma, nadgodziny, spóźnienia, nieobecności
        public string Contract = "UOP";
        public TimeSpan DayNorm;          // norma dobowa tej osoby (z uwzględnieniem etatu)

        public TimeSpan Total { get { return Worked + Remote; } }
        public bool Problem { get { return MissingPunch || NoShow || (Abs != null && Abs.Kind == 3); } }

        public string Notes()
        {
            var parts = new List<string>();
            if (Holiday != null) parts.Add(Holiday);
            else if (!WorkingDay) parts.Add("dzień wolny");
            if (Abs != null) parts.Add(Abs.Name + (Absence.Note != "" ? " – " + Absence.Note : ""));
            if (NoShow) parts.Add("BRAK OBECNOŚCI – brak odbić i nieobecności");
            if (LateMin > 0) parts.Add("spóźnienie " + LateMin + " min");
            if (EarlyMin > 0) parts.Add("wcześniejsze wyjście " + EarlyMin + " min");
            if (!WorkingDay && Worked > TimeSpan.Zero) parts.Add("praca w dzień wolny");
            return string.Join(", ", parts);
        }
    }

    class Data
    {
        public List<Employee> Emps;
        public List<Punch> Punches;
        public List<Absence> Absences;
        public List<CompanyDay> CompanyDays = new List<CompanyDay>();
        readonly Dictionary<DateTime, CompanyDay> companyByDay = new Dictionary<DateTime, CompanyDay>();
        readonly Dictionary<string, List<Punch>> byDay = new Dictionary<string, List<Punch>>();
        readonly Dictionary<string, Absence> absByDay = new Dictionary<string, Absence>();
        readonly Dictionary<string, DateTime> firstSeen = new Dictionary<string, DateTime>();
        readonly Dictionary<string, string> lastName = new Dictionary<string, string>();
        static readonly List<Punch> Empty = new List<Punch>();

        public static Data Load()
        {
            var d = new Data { Emps = Store.LoadEmployees(), Punches = Store.LoadPunches(), Absences = Store.LoadAbsences(), CompanyDays = Store.LoadCompanyDays() };
            d.Index();
            return d;
        }

        static string Key(string uid, DateTime day) { return Store.Norm(uid) + "|" + day.ToString("yyyyMMdd"); }

        void Index()
        {
            foreach (var c in CompanyDays) companyByDay[c.Day] = c;
            foreach (var p in Punches)
            {
                string k = Key(p.Uid, p.Time.Date);
                List<Punch> l;
                if (!byDay.TryGetValue(k, out l)) byDay[k] = l = new List<Punch>();
                l.Add(p);
                Seen(p.Uid, p.Time.Date);
                if (p.Name != "") lastName[Store.Norm(p.Uid)] = p.Name;
            }
            foreach (var a in Absences)
            {
                absByDay[Key(a.Uid, a.Day)] = a;
                Seen(a.Uid, a.Day);
                if (a.Name != "" && !lastName.ContainsKey(Store.Norm(a.Uid))) lastName[Store.Norm(a.Uid)] = a.Name;
            }
        }

        void Seen(string uid, DateTime day)
        {
            string n = Store.Norm(uid);
            DateTime f;
            if (!firstSeen.TryGetValue(n, out f) || day < f) firstSeen[n] = day;
        }

        public List<Punch> DayPunches(string uid, DateTime day)
        {
            List<Punch> l;
            return byDay.TryGetValue(Key(uid, day), out l) ? l : Empty;
        }

        public CompanyDay CompanyDayOn(DateTime day)
        {
            CompanyDay c;
            return companyByDay.TryGetValue(day.Date, out c) ? c : null;
        }

        // Nieobecność danej osoby w danym dniu. Dzień wolny firmowy działa jak nieobecność
        // dla wszystkich na umowie o pracę (od ich pierwszego dnia w ewidencji).
        public Absence AbsenceOn(string uid, DateTime day)
        {
            Absence a;
            if (absByDay.TryGetValue(Key(uid, day), out a)) return a;
            var c = CompanyDayOn(day);
            if (c == null || !HasNorm(uid)) return null;
            var first = FirstSeen(uid);
            if (!first.HasValue || day.Date < first.Value) return null;
            return new Absence { Day = day.Date, Uid = uid, Name = NameOf(uid), Code = c.Code, Note = c.Name, Company = true };
        }

        public DateTime? FirstSeen(string uid)
        {
            DateTime f;
            return firstSeen.TryGetValue(Store.Norm(uid), out f) ? (DateTime?)f : null;
        }

        public Employee Emp(string uid) { return Store.FindEmployee(Emps, uid); }

        public bool HasNorm(string uid) { var e = Emp(uid); return e == null || e.HasNorm; }
        public string ContractOf(string uid) { var e = Emp(uid); return e == null ? "UOP" : e.Contract; }
        public double EtatOf(string uid) { var e = Emp(uid); return e != null && e.HasNorm ? e.Etat : 1; }

        public string NameOf(string uid)
        {
            var e = Emp(uid);
            if (e != null) return e.Name;
            string n;
            return lastName.TryGetValue(Store.Norm(uid), out n) ? n : "(nieznana karta " + uid + ")";
        }

        // pracownicy z listy + karty, które mają odbicia, a nie ma ich już na liście
        public List<string> AllUids()
        {
            var seen = new HashSet<string>();
            var result = new List<string>();
            foreach (var e in Emps) if (seen.Add(Store.Norm(e.Uid))) result.Add(e.Uid);
            foreach (var p in Punches) if (seen.Add(Store.Norm(p.Uid))) result.Add(p.Uid);
            foreach (var a in Absences) if (seen.Add(Store.Norm(a.Uid))) result.Add(a.Uid);
            return result;
        }
    }

    class Summary
    {
        public string Uid, Name;
        public bool HasNorm = true;
        public string Contract = "UOP";
        public TimeSpan Norm, NormToDate, Worked, Remote, Excused, Overtime, FreeDayWork, Night;
        public int Days, Late, LateMinutes, Early, Missing, NoShow;
        public readonly Dictionary<string, int> AbsDays = new Dictionary<string, int>();
        public double ArriveSum, LeaveSum;
        public int ArriveCount, LeaveCount;

        public TimeSpan Total { get { return Worked + Remote; } }
        // saldo = przepracowane + nieobecności usprawiedliwione – wymiar (do dziś)
        public TimeSpan Balance { get { return Worked + Remote + Excused - NormToDate; } }
        public int Abs(string code) { int n; return AbsDays.TryGetValue(code, out n) ? n : 0; }
        public string AvgArrive { get { return ArriveCount == 0 ? "" : Calc.Clock(ArriveSum / ArriveCount); } }
        public string AvgLeave { get { return LeaveCount == 0 ? "" : Calc.Clock(LeaveSum / LeaveCount); } }
    }

    class LeaveInfo
    {
        public bool HasLeave = true;      // urlop przysługuje tylko na umowie o pracę
        public int Entitled, Carry, UW, UZ, Sick;
        public int Used { get { return UW + UZ; } }
        public int Remaining { get { return Entitled + Carry - Used; } }
    }

    static class Calc
    {
        // Łączy odbicia jednej osoby z jednego dnia w pary wejście–wyjście.
        public static List<WorkRow> PairDay(List<Punch> dayPunches, DateTime day)
        {
            var rows = new List<WorkRow>();
            WorkRow open = null;
            foreach (var p in dayPunches.OrderBy(x => x.Time))
            {
                if (p.Type == Store.IN)
                {
                    if (open != null) { open.Note = "BRAK WYJŚCIA"; rows.Add(open); }
                    open = new WorkRow { In = p.Time };
                }
                else
                {
                    if (open != null) { open.Out = p.Time; rows.Add(open); open = null; }
                    else rows.Add(new WorkRow { Out = p.Time, Note = "BRAK WEJŚCIA" });
                }
                if (p.Source != "KARTA" && p.Source != "")
                {
                    var r = open ?? rows[rows.Count - 1];
                    if (!r.Note.Contains("korekta")) r.Note = (r.Note + " korekta ręczna").Trim();
                }
            }
            if (open != null)
            {
                open.Note = ((day.Date == DateTime.Today ? "w pracy " : "BRAK WYJŚCIA ") + open.Note).Trim();
                rows.Add(open);
            }
            return rows;
        }

        static TimeSpan Overlap(DateTime a1, DateTime a2, DateTime b1, DateTime b2)
        {
            var s = a1 > b1 ? a1 : b1;
            var e = a2 < b2 ? a2 : b2;
            return e > s ? e - s : TimeSpan.Zero;
        }

        static TimeSpan NightPart(WorkRow r, DateTime day)
        {
            if (!r.In.HasValue || !r.Out.HasValue) return TimeSpan.Zero;
            var d = day.Date;
            if (Cfg.NightFrom > Cfg.NightTo)
                return Overlap(r.In.Value, r.Out.Value, d, d + Cfg.NightTo) + Overlap(r.In.Value, r.Out.Value, d + Cfg.NightFrom, d.AddDays(1));
            return Overlap(r.In.Value, r.Out.Value, d + Cfg.NightFrom, d + Cfg.NightTo);
        }

        public static DayInfo Day(Data data, string uid, DateTime day)
        {
            var info = new DayInfo
            {
                Uid = uid, Name = data.NameOf(uid), Day = day.Date,
                WorkingDay = PlCalendar.IsWorkingDay(day), Holiday = PlCalendar.Holiday(day)
            };
            var emp = data.Emp(uid);
            info.HasNorm = emp == null || emp.HasNorm;
            info.Contract = emp != null ? emp.Contract : "UOP";
            var norm = TimeSpan.FromTicks((long)(Cfg.Norm.Ticks * data.EtatOf(uid)));
            info.DayNorm = info.HasNorm ? norm : TimeSpan.Zero;
            var punches = data.DayPunches(uid, day);
            if (punches.Count > 0)
            {
                info.Rows = PairDay(punches, day);
                foreach (var r in info.Rows)
                {
                    info.Worked += r.Duration;
                    info.Night += NightPart(r, day);
                    if (r.Note.Contains("BRAK")) info.MissingPunch = true;
                }
                var last = info.Rows[info.Rows.Count - 1];
                if (last.In.HasValue && !last.Out.HasValue && day.Date == DateTime.Today) info.OpenSince = last.In;
                var firstIn = punches.FirstOrDefault(p => p.Type == Store.IN);
                if (firstIn != null) info.FirstIn = firstIn.Time;
                if (!info.OpenSince.HasValue && punches[punches.Count - 1].Type == Store.OUT) info.LastOut = punches[punches.Count - 1].Time;
            }

            info.Absence = data.AbsenceOn(uid, day);
            if (info.Absence != null) info.Abs = AbsTypes.Find(info.Absence.Code);

            // umowa zlecenie / o dzieło / B2B: liczy się tylko przepracowany czas
            // (bez normy, nadgodzin, spóźnień i zaliczania nieobecności)
            if (!info.HasNorm) return info;

            if (info.Absence != null)
            {
                var rest = norm - info.Worked;
                if (rest < TimeSpan.Zero) rest = TimeSpan.Zero;
                if (info.WorkingDay && info.Abs.Kind == 0) info.Remote = rest;
                if (info.WorkingDay && info.Abs.Kind == 1) info.Excused = rest;
            }

            if (info.WorkingDay)
            {
                // nadgodziny liczone od pełnej minuty (sekundy to tylko szum odbić)
                if (info.Total - norm >= TimeSpan.FromMinutes(1)) info.Overtime = info.Total - norm;
                if (info.FirstIn.HasValue && info.Absence == null &&
                    info.FirstIn.Value.TimeOfDay > Cfg.WorkStart + TimeSpan.FromMinutes(Cfg.LateToleranceMin))
                    info.LateMin = (int)(info.FirstIn.Value.TimeOfDay - Cfg.WorkStart).TotalMinutes;
                if (info.LastOut.HasValue && info.Absence == null && !info.MissingPunch && info.Total < norm &&
                    info.LastOut.Value.TimeOfDay < Cfg.WorkEnd - TimeSpan.FromMinutes(Cfg.LateToleranceMin))
                    info.EarlyMin = (int)(Cfg.WorkEnd - info.LastOut.Value.TimeOfDay).TotalMinutes;
            }
            else
            {
                info.FreeDayWork = info.Worked;
                info.Overtime = info.Worked;
            }
            return info;
        }

        // Dni z odbiciami lub nieobecnościami w okresie [from, to) + dni robocze bez żadnego wpisu
        // (od pierwszego odbicia danej osoby do wczoraj) oznaczone jako BRAK OBECNOŚCI.
        public static List<DayInfo> Range(Data data, DateTime from, DateTime to, string onlyUid)
        {
            var result = new List<DayInfo>();
            var today = DateTime.Today;
            foreach (var uid in data.AllUids())
            {
                if (onlyUid != null && Store.Norm(uid) != Store.Norm(onlyUid)) continue;
                var emp = data.Emp(uid);
                var first = data.FirstSeen(uid);
                for (var d = from.Date; d < to.Date; d = d.AddDays(1))
                {
                    bool has = data.DayPunches(uid, d).Count > 0 || data.AbsenceOn(uid, d) != null;
                    bool noShow = !has && emp != null && emp.Active && emp.HasNorm && first.HasValue && d >= first.Value && d < today && PlCalendar.IsWorkingDay(d);
                    if (!has && !noShow) continue;
                    var info = Day(data, uid, d);
                    info.NoShow = noShow;
                    result.Add(info);
                }
            }
            var pl = StringComparer.Create(new CultureInfo("pl-PL"), true);
            return result.OrderBy(x => x.Name, pl).ThenBy(x => x.Day).ToList();
        }

        public static Summary Summarize(string uid, string name, IEnumerable<DayInfo> days, DateTime from, DateTime to, Data data)
        {
            var s = new Summary { Uid = uid, Name = name, HasNorm = data.HasNorm(uid), Contract = data.ContractOf(uid) };
            if (s.HasNorm)
            {
                double etat = data.EtatOf(uid);
                s.Norm = PlCalendar.NormBetween(from, to, etat);
                var end = DateTime.Today.AddDays(1) < to ? DateTime.Today.AddDays(1) : to;
                s.NormToDate = end > from ? PlCalendar.NormBetween(from, end, etat) : TimeSpan.Zero;
            }
            foreach (var d in days)
            {
                s.Worked += d.Worked;
                s.Remote += d.Remote;
                s.Excused += d.Excused;
                s.Overtime += d.Overtime;
                s.FreeDayWork += d.FreeDayWork;
                s.Night += d.Night;
                if (d.Rows.Count > 0) s.Days++;
                if (d.LateMin > 0) { s.Late++; s.LateMinutes += d.LateMin; }
                if (d.EarlyMin > 0) s.Early++;
                if (d.MissingPunch) s.Missing++;
                if (d.NoShow) s.NoShow++;
                if (d.Abs != null && (d.WorkingDay || d.Abs.Code == "CH"))
                {
                    int n;
                    s.AbsDays.TryGetValue(d.Abs.Code, out n);
                    s.AbsDays[d.Abs.Code] = n + 1;
                }
                if (d.WorkingDay && d.FirstIn.HasValue) { s.ArriveSum += d.FirstIn.Value.TimeOfDay.TotalMinutes; s.ArriveCount++; }
                if (d.WorkingDay && d.LastOut.HasValue) { s.LeaveSum += d.LastOut.Value.TimeOfDay.TotalMinutes; s.LeaveCount++; }
            }
            return s;
        }

        public static List<Summary> SummarizeAll(List<DayInfo> days, DateTime from, DateTime to, Data data)
        {
            var result = new List<Summary>();
            foreach (var g in days.GroupBy(d => Store.Norm(d.Uid)))
                result.Add(Summarize(g.First().Uid, g.First().Name, g, from, to, data));
            // aktywni pracownicy bez żadnego wpisu w okresie też mają wymiar
            foreach (var e in data.Emps.Where(e => e.Active))
                if (!result.Any(r => Store.Norm(r.Uid) == Store.Norm(e.Uid)))
                    result.Add(Summarize(e.Uid, e.Name, new DayInfo[0], from, to, data));
            var pl = StringComparer.Create(new CultureInfo("pl-PL"), true);
            return result.OrderBy(r => r.Name, pl).ToList();
        }

        public static LeaveInfo Leave(Data data, string uid, int year)
        {
            var e = data.Emp(uid);
            var li = new LeaveInfo { Entitled = e != null ? e.LeaveDays : 0, Carry = e != null ? e.LeaveCarry : 0 };
            if (e != null && !e.HasNorm) { li.HasLeave = false; li.Entitled = 0; li.Carry = 0; }
            foreach (var a in data.Absences)
            {
                if (a.Day.Year != year || Store.Norm(a.Uid) != Store.Norm(uid)) continue;
                if (a.Code == "CH") li.Sick++;
                if (!PlCalendar.IsWorkingDay(a.Day)) continue;
                if (a.Code == "UW") li.UW++;
                if (a.Code == "UŻ") li.UZ++;
            }
            return li;
        }

        // Nieobecności nieplanowane: L4, urlop na żądanie, opieka nad dzieckiem, nieusprawiedliwione.
        static readonly string[] Unplanned = { "CH", "UŻ", "OP", "NN" };

        // Współczynnik Bradforda = S² × D (S – liczba odrębnych nieobecności nieplanowanych,
        // D – łączna liczba dni) – liczony za ostatnie 12 miesięcy. Wiele krótkich nieobecności
        // daje dużo wyższy wynik niż jedna długa.
        public static void Bradford(Data data, string uid, DateTime end, out int spells, out int days, out int score)
        {
            spells = 0; days = 0;
            bool prev = false;
            for (var d = end.AddYears(-1); d < end; d = d.AddDays(1))
            {
                if (!PlCalendar.IsWorkingDay(d) || data.CompanyDayOn(d) != null) continue;
                var a = data.AbsenceOn(uid, d);
                bool abs = a != null && !a.Company && Unplanned.Contains(a.Code);
                if (abs) { days++; if (!prev) spells++; }
                prev = abs;
            }
            score = spells * spells * days;
        }

        // Wskaźnik absencji: dni nieobecności nieplanowanej / dni robocze w okresie (do dziś).
        public static double AbsenceRate(Data data, string uid, DateTime from, DateTime to)
        {
            var end = DateTime.Today.AddDays(1) < to ? DateTime.Today.AddDays(1) : to;
            var first = data.FirstSeen(uid);
            if (first.HasValue && first.Value > from) from = first.Value;
            int work = 0, abs = 0;
            for (var d = from; d < end; d = d.AddDays(1))
            {
                if (!PlCalendar.IsWorkingDay(d) || data.CompanyDayOn(d) != null) continue;
                work++;
                var a = data.AbsenceOn(uid, d);
                if (a != null && !a.Company && Unplanned.Contains(a.Code)) abs++;
            }
            return work == 0 ? 0 : 100.0 * abs / work;
        }

        public static string Hm(TimeSpan ts)
        {
            string sign = ts < TimeSpan.Zero ? "-" : "";
            ts = ts.Duration();
            return string.Format("{0}{1}:{2:00}", sign, (int)ts.TotalHours, ts.Minutes);
        }

        public static string Signed(TimeSpan ts)
        {
            return (ts > TimeSpan.Zero ? "+" : "") + Hm(ts);
        }

        public static string Dec(TimeSpan ts) { return Math.Round(ts.TotalHours, 2).ToString("0.00", new CultureInfo("pl-PL")); }

        public static string Clock(double minutes)
        {
            int m = (int)Math.Round(minutes);
            return string.Format("{0:00}:{1:00}", m / 60, m % 60);
        }
    }

    // ------------------------------------------------------------------ raporty

    static class Reports
    {
        static readonly CultureInfo Pl = new CultureInfo("pl-PL");

        static string SavePath(string baseName, string ext, string content)
        {
            string path = Path.Combine(Store.ReportDir, baseName + ext);
            try { File.WriteAllText(path, content, Store.Bom); }
            catch (IOException)
            {
                // poprzednia wersja jest otwarta np. w Excelu
                path = Path.Combine(Store.ReportDir, baseName + "_" + DateTime.Now.ToString("HHmmss") + ext);
                File.WriteAllText(path, content, Store.Bom);
            }
            return path;
        }

        public static string MonthReport(int year, int month)
        {
            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1);
            var data = Data.Load();
            var days = Calc.Range(data, from, to, null);
            var sums = Calc.SummarizeAll(days, from, to, data);

            var sb = new StringBuilder();
            sb.AppendLine("Raport czasu pracy;" + from.ToString("MMMM yyyy", Pl));
            sb.AppendLine(string.Format("Wymiar czasu pracy;{0} h;Godziny pracy;{1}–{2}", Calc.Hm(PlCalendar.MonthNorm(year, month)), Cfg.T(Cfg.WorkStart), Cfg.T(Cfg.WorkEnd)));
            sb.AppendLine("Wygenerowano;" + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine();
            sb.AppendLine("PODSUMOWANIE");
            sb.AppendLine("Pracownik;UID;Forma zatrudnienia;Wymiar;Przepracowano;Przepracowano (godz. dziesiętnie);Nieobecności usprawiedliwione;Saldo;Nadgodziny;Praca w dni wolne;Pora nocna;Dni obecności;Spóźnienia;Suma spóźnień (min);Braki odbić;Dni bez obecności;Urlop wyp. (dni);Urlop na żądanie (dni);L4 (dni);Praca zdalna (dni);Urlop pozostały (rok)");
            foreach (var s in sums)
            {
                var li = Calc.Leave(data, s.Uid, year);
                var emp = data.Emp(s.Uid);
                string contract = Contracts.Name(s.Contract) + (s.HasNorm && emp != null && emp.Etat < 1 ? " (" + Contracts.EtatName(emp.Etat) + ")" : "");
                if (s.HasNorm)
                    sb.AppendFormat("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13};{14};{15};{16};{17};{18};{19};{20}\r\n",
                        s.Name, s.Uid, contract, Calc.Hm(s.Norm), Calc.Hm(s.Total), Calc.Dec(s.Total), Calc.Hm(s.Excused), Calc.Signed(s.Balance),
                        Calc.Hm(s.Overtime), Calc.Hm(s.FreeDayWork), Calc.Hm(s.Night), s.Days, s.Late, s.LateMinutes, s.Missing, s.NoShow,
                        s.Abs("UW"), s.Abs("UŻ"), s.Abs("CH"), s.Abs("PZ"), li.Remaining);
                else // umowa cywilnoprawna: tylko przepracowany czas
                    sb.AppendFormat("{0};{1};{2};–;{3};{4};–;–;–;–;{5};{6};–;–;{7};–;–;–;–;–;–\r\n",
                        s.Name, s.Uid, contract, Calc.Hm(s.Total), Calc.Dec(s.Total), Calc.Hm(s.Night), s.Days, s.Missing);
            }
            sb.AppendLine();
            sb.AppendLine("SZCZEGÓŁY");
            sb.AppendLine("Pracownik;Data;Dzień;Wejście;Wyjście;Czas;Suma dnia;Nadgodziny;Nieobecność;Uwagi");
            foreach (var d in days)
            {
                var rows = d.Rows.Count > 0 ? d.Rows : new List<WorkRow> { new WorkRow() };
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    bool last = i == rows.Count - 1;
                    sb.AppendFormat("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9}\r\n",
                        d.Name, d.Day.ToString("yyyy-MM-dd"), d.Day.ToString("dddd", Pl),
                        r.In.HasValue ? r.In.Value.ToString("HH:mm") : "",
                        r.Out.HasValue ? r.Out.Value.ToString("HH:mm") : "",
                        r.In.HasValue && r.Out.HasValue ? Calc.Hm(r.Duration) : "",
                        last ? Calc.Hm(d.Total) : "",
                        last && d.Overtime > TimeSpan.Zero ? Calc.Hm(d.Overtime) : "",
                        last && d.Abs != null ? d.Abs.Code : "",
                        string.Join(", ", new[] { r.Note, last ? d.Notes() : "" }.Where(x => x != "")));
                }
            }
            return SavePath(string.Format("raport_{0:yyyy-MM}", from), ".csv", sb.ToString());
        }

        static string H(string s) { return WebUtility.HtmlEncode(s ?? ""); }

        // Ewidencja godzin dla umowy zlecenie (obowiązkowa od 2017 r. – minimalna stawka godzinowa),
        // umowy o dzieło i B2B: dni, godziny rozpoczęcia i zakończenia, liczba godzin, suma.
        static void CivilCard(StringBuilder sb, Summary s, Dictionary<DateTime, DayInfo> days, DateTime from, DateTime to)
        {
            bool zl = s.Contract == "ZLECENIE";
            string title = zl ? "Ewidencja liczby godzin wykonywania zlecenia" : "Ewidencja godzin pracy";
            sb.Append("<div class=card><h1>").Append(H(title)).Append("</h1><table class=meta>")
              .AppendFormat("<tr><td>{0}: <b>{1}</b></td><td>Okres: <b>{2}</b></td></tr>", zl ? "Zleceniobiorca" : "Wykonawca", H(s.Name), H(from.ToString("MMMM yyyy", Pl)))
              .AppendFormat("<tr><td>Forma: {0}</td><td>Liczba godzin w miesiącu: <b>{1}</b> ({2} h)</td></tr></table>",
                  H(Contracts.Name(s.Contract)), Calc.Hm(s.Total), Calc.Dec(s.Total))
              .Append("<table><tr><th>Dzień</th><th>Dzień tyg.</th><th>Rozpoczęcie</th><th>Zakończenie</th><th>Liczba godzin</th><th>Uwagi</th></tr>");
            for (var d = from; d < to; d = d.AddDays(1))
            {
                DayInfo di;
                days.TryGetValue(d, out di);
                string start = "", end = "", worked = "", note = "";
                bool prob = false;
                if (di != null)
                {
                    if (di.FirstIn.HasValue) start = di.FirstIn.Value.ToString("HH:mm");
                    if (di.LastOut.HasValue) end = di.LastOut.Value.ToString("HH:mm");
                    if (di.Total > TimeSpan.Zero) worked = Calc.Hm(di.Total);
                    if (di.Rows.Count > 1) note = di.Rows.Count + " odcinki pracy";
                    if (di.MissingPunch) { note = "brak odbicia"; prob = true; }
                    if (di.Rows.Any(r => r.Note.Contains("korekta"))) note = (note + " korekta ręczna").Trim();
                }
                sb.AppendFormat("<tr class=\"{0}\"><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td class=l>{6}</td></tr>",
                    prob ? "prob" : (di == null && !PlCalendar.IsWorkingDay(d) ? "free" : ""), d.Day, H(d.ToString("ddd", Pl)), start, end, worked, H(note));
            }
            sb.AppendFormat("<tr><th colspan=4 class=l>Razem</th><th>{0}</th><th></th></tr></table>", Calc.Hm(s.Total));
            sb.AppendFormat("<table class=sum><tr><td>Dni z wykonywaniem pracy: <b>{0}</b></td><td>Łącznie: <b>{1} h</b> ({2} h dziesiętnie)</td><td>Braki odbić: {3}</td></tr></table>",
                s.Days, Calc.Hm(s.Total), Calc.Dec(s.Total), s.Missing);
            sb.Append("<div class=sign><div>data i podpis ").Append(zl ? "zleceniobiorcy" : "wykonawcy")
              .Append("</div><div>data i podpis ").Append(zl ? "zleceniodawcy" : "zamawiającego").Append("</div></div></div>");
        }

        // Karta ewidencji czasu pracy (art. 149 KP) – strona HTML do wydruku lub zapisu jako PDF.
        public static string TimeCards(int year, int month, string onlyUid)
        {
            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1);
            var data = Data.Load();
            var all = Calc.Range(data, from, to, onlyUid);
            var uids = data.Emps.Where(e => e.Active).Select(e => e.Uid)
                .Concat(all.Select(d => d.Uid))
                .Where(u => onlyUid == null || Store.Norm(u) == Store.Norm(onlyUid))
                .GroupBy(Store.Norm).Select(g => g.First())
                .OrderBy(u => data.NameOf(u), StringComparer.Create(Pl, true)).ToList();

            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=pl><head><meta charset=utf-8><title>Karta ewidencji czasu pracy ")
              .Append(from.ToString("MMMM yyyy", Pl)).Append("</title><style>")
              .Append("@page{size:A4;margin:12mm}body{font:11px Segoe UI,Arial,sans-serif;color:#111;margin:0}")
              .Append(".card{page-break-after:always;padding:8px}.card:last-child{page-break-after:auto}")
              .Append("h1{font-size:17px;margin:0 0 6px}table{border-collapse:collapse;width:100%}")
              .Append("td,th{border:1px solid #888;padding:2px 4px;text-align:center}th{background:#e5e7eb;font-weight:600}")
              .Append(".meta td{border:none;text-align:left;padding:1px 6px 1px 0}.free td{background:#f1f5f9;color:#555}")
              .Append(".prob td{background:#fee2e2}.l{text-align:left}.sum{margin-top:8px}.sum td{text-align:left}")
              .Append(".legend{margin-top:6px;font-size:10px;color:#444}.sign{display:flex;justify-content:space-between;margin-top:34px}")
              .Append(".sign div{border-top:1px solid #333;width:40%;text-align:center;padding-top:3px}")
              .Append(".noprint{background:#fef3c7;padding:8px;font-size:13px;margin-bottom:8px}@media print{.noprint{display:none}}")
              .Append("</style></head><body>")
              .Append("<div class=noprint>Aby zapisać jako PDF: naciśnij <b>Ctrl+P</b> i wybierz drukarkę „Zapisz jako PDF” / „Microsoft Print to PDF”. Każdy pracownik jest na osobnej stronie.</div>");

            foreach (var uid in uids)
            {
                var days = all.Where(d => Store.Norm(d.Uid) == Store.Norm(uid)).ToDictionary(d => d.Day);
                var s = Calc.Summarize(uid, data.NameOf(uid), days.Values, from, to, data);
                if (!s.HasNorm) { CivilCard(sb, s, days, from, to); continue; }
                var li = Calc.Leave(data, uid, year);
                var emp = data.Emp(uid);
                string etat = emp != null && emp.Etat < 1 ? ", " + Contracts.EtatName(emp.Etat) : "";
                sb.Append("<div class=card><h1>Karta ewidencji czasu pracy</h1><table class=meta>")
                  .AppendFormat("<tr><td>Pracownik: <b>{0}</b></td><td>Okres: <b>{1}</b></td></tr>", H(s.Name), H(from.ToString("MMMM yyyy", Pl)))
                  .AppendFormat("<tr><td>Umowa o pracę{3} – system podstawowy, norma dobowa {0} h</td><td>Godziny pracy: {1}–{2}</td></tr>",
                      (Cfg.NormHours * (emp != null ? emp.Etat : 1)).ToString("0.##", Pl), Cfg.T(Cfg.WorkStart), Cfg.T(Cfg.WorkEnd), H(etat))
                  .AppendFormat("<tr><td>Wymiar czasu pracy w miesiącu: <b>{0} h</b></td><td>Pora nocna: {1}–{2}</td></tr></table>",
                      Calc.Hm(s.Norm), Cfg.T(Cfg.NightFrom), Cfg.T(Cfg.NightTo))
                  .Append("<table><tr><th>Dzień</th><th>Dzień tyg.</th><th>Rozpoczęcie</th><th>Zakończenie</th><th>Przepracowano</th>")
                  .Append("<th>Nadgodziny</th><th>Praca w dni wolne / święta</th><th>Pora nocna</th><th>Nieobecność</th><th>Uwagi</th></tr>");
                for (var d = from; d < to; d = d.AddDays(1))
                {
                    DayInfo di;
                    days.TryGetValue(d, out di);
                    string cls = di != null && di.Problem ? "prob" : (!PlCalendar.IsWorkingDay(d) ? "free" : "");
                    string start = "", end = "", worked = "", ot = "", free = "", night = "", abs = "", note = "";
                    if (di != null)
                    {
                        if (di.FirstIn.HasValue) start = di.FirstIn.Value.ToString("HH:mm");
                        if (di.LastOut.HasValue) end = di.LastOut.Value.ToString("HH:mm");
                        if (di.Total > TimeSpan.Zero) worked = Calc.Hm(di.Total);
                        if (di.Overtime > TimeSpan.Zero) ot = Calc.Hm(di.Overtime);
                        if (di.FreeDayWork > TimeSpan.Zero) free = Calc.Hm(di.FreeDayWork);
                        if (di.Night > TimeSpan.Zero) night = Calc.Hm(di.Night);
                        if (di.Abs != null) abs = di.Abs.Code;
                        var notes = new List<string>();
                        if (di.MissingPunch) notes.Add("brak odbicia");
                        var n = di.Notes();
                        if (n != "") notes.Add(n);
                        note = string.Join(", ", notes);
                    }
                    else if (!PlCalendar.IsWorkingDay(d)) note = PlCalendar.Holiday(d) ?? "dzień wolny";
                    sb.AppendFormat("<tr class=\"{0}\"><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td>{8}</td><td>{9}</td><td class=l>{10}</td></tr>",
                        cls, d.Day, H(d.ToString("ddd", Pl)), start, end, worked, ot, free, night, H(abs), H(note));
                }
                sb.AppendFormat("<tr><th colspan=4 class=l>Razem</th><th>{0}</th><th>{1}</th><th>{2}</th><th>{3}</th><th colspan=2></th></tr></table>",
                    Calc.Hm(s.Total), Calc.Hm(s.Overtime), Calc.Hm(s.FreeDayWork), Calc.Hm(s.Night));
                sb.Append("<table class=sum><tr>")
                  .AppendFormat("<td>Wymiar: <b>{0} h</b></td><td>Przepracowano: <b>{1} h</b> (w tym zdalnie/delegacja {2} h)</td><td>Nieobecności usprawiedliwione: {3} h</td><td>Saldo: <b>{4} h</b></td></tr><tr>",
                      Calc.Hm(s.Norm), Calc.Hm(s.Total), Calc.Hm(s.Remote), Calc.Hm(s.Excused), Calc.Signed(s.Total + s.Excused - s.Norm))
                  .AppendFormat("<td>Urlop wypoczynkowy: {0} dni (w tym na żądanie {1})</td><td>Choroba (L4): {2} dni</td><td>Opieka art. 188: {3} dni, okolicznościowy: {4} dni</td><td>Bezpłatny: {5}, nieusprawiedliwione: {6}</td></tr><tr>",
                      s.Abs("UW") + s.Abs("UŻ"), s.Abs("UŻ"), s.Abs("CH"), s.Abs("OP"), s.Abs("UO"), s.Abs("UB"), s.Abs("NN"))
                  .AppendFormat("<td>Spóźnienia: {0} ({1} min)</td><td>Urlop w {2}: wykorzystano {3} z {4} dni, pozostało {5}</td><td colspan=2>Praca zdalna: {6} dni, delegacje: {7} dni</td></tr></table>",
                      s.Late, s.LateMinutes, year, li.Used, li.Entitled + li.Carry, li.Remaining, s.Abs("PZ"), s.Abs("DL"));
                sb.Append("<div class=legend>Oznaczenia: ")
                  .Append(string.Join(", ", AbsTypes.All.Select(t => "<b>" + H(t.Code) + "</b> – " + H(t.Name.ToLower()))))
                  .Append("</div><div class=sign><div>data i podpis pracownika</div><div>data i podpis pracodawcy</div></div></div>");
            }
            sb.Append("</body></html>");
            return SavePath(string.Format("karta_ewidencji_{0:yyyy-MM}", from) + (onlyUid != null ? "_" + Store.Norm(onlyUid) : ""), ".html", sb.ToString());
        }
    }

    // ------------------------------------------------------------------ kopia zapasowa poza komputerem

    static class CloudBackup
    {
        static string StampFile { get { return Path.Combine(Store.Dir, "ostatnia_kopia.txt"); } }
        const string Prefix = "EwidencjaCzasu_kopia_";

        public static DateTime? Last
        {
            get
            {
                try
                {
                    DateTime d;
                    if (File.Exists(StampFile) && DateTime.TryParseExact(File.ReadAllText(StampFile).Trim(), "yyyy-MM-dd HH:mm",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
                }
                catch { }
                return null;
            }
        }

        // Dysk Google na komputer tworzy zwykle dysk G: z folderem „Mój dysk”.
        public static string DetectGoogleDrive()
        {
            foreach (var drive in DriveInfo.GetDrives())
                foreach (var name in new[] { "Mój dysk", "My Drive" })
                {
                    try
                    {
                        var p = Path.Combine(drive.RootDirectory.FullName, name);
                        if (Directory.Exists(p)) return Path.Combine(p, "EwidencjaCzasu-kopie");
                    }
                    catch { }
                }
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var name in new[] { "Google Drive", "Mój dysk", "My Drive" })
                if (Directory.Exists(Path.Combine(home, name))) return Path.Combine(home, name, "EwidencjaCzasu-kopie");
            return null;
        }

        public static bool Due
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Cfg.BackupFolder)) return false;
                var last = Last;
                return !last.HasValue || (DateTime.Now - last.Value).TotalDays >= Cfg.BackupEveryDays;
            }
        }

        public static string Run()
        {
            string folder = Cfg.BackupFolder;
            if (string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("Nie ustawiono folderu kopii (Panel → Ustawienia).");
            string root = Path.GetPathRoot(folder);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                throw new DirectoryNotFoundException("Folder kopii jest niedostępny: " + folder + "\nCzy Dysk Google jest uruchomiony?");
            var parent = Path.GetDirectoryName(folder.TrimEnd('\\'));
            if (!Directory.Exists(folder) && parent != null && !Directory.Exists(parent))
                throw new DirectoryNotFoundException("Folder kopii jest niedostępny: " + folder + "\nCzy Dysk Google jest uruchomiony?");
            Directory.CreateDirectory(folder);

            string name = Prefix + DateTime.Now.ToString("yyyy-MM-dd_HHmm") + ".zip";
            string tmp = Path.Combine(Path.GetTempPath(), name);
            using (var fs = new FileStream(tmp, FileMode.Create))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                string baseDir = Store.Dir.TrimEnd('\\') + "\\";
                foreach (var f in Directory.GetFiles(Store.Dir, "*", SearchOption.AllDirectories))
                {
                    string rel = f.Substring(baseDir.Length);
                    if (rel.StartsWith("kopie\\", StringComparison.OrdinalIgnoreCase) || rel.EndsWith(".tmp")) continue;
                    var entry = zip.CreateEntry(rel.Replace('\\', '/'), CompressionLevel.Optimal);
                    var bytes = Store.ReadBytesShared(f);
                    using (var es = entry.Open()) es.Write(bytes, 0, bytes.Length);
                }
            }
            string target = Path.Combine(folder, name);
            File.Copy(tmp, target, true);
            File.Delete(tmp);
            File.WriteAllText(StampFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

            // zostawiamy rok cotygodniowych kopii
            foreach (var old in Directory.GetFiles(folder, Prefix + "*.zip").OrderByDescending(x => x).Skip(52))
                try { File.Delete(old); } catch { }
            return target;
        }
    }

    // ------------------------------------------------------------------ PIN administratora

    static class Pin
    {
        static string FilePath { get { return Path.Combine(Store.Dir, "pin.dat"); } }
        static DateTime unlockedUntil = DateTime.MinValue, blockedUntil = DateTime.MinValue;
        static int fails;

        public static bool IsSet { get { return File.Exists(FilePath); } }

        static string Hash(string pin, byte[] salt)
        {
            using (var k = new Rfc2898DeriveBytes(pin, salt, 20000)) return Convert.ToBase64String(k.GetBytes(32));
        }

        static void Set(string pin)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            File.WriteAllText(FilePath, Convert.ToBase64String(salt) + ":" + Hash(pin, salt));
        }

        static bool Check(string pin)
        {
            try
            {
                var parts = File.ReadAllText(FilePath).Trim().Split(':');
                return parts.Length == 2 && Hash(pin, Convert.FromBase64String(parts[0])) == parts[1];
            }
            catch { return false; }
        }

        public static void Extend() { unlockedUntil = DateTime.Now.AddMinutes(5); }
        public static void Lock() { unlockedUntil = DateTime.MinValue; }

        // true = można kontynuować (PIN poprawny albo odblokowane w ciągu ostatnich 5 minut)
        public static bool Require(string why)
        {
            if (DateTime.Now < unlockedUntil) return true;
            if (!IsSet) return Setup(false);
            if (DateTime.Now < blockedUntil)
            {
                MessageBox.Show("Zbyt wiele błędnych prób. Spróbuj ponownie za " + (int)(blockedUntil - DateTime.Now).TotalSeconds + " s.", "PIN");
                return false;
            }
            string pin = InputForm.Ask("PIN administratora", why + "\n\nPodaj PIN:", "", true);
            if (pin == null) return false;
            if (Check(pin.Trim())) { fails = 0; Extend(); return true; }
            if (++fails >= 3) { fails = 0; blockedUntil = DateTime.Now.AddSeconds(60); }
            MessageBox.Show("Błędny PIN.", "PIN", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        public static bool Setup(bool change)
        {
            while (true)
            {
                string a = InputForm.Ask(change ? "Zmiana PIN" : "Ustaw PIN administratora",
                    (change ? "Podaj nowy PIN" : "Ustaw PIN administratora") + " (4–8 cyfr).\n\nBędzie wymagany do otwarcia panelu, zmian w pracownikach, raportów, wstrzymania i zamknięcia programu.", "", true);
                if (a == null) return false;
                a = a.Trim();
                if (a.Length < 4 || a.Length > 8 || !a.All(char.IsDigit))
                {
                    MessageBox.Show("PIN musi mieć od 4 do 8 cyfr.", "PIN");
                    continue;
                }
                string b = InputForm.Ask("Powtórz PIN", "Wpisz PIN jeszcze raz:", "", true);
                if (b == null) return false;
                if (a != b.Trim())
                {
                    MessageBox.Show("PIN-y się różnią. Spróbuj jeszcze raz.", "PIN");
                    continue;
                }
                Set(a);
                Store.Audit(change ? "ZMIANA PIN" : "USTAWIENIE PIN", "", "");
                Extend();
                MessageBox.Show("PIN zapisany.", "PIN");
                return true;
            }
        }
    }
}
