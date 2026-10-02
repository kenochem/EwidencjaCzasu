using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
namespace EwidencjaCzasu
{
    static class Tests
    {
        static int fails = 0;
        static void Check(bool ok, string name) { Console.WriteLine((ok ? "OK   " : "FAIL ") + name); if (!ok) fails++; }

        static List<KeyEvt> Seq(string text, uint start, uint step)
        {
            var l = new List<KeyEvt>(); uint t = start;
            foreach (char c in text)
            {
                ushort vk = c == '\n' ? (ushort)0x0D : c == 'a' ? (ushort)0x41 : (ushort)c;
                l.Add(new KeyEvt { Vk = vk, Time = t }); t += step;
                l.Add(new KeyEvt { Vk = vk, Up = true, Time = t }); t += step;
            }
            return l;
        }

        static void Run(string label, List<KeyEvt> evs, string expCard, int expOut)
        {
            var cap = new KeyboardCapture();
            string card = null; var outp = new List<KeyEvt>();
            cap.CardRead += c => card = c;
            cap.ReplaySink = r => outp.AddRange(r);
            foreach (var e in evs) if (!cap.Process(e)) outp.Add(e);
            cap.Process(new KeyEvt { Vk = 0x10, Time = evs.Last().Time + 10000 });
            int downs = outp.Count(e => !e.Up && e.Vk != 0x10);
            Check(card == expCard && downs == expOut, label + " card=" + (card ?? "null") + " out=" + downs);
        }

        static Punch P(string uid, string name, DateTime t, string type) { return new Punch { Time = t, Uid = uid, Name = name, Type = type, Source = "KARTA" }; }

