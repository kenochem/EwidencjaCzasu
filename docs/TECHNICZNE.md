# 🔧 Dokumentacja techniczna

[← Powrót do README](../README.md)

- [Technologia](#technologia)
- [Architektura](#architektura)
- [Rozpoznawanie czytnika](#rozpoznawanie-czytnika)
- [Pliki danych](#pliki-danych)
- [Ustawienia](#ustawienia)
- [Wyliczenia czasu pracy](#wyliczenia-czasu-pracy)
- [Wskaźniki absencji](#wskaźniki-absencji)
- [Odporność na awarie](#odporność-na-awarie)
- [Wygląd](#wygląd)
- [Kompilacja](#kompilacja)
- [Testy](#testy)
- [Parametry wiersza poleceń](#parametry-wiersza-poleceń)
- [Wskazówki dla rozwoju](#wskazówki-dla-rozwoju)

---

## Technologia

| | |
|---|---|
| Język | C# 5 (kompilator `csc.exe` z .NET Framework 4, wbudowany w Windows) |
| Interfejs | Windows Forms, własne kontrolki rysowane GDI+ |
| Zależności | brak – tylko biblioteki systemowe (`System.Windows.Forms`, `System.Drawing`, `System.IO.Compression`) |
| Dane | pliki CSV (UTF-8 z BOM, separator `;`) – czytelne w Excelu |
| Wynik | jeden plik `EwidencjaCzasu.exe` (~200 KB), bez instalatora |

> Kompilator .NET Framework obsługuje tylko C# 5 – w kodzie nie ma interpolacji `$"..."`, operatora `?.`
> ani składni `=>` dla właściwości.

## Architektura

```
┌─────────────────────────┐   numer karty   ┌──────────────────────────────┐
│ Czytnik.cs              │ ──────────────▶ │ Aplikacja.cs  (TrayApp)      │
│ KeyboardCapture         │                 │ OnCard → wejście / wyjście   │
│ (osobny wątek + hak     │                 │ przypomnienia, kopie, PIN    │
│  WH_KEYBOARD_LL)        │                 │ dymki (Toast), okna dialogowe│
└─────────────────────────┘                 └──────────────┬───────────────┘
                                                           │
                    ┌──────────────────────────────────────┼──────────────────────┐
                    ▼                                      ▼                      ▼
        ┌──────────────────────┐            ┌──────────────────────┐   ┌──────────────────┐
        │ Dane.cs              │            │ Panel.cs / Grafik.cs │   │ Wyglad.cs        │
        │ Store   – pliki CSV  │ ◀────────▶ │ panel zarządzania,   │   │ Theme, kontrolki │
        │ Data    – indeks dni │            │ grafik, plan urlopów │   │ (Card, DateBox…) │
        │ Calc    – wyliczenia │            └──────────────────────┘   └──────────────────┘
        │ Reports – CSV / HTML │
        │ PlCalendar, Cfg, Pin │
        │ CloudBackup          │
        └──────────────────────┘
```

| Klasa | Odpowiedzialność |
|---|---|
| `Program` | start, blokada drugiej instancji (mutex), obsługa awarii i auto-restart |
| `Cfg` | odczyt i zapis `ustawienia.txt` |
| `Store` | odczyt/zapis plików CSV, wykrywanie kodowania (UTF-8 / Windows-1250), historia zmian, znacznik „żyję” |
| `Data` | jednorazowy odczyt wszystkich danych + indeksy (odbicia wg osoby i dnia, nieobecności, dni firmowe) |
| `Calc` | pary wejście–wyjście, `DayInfo` dnia, zakresy, podsumowania, urlop, Bradford, absencja |
| `PlCalendar` | polskie święta (z Wielkanocą), dni robocze, wymiar czasu pracy |
| `Reports` | raport CSV, karty ewidencji HTML (umowa o pracę / zlecenie) |
| `CloudBackup` | cotygodniowe archiwum ZIP do wskazanego folderu |
| `Pin` | PIN administratora (PBKDF2, 20 000 iteracji, sól), blokada po błędach |
| `KeyboardCapture` | przechwytywanie czytnika |
| `TrayApp` | ikonka, odbicia, przypomnienia, nieusypianie, autostart, wykrywanie przerw |
| `PanelForm` | panel: strony, tabele, wykres, licznik na żywo |
| `MonthSchedule`, `YearPlanner` | grafik miesięczny i roczny plan urlopów (rysowane w całości) |
| `Theme` i kontrolki | motywy, przyciski, karty, pola dat/godzin, listy rozwijane |

## Rozpoznawanie czytnika

Czytnik USB w trybie klawiatury wysyła cyfry numeru karty i Enter. Windows nie pozwala zwykłemu programowi
jednocześnie sprawdzić, z której klawiatury przyszedł znak, i go zablokować (wymagałoby to sterownika),
dlatego program rozpoznaje czytnik **po tempie**:

1. Globalny hak `WH_KEYBOARD_LL` działa w osobnym wątku z własną pętlą komunikatów
   (niezależnie od obciążenia interfejsu).
2. Każda cyfra jest na chwilę **wstrzymywana** w buforze.
3. Jeśli przyjdzie **≥ `min_cyfr` cyfr + Enter**, a każdy odstęp między zdarzeniami jest **< `max_odstep_ms`**
   (domyślnie 50 ms) – to karta. Bufor jest zjadany, numer trafia do `OnCard`.
4. W każdym innym przypadku (przerwa, inny klawisz, przytrzymanie klawisza) wstrzymane klawisze są
   **odtwarzane** przez `SendInput` w oryginalnej kolejności, oznaczone znacznikiem `dwExtraInfo`,
   żeby hak ich ponownie nie przechwycił.
5. Zdarzenia wstrzyknięte przez inne programy (np. menedżery haseł) są ignorowane.
6. Hak jest odświeżany co 5 minut oraz po wybudzeniu z uśpienia i odblokowaniu sesji
   (Windows potrafi go po cichu odpiąć).

Ograniczenia: hak nie działa na ekranie blokady / logowania; programy uruchomione jako administrator
mogą nie przyjąć odtworzonych klawiszy.

## Pliki danych

Folder: `%USERPROFILE%\Documents\EwidencjaCzasu` (lub inny – parametr `/dane:`).

| Plik | Zawartość |
|---|---|
| `pracownicy.csv` | `UID;Imię i nazwisko;Aktywny;Urlop roczny (dni);Urlop zaległy (dni);Forma zatrudnienia;Etat` |
| `odbicia.csv` | `Data;Godzina;UID;Pracownik;Typ;Źródło` – typ `WEJŚCIE`/`WYJŚCIE`, źródło `KARTA`/`RĘCZNIE`/`KARTA-POPRAWIONE` |
| `oczekujace.csv` | odbicia zapisane tymczasowo, gdy `odbicia.csv` był zablokowany (np. otwarty w Excelu) |
| `nieobecnosci.csv` | `Data;UID;Pracownik;Rodzaj;Uwagi` – kody z tabeli w instrukcji |
| `dni_wolne_firmowe.csv` | `Data;Rodzaj;Nazwa` – rodzaj `DW` (płatny) lub `WS` (za święto w sobotę) |
| `historia_zmian.csv` | `Kiedy;Operacja;Przed;Po;Użytkownik Windows` |
| `przerwy_w_dzialaniu.csv` | okresy, w których program nie działał |
| `ustawienia.txt` | ustawienia (klucz=wartość) |
| `pin.dat` | sól i skrót PIN-u (PBKDF2) |
| `ostatnio_aktywny.txt` | znacznik czasu aktualizowany co 30 s |
| `ostatnia_kopia.txt`, `awarie.txt`, `bledy.log` | stan kopii, licznik awarii, log błędów |
| `kopie\` | codzienne kopie plików CSV (60 dni) |
| `raporty\` | wygenerowane raporty CSV i karty HTML |

Zasady zapisu:
- dopisywanie odbić – do końca pliku, z zachowaniem kodowania pliku (Excel potrafi zapisać CSV w Windows-1250),
- pełny zapis (poprawki) – do pliku tymczasowego, potem podmiana; przy blokadzie – czytelny komunikat,
- porównania numerów kart ignorują zera wiodące.

## Ustawienia

`ustawienia.txt` – większość opcji jest też w oknie *Ustawienia*.

| Klucz | Domyślnie | Opis |
|---|---|---|
| `godzina_od`, `godzina_do` | `08:00`, `16:00` | godziny pracy (spóźnienia, wcześniejsze wyjścia) |
| `norma_godzin` | `8` | norma dobowa (pełny etat) |
| `tolerancja_min` | `5` | po ilu minutach liczy się spóźnienie |
| `noc_od`, `noc_do` | `22:00`, `06:00` | pora nocna |
| `przypomnienie`, `przypomnienie_o` | `1`, `16:30` | przypomnienie o odbiciu wyjścia |
| `nie_usypiaj`, `nie_usypiaj_od`, `nie_usypiaj_do` | `1`, `07:00`, `18:00` | blokada uśpienia w dni robocze |
| `folder_kopii`, `kopia_co_dni` | –, `7` | kopia ZIP poza komputer |
| `motyw`, `akcent` | `system`, `niebieski` | wygląd |
| `autostart` | `1` | program pilnuje wpisu w `HKCU\…\Run` |
| `blokada_sekund` | `60` | ignorowanie ponownego przyłożenia tej samej karty |
| `min_cyfr` | `8` | minimalna długość numeru karty |
| `max_odstep_ms` | `50` | maksymalny odstęp między znakami z czytnika |

## Wyliczenia czasu pracy

**Pary wejście–wyjście** (`Calc.PairDay`) – odbicia z jednego dnia łączone kolejno; brak pary oznacza
`BRAK WYJŚCIA` / `BRAK WEJŚCIA` (dziś otwarta para = „w pracy”).

**Wymiar czasu pracy** (`PlCalendar.NormBetween`, art. 130 KP):

```
wymiar = norma × etat × (dni pon–pt  −  święta przypadające w dniu innym niż niedziela)
```

Święta: 1.01, 6.01, Wielkanoc i Poniedziałek Wielkanocny, 1.05, 3.05, Zielone Świątki, Boże Ciało,
15.08, 1.11, 11.11, 24.12 (od 2025 r.), 25.12, 26.12. Wielkanoc liczona algorytmem Meeusa/Jonesa/Butchera.

**Dzień umowy o pracę** (`Calc.Day`):
- nieobecność usprawiedliwiona (rodzaj 1) – zalicza do normy brakujące godziny (obniża wymiar),
- praca zdalna / delegacja (rodzaj 0) – liczy się jako przepracowane,
- nadgodziny – czas ponad normę dobową (od pełnej minuty); w dzień wolny cały czas to praca w dzień wolny,
- spóźnienie – pierwsze wejście po `godzina_od + tolerancja`,
- wcześniejsze wyjście – ostatnie wyjście przed `godzina_do − tolerancja`, gdy norma nie została wykonana,
- pora nocna – część pracy w przedziale `noc_od–noc_do`.

**Saldo** = przepracowane + nieobecności usprawiedliwione − wymiar do dnia dzisiejszego.

**Brak obecności** – dzień roboczy bez odbić i nieobecności (od pierwszego wpisu danej osoby do wczoraj),
tylko dla umowy o pracę.

**Umowy cywilnoprawne** (zlecenie, dzieło, B2B) – liczony jest wyłącznie przepracowany czas,
bez normy, nadgodzin, spóźnień, nieobecności i braków obecności.

**Dzień wolny firmowy** – dla osób na umowie o pracę działa jak nieobecność (`DW` lub `WS`)
generowana w locie przez `Data.AbsenceOn` (nie jest zapisywana w `nieobecnosci.csv`).

## Wskaźniki absencji

Nieobecności nieplanowane: `CH`, `UŻ`, `OP`, `NN`. Dni wolne firmowe i weekendy są pomijane.

- **Wskaźnik absencji** = dni nieobecności nieplanowanej / dni robocze w okresie (do dziś) × 100%.
- **Współczynnik Bradforda** = S² × D z ostatnich 12 miesięcy, gdzie S to liczba odrębnych ciągów
  nieobecności (kolejne dni robocze tworzą jeden ciąg), a D – łączna liczba dni.
  Progi w interfejsie: < 50 zielony, 50–199 żółty, ≥ 200 czerwony.

## Odporność na awarie

| Mechanizm | Działanie |
|---|---|
| Natychmiastowy zapis | każde odbicie od razu na dysku |
| Zapis tymczasowy | gdy plik jest zablokowany – `oczekujace.csv`, przeniesienie przy kolejnym zapisie |
| Autostart | wpis w `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, poprawiany po przeniesieniu `.exe` |
| Znacznik „żyję” | co 30 s; po starcie / wybudzeniu program liczy przerwę i ostrzega, jeśli wypadła w godzinach pracy |
| Auto-restart | nieobsłużony wyjątek → `bledy.log` i ponowne uruchomienie (`/restart`); maks. 3 razy w 10 minut |
| Nieusypianie | `SetThreadExecutionState(ES_SYSTEM_REQUIRED)` w dni robocze w ustawionych godzinach |
| Kopie | codzienne lokalne + cotygodniowe ZIP (52 ostatnie) |

## Wygląd

`Theme` przechowuje paletę dla motywu jasnego i ciemnego oraz 8 kolorów akcentu.
`ThemedForm` stosuje motyw do okna (także ciemny pasek tytułu przez `DwmSetWindowAttribute`)
i odświeża je po zmianie motywu (`Theme.Changed`).

Własne kontrolki: `Card`, `StatCard`, `Banner`, `NavButton`, `DateBox` (pole daty ‹ › z kalendarzem),
`TimeBox`, `ThemedCombo`, `Swatch`, `BarChart`, `MonthSchedule`, `YearPlanner`.
Przyciski standardowe są rysowane od nowa: `Tag` = `primary` / `danger` / `ghost`,
pierwszy znak z czcionki *Segoe Fluent Icons* jest ikoną.

Siatka tabel jest rysowana przez program (`Theme.GridLines`), a kontrolki rysujące z wygładzaniem
zapisują i przywracają stan `Graphics` – inaczej linie tabel rysowały się niejednolicie.

## Kompilacja

```bat
zrodla\kompiluj.bat
```

lub ręcznie:

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:winexe /optimize+ ^
  /r:System.IO.Compression.dll /out:EwidencjaCzasu.exe ^
  zrodla\Dane.cs zrodla\Czytnik.cs zrodla\Aplikacja.cs zrodla\Panel.cs zrodla\Wyglad.cs zrodla\Grafik.cs
```

## Testy

```bat
testy\uruchom_testy.bat
```

Testy (`testy/Testy.cs`) działają na folderze tymczasowym i sprawdzają m.in.:
rozpoznawanie czytnika (różne tempa, kolejność klawiszy, przytrzymanie), kalendarz świąt i wymiar czasu pracy,
spóźnienia, nadgodziny, pracę w dni wolne, urlopy i pracę zdalną, braki obecności, poprawki i historię zmian,
raporty CSV i HTML, kopię ZIP, przerwy w działaniu, formy zatrudnienia i etat, dni wolne firmowe, Bradforda.

## Parametry wiersza poleceń

| Parametr | Działanie |
|---|---|
| `/dane:<folder>` | inny folder danych – osobna instancja, bez autostartu (np. do testów) |
| `/restart` | uruchomienie po awarii (czeka na zamknięcie poprzedniej instancji) |
| `/test-awaria` | celowa awaria po 3 s – sprawdzenie auto-restartu |

## Wskazówki dla rozwoju

- Kod musi kompilować się kompilatorem C# 5 z .NET Framework (patrz [Technologia](#technologia)).
- Nie ustawiać `DataGridView.FirstDisplayedScrollingRowIndex`, gdy tabela nie ma jeszcze wysokości –
  Windows Forms potrafi się wtedy zawiesić (`PanelForm.ScrollPunchesToEnd`).
- W `CellPainting` rysować tło przez `PaintCellBase` i nie zostawiać `SmoothingMode.AntiAlias` w obiekcie `Graphics`.
- Przy nowych polach w plikach CSV dopisywać kolumny na końcu i zachować zgodność wstecz (brak kolumny = wartość domyślna).
- Każda ręczna zmiana danych powinna trafiać do `Store.Audit`.