        static void Main()
        {
            Run("czytnik 5ms", Seq("5550001111\n", 1000, 5), "5550001111", 0);
            Run("czytnik 20ms", Seq("5550002222\n", 1000, 20), "5550002222", 0);
            Run("czlowiek 120ms", Seq("5550001111\n", 1000, 120), null, 11);
            Run("za krotki kod", Seq("12345\n", 1000, 5), null, 6);

            // kalendarz
            Check(PlCalendar.Easter(2026) == new DateTime(2026, 4, 5), "Wielkanoc 2026 = 5 kwietnia");
            Check(PlCalendar.Holiday(new DateTime(2026, 6, 4)) == "Boże Ciało", "Boże Ciało 2026 = 4 czerwca");
            Check(PlCalendar.Holiday(new DateTime(2026, 12, 24)) == "Wigilia", "Wigilia wolna od 2025");
            Check(PlCalendar.MonthNorm(2026, 10).TotalHours == 176, "wymiar paźdz. 2026 = 176 h: " + PlCalendar.MonthNorm(2026, 10).TotalHours);
            Check(PlCalendar.MonthNorm(2026, 11).TotalHours == 160, "wymiar list. 2026 = 160 h: " + PlCalendar.MonthNorm(2026, 11).TotalHours);
            Check(PlCalendar.MonthNorm(2026, 12).TotalHours == 160, "wymiar grudz. 2026 = 160 h (święto w sobotę 26.12): " + PlCalendar.MonthNorm(2026, 12).TotalHours);

            string dir = Path.Combine(Path.GetTempPath(), "rcp_test_" + Guid.NewGuid().ToString("N"));
            Store.Dir = dir; Store.Init();
            Check(File.ReadAllText(Store.CfgFile).Contains("godzina_od=08:00"), "ustawienia domyślne 8-16");
            Store.SaveEmployees(new List<Employee> {
                new Employee { Uid = "5550001111", Name = "Jan Kowalski", Active = true, LeaveDays = 26 },
                new Employee { Uid = "5550002222", Name = "Anna Nowak", Active = true, LeaveDays = 20, LeaveCarry = 3 } });
            string J = "5550001111", A = "5550002222";
            var d1 = new DateTime(2026, 9, 14); // poniedziałek
            Store.AppendPunch(P(J, "Jan Kowalski", d1.AddHours(8).AddMinutes(12), Store.IN));
            Store.AppendPunch(P(J, "Jan Kowalski", d1.AddHours(16).AddMinutes(30), Store.OUT));
            Store.AppendPunch(P(J, "Jan Kowalski", d1.AddDays(1).AddHours(7).AddMinutes(58), Store.IN));
            Store.AppendPunch(P(J, "Jan Kowalski", d1.AddDays(1).AddHours(14), Store.OUT));
            Store.AppendPunch(P(J, "Jan Kowalski", d1.AddDays(5).AddHours(10), Store.IN)); // sobota
            Store.AppendPunch(P(J, "Jan Kowalski", d1.AddDays(5).AddHours(13), Store.OUT));
            Store.AppendPunch(P(A, "Anna Nowak", d1.AddHours(8), Store.IN));
            Store.AppendPunch(P(A, "Anna Nowak", d1.AddHours(16), Store.OUT));
            Store.AddAbsences(new List<Absence> {
                new Absence { Day = d1.AddDays(1), Uid = A, Name = "Anna Nowak", Code = "UW" },
                new Absence { Day = d1.AddDays(2), Uid = A, Name = "Anna Nowak", Code = "PZ" } });

            var data = Data.Load();
            var mon = Calc.Day(data, J, d1);
            Check(mon.LateMin == 12, "spóźnienie 12 min: " + mon.LateMin);
            Check(Calc.Hm(mon.Total) == "8:18" && Calc.Hm(mon.Overtime) == "0:18", "8:18 h, nadgodziny 0:18: " + Calc.Hm(mon.Total) + " / " + Calc.Hm(mon.Overtime));
            var tue = Calc.Day(data, J, d1.AddDays(1));
            Check(tue.LateMin == 0 && tue.EarlyMin == 120, "wcześniejsze wyjście 120 min: " + tue.EarlyMin);
            var sat = Calc.Day(data, J, d1.AddDays(5));
            Check(!sat.WorkingDay && Calc.Hm(sat.FreeDayWork) == "3:00", "sobota = praca w dzień wolny 3:00");
            var annaUw = Calc.Day(data, A, d1.AddDays(1));
            Check(annaUw.Excused.TotalHours == 8 && annaUw.Abs.Code == "UW", "urlop zalicza 8 h");
            var annaPz = Calc.Day(data, A, d1.AddDays(2));
            Check(annaPz.Remote.TotalHours == 8, "praca zdalna = 8 h przepracowane");

            var range = Calc.Range(data, new DateTime(2026, 9, 1), new DateTime(2026, 10, 1), null);
            var annaNoShow = range.Where(x => x.Uid == A && x.NoShow).Select(x => x.Day.Day).ToList();
            Check(annaNoShow.Contains(17) && !annaNoShow.Contains(15) && !annaNoShow.Contains(13) && !annaNoShow.Contains(11),
                "brak obecności Anny od 17.09 (nie w weekend, nie przed pierwszym odbiciem): " + string.Join(",", annaNoShow));
            var sums = Calc.SummarizeAll(range, new DateTime(2026, 9, 1), new DateTime(2026, 10, 1), data);
            var sj = sums.First(s => s.Uid == J);
            Check(sj.Norm.TotalHours == 176 && sj.Late == 1 && sj.Days == 3, "podsumowanie Jana: wymiar " + sj.Norm.TotalHours + ", spóźnienia " + sj.Late + ", dni " + sj.Days);
            var li = Calc.Leave(data, A, 2026);
            Check(li.Remaining == 22 && li.UW == 1, "urlop Anny: 20+3-1 = 22: " + li.Remaining);

            // poprawki i nieobecności
            var jIn = data.Punches.First(p => p.Uid == J && p.Type == Store.IN);
            Store.ChangePunch(jIn, P(J, "Jan Kowalski", jIn.Time.AddMinutes(-12), Store.IN));
            Store.RemoveAbsences(new List<Absence> { new Absence { Day = d1.AddDays(2), Uid = A, Code = "PZ" } });
            data = Data.Load();
            Check(Calc.Day(data, J, d1).LateMin == 0, "po poprawce brak spóźnienia");
            Check(data.Absences.Count == 1, "nieobecność usunięta");
            Check(File.ReadAllLines(Store.AuditFile).Length == 5, "historia zmian: " + File.ReadAllLines(Store.AuditFile).Length + " linie");

            // raporty
            var rep = Reports.MonthReport(2026, 9);
            Check(File.ReadAllText(rep).Contains("Jan Kowalski;5550001111;Umowa o pracę;176:00"), "raport Excel");
            var card = Reports.TimeCards(2026, 9, null);
            var html = File.ReadAllText(card);
            Check(html.Contains("Karta ewidencji czasu pracy") && html.Contains("Anna Nowak") && html.Contains("Wymiar czasu pracy w miesiącu: <b>176:00 h"), "karta ewidencji HTML");

            // kopia zapasowa
            string bdir = Path.Combine(dir + "_drive", "EwidencjaCzasu-kopie");
            Directory.CreateDirectory(Path.GetDirectoryName(bdir));
            Cfg.BackupFolder = bdir;
            Check(CloudBackup.Due, "kopia wymagana (jeszcze nie było)");
            var zipPath = CloudBackup.Run();
            using (var z = ZipFile.OpenRead(zipPath))
                Check(z.Entries.Any(e => e.FullName == "odbicia.csv") && z.Entries.Any(e => e.FullName == "nieobecnosci.csv") && !z.Entries.Any(e => e.FullName.StartsWith("kopie/")),
                    "zip zawiera dane (" + z.Entries.Count + " plików, bez lokalnych kopii)");
            Check(!CloudBackup.Due, "kolejna kopia dopiero za 7 dni");

            // formy zatrudnienia: zlecenie i pół etatu
            var emps2 = Store.LoadEmployees();
            emps2.Add(new Employee { Uid = "333333333", Name = "Zbigniew Zlecenie", Active = true, Contract = "ZLECENIE" });
            emps2.Add(new Employee { Uid = "444444444", Name = "Paula Połowa", Active = true, Contract = "UOP", Etat = 0.5 });
            Store.SaveEmployees(emps2);
            var reread = Store.LoadEmployees();
            Check(reread.First(e => e.Uid == "333333333").Contract == "ZLECENIE" && reread.First(e => e.Uid == "444444444").Etat == 0.5, "zapis formy zatrudnienia i etatu");
            var oct1 = new DateTime(2026, 9, 21); // poniedziałek
            Store.AppendPunch(P("333333333", "Zbigniew Zlecenie", oct1.AddHours(9).AddMinutes(40), Store.IN));
            Store.AppendPunch(P("333333333", "Zbigniew Zlecenie", oct1.AddHours(19), Store.OUT));
            Store.AppendPunch(P("333333333", "Zbigniew Zlecenie", oct1.AddDays(5).AddHours(10), Store.IN)); // sobota
            Store.AppendPunch(P("333333333", "Zbigniew Zlecenie", oct1.AddDays(5).AddHours(12), Store.OUT));
            Store.AppendPunch(P("444444444", "Paula Połowa", oct1.AddHours(8), Store.IN));
            Store.AppendPunch(P("444444444", "Paula Połowa", oct1.AddHours(13), Store.OUT));
            var d2 = Data.Load();
            var zl = Calc.Day(d2, "333333333", oct1);
            Check(!zl.HasNorm && zl.LateMin == 0 && zl.Overtime == TimeSpan.Zero && Calc.Hm(zl.Total) == "9:20", "zlecenie: 9:20 h, bez spóźnienia i nadgodzin");
            var r2 = Calc.Range(d2, new DateTime(2026, 9, 1), new DateTime(2026, 10, 1), "333333333");
            Check(!r2.Any(x => x.NoShow), "zlecenie: brak 'braku obecności' w dni bez pracy");
            var sz = Calc.SummarizeAll(r2, new DateTime(2026, 9, 1), new DateTime(2026, 10, 1), d2).First(x => x.Uid == "333333333");
            Check(!sz.HasNorm && sz.Norm == TimeSpan.Zero && Calc.Hm(sz.Total) == "11:20" && sz.Days == 2, "zlecenie: suma 11:20 h z 2 dni, bez wymiaru");
            Check(!Calc.Leave(d2, "333333333", 2026).HasLeave, "zlecenie: bez urlopu");
            var half = Calc.Day(d2, "444444444", oct1);
            Check(half.DayNorm.TotalHours == 4 && Calc.Hm(half.Overtime) == "1:00", "pół etatu: norma 4 h, 5 h pracy = 1 h ponad");
            var sh = Calc.Summarize("444444444", "Paula", new DayInfo[0], new DateTime(2026, 10, 1), new DateTime(2026, 11, 1), d2);
            Check(sh.Norm.TotalHours == 88, "pół etatu: wymiar paźdz. 88 h: " + sh.Norm.TotalHours);
            var cardZl = File.ReadAllText(Reports.TimeCards(2026, 9, "333333333"));
            Check(cardZl.Contains("Ewidencja liczby godzin wykonywania zlecenia") && cardZl.Contains("11:20") && cardZl.Contains("zleceniodawcy"), "karta: ewidencja godzin zlecenia");
            Check(File.ReadAllText(Reports.MonthReport(2026, 9)).Contains("Zbigniew Zlecenie;333333333;Umowa zlecenie;–;11:20"), "raport Excel: zlecenie bez wymiaru");

            // dzień wolny firmowy + Bradford
            Store.SaveCompanyDays(new List<CompanyDay> { new CompanyDay { Day = new DateTime(2026, 9, 25), Code = "DW", Name = "Dzień firmy" } });
            var d3 = Data.Load();
            var jFri = Calc.Day(d3, J, new DateTime(2026, 9, 25));
            Check(jFri.Abs != null && jFri.Abs.Code == "DW" && jFri.Excused.TotalHours == 8, "dzień wolny firmowy: zalicza 8 h");
            Check(Calc.Day(d3, "333333333", new DateTime(2026, 9, 25)).Abs == null, "dzień wolny firmowy: nie dotyczy zlecenia");
            var r3 = Calc.Range(d3, new DateTime(2026, 9, 1), new DateTime(2026, 10, 1), A);
            Check(!r3.Any(x => x.Day.Day == 25 && x.NoShow), "dzień wolny firmowy: brak 'braku obecności'");
            Store.AddAbsences(new List<Absence> {
                new Absence { Day = new DateTime(2026, 9, 1), Uid = J, Name = "Jan Kowalski", Code = "CH" },
                new Absence { Day = new DateTime(2026, 9, 2), Uid = J, Name = "Jan Kowalski", Code = "CH" },
                new Absence { Day = new DateTime(2026, 9, 8), Uid = J, Name = "Jan Kowalski", Code = "UŻ" },
                new Absence { Day = new DateTime(2026, 9, 10), Uid = J, Name = "Jan Kowalski", Code = "UW" } });
            int sp, bd, sc;
            Calc.Bradford(Data.Load(), J, new DateTime(2026, 10, 1), out sp, out bd, out sc);
            Check(sp == 2 && bd == 3 && sc == 12, "Bradford: 2 nieobecności, 3 dni = 2²×3 = 12 (urlop się nie liczy): " + sp + "/" + bd + "/" + sc);

            // przerwy w działaniu
            var thu = new DateTime(2026, 10, 1);
            Check(Store.DowntimeInWorkHours(thu.AddHours(10), thu.AddHours(10).AddMinutes(5)).TotalMinutes == 5, "restart 10:00–10:05 = 5 min w godzinach pracy");
            Check(Store.DowntimeInWorkHours(thu.AddHours(-6), thu.AddHours(7).AddMinutes(40)).TotalMinutes == 0, "komputer wyłączony na noc, włączony 7:40 = bez ostrzeżenia");
            Check(Store.DowntimeInWorkHours(thu.AddHours(-6), thu.AddHours(8).AddMinutes(10)).TotalMinutes == 25, "włączony dopiero 8:10 = ostrzeżenie (25 min od 7:45)");
            Check(Store.DowntimeInWorkHours(new DateTime(2026, 10, 3, 9, 0, 0), new DateTime(2026, 10, 3, 12, 0, 0)).TotalMinutes == 0, "sobota = bez ostrzeżenia");
            Store.LogDowntime(thu.AddHours(10), thu.AddHours(10).AddMinutes(5));
            Check(Store.Downtimes(thu).Count == 1 && Store.Downtimes(thu.AddDays(1)).Count == 0, "zapis i odczyt przerw");

            Directory.Delete(dir, true);
            Directory.Delete(dir + "_drive", true);
            Console.WriteLine(fails == 0 ? "WSZYSTKO OK" : ("BLEDY: " + fails));
        }
    }
}
