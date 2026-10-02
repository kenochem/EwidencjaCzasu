# đź”§ Dokumentacja techniczna

[â† PowrĂłt do README](../README.md)

- [Technologia](#technologia)
- [Architektura](#architektura)
- [Rozpoznawanie czytnika](#rozpoznawanie-czytnika)
- [Pliki danych](#pliki-danych)
- [Ustawienia](#ustawienia)
- [Wyliczenia czasu pracy](#wyliczenia-czasu-pracy)
- [WskaĹşniki absencji](#wskaĹşniki-absencji)
- [OdpornoĹ›Ä‡ na awarie](#odpornoĹ›Ä‡-na-awarie)
- [WyglÄ…d](#wyglÄ…d)
- [Kompilacja](#kompilacja)
- [Testy](#testy)
- [Parametry wiersza poleceĹ„](#parametry-wiersza-poleceĹ„)
- [WskazĂłwki dla rozwoju](#wskazĂłwki-dla-rozwoju)

---

## Technologia

| | |
|---|---|
| JÄ™zyk | C# 5 (kompilator `csc.exe` z .NET Framework 4, wbudowany w Windows) |
| Interfejs | Windows Forms, wĹ‚asne kontrolki rysowane GDI+ |
| ZaleĹĽnoĹ›ci | brak â€“ tylko biblioteki systemowe (`System.Windows.Forms`, `System.Drawing`, `System.IO.Compression`) |
| Dane | pliki CSV (UTF-8 z BOM, separator `;`) â€“ czytelne w Excelu |
| Wynik | jeden plik `EwidencjaCzasu.exe` (~200 KB), bez instalatora |

> Kompilator .NET Framework obsĹ‚uguje tylko C# 5 â€“ w kodzie nie ma interpolacji `$"..."`, operatora `?.`
> ani skĹ‚adni `=>` dla wĹ‚aĹ›ciwoĹ›ci.

## Architektura

```
â”Śâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”   numer karty   â”Śâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ Czytnik.cs              â”‚ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â–¶ â”‚ Aplikacja.cs  (TrayApp)      â”‚
â”‚ KeyboardCapture         â”‚                 â”‚ OnCard â†’ wejĹ›cie / wyjĹ›cie   â”‚
â”‚ (osobny wÄ…tek + hak     â”‚                 â”‚ przypomnienia, kopie, PIN    â”‚
â”‚  WH_KEYBOARD_LL)        â”‚                 â”‚ dymki (Toast), okna dialogoweâ”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”                 â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
                                                           â”‚
                    â”Śâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”Ľâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
                    â–Ľ                                      â–Ľ                      â–Ľ
        â”Śâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”            â”Śâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”   â”Śâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
        â”‚ Dane.cs              â”‚            â”‚ Panel.cs / Grafik.cs â”‚   â”‚ Wyglad.cs        â”‚
        â”‚ Store   â€“ pliki CSV  â”‚ â—€â”€â”€â”€â”€â”€â”€â”€â”€â–¶ â”‚ panel zarzÄ…dzania,   â”‚   â”‚ Theme, kontrolki â”‚
        â”‚ Data    â€“ indeks dni â”‚            â”‚ grafik, plan urlopĂłw â”‚   â”‚ (Card, DateBoxâ€¦) â”‚
        â”‚ Calc    â€“ wyliczenia â”‚            â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”   â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
        â”‚ Reports â€“ CSV / HTML â”‚
        â”‚ PlCalendar, Cfg, Pin â”‚
        â”‚ CloudBackup          â”‚
        â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
```

| Klasa | OdpowiedzialnoĹ›Ä‡ |
|---|---|
| `Program` | start, blokada drugiej instancji (mutex), obsĹ‚uga awarii i auto-restart |
| `Cfg` | odczyt i zapis `ustawienia.txt` |
| `Store` | odczyt/zapis plikĂłw CSV, wykrywanie kodowania (UTF-8 / Windows-1250), historia zmian, znacznik â€žĹĽyjÄ™â€ť |
| `Data` | jednorazowy odczyt wszystkich danych + indeksy (odbicia wg osoby i dnia, nieobecnoĹ›ci, dni firmowe) |
| `Calc` | pary wejĹ›cieâ€“wyjĹ›cie, `DayInfo` dnia, zakresy, podsumowania, urlop, Bradford, absencja |
| `PlCalendar` | polskie Ĺ›wiÄ™ta (z WielkanocÄ…), dni robocze, wymiar czasu pracy |
| `Reports` | raport CSV, karty ewidencji HTML (umowa o pracÄ™ / zlecenie) |
| `CloudBackup` | cotygodniowe archiwum ZIP do wskazanego folderu |
| `Pin` | PIN administratora (PBKDF2, 20 000 iteracji, sĂłl), blokada po bĹ‚Ä™dach |
| `KeyboardCapture` | przechwytywanie czytnika |
| `TrayApp` | ikonka, odbicia, przypomnienia, nieusypianie, autostart, wykrywanie przerw |
| `PanelForm` | panel: strony, tabele, wykres, licznik na ĹĽywo |
| `MonthSchedule`, `YearPlanner` | grafik miesiÄ™czny i roczny plan urlopĂłw (rysowane w caĹ‚oĹ›ci) |
| `Theme` i kontrolki | motywy, przyciski, karty, pola dat/godzin, listy rozwijane |

## Rozpoznawanie czytnika

Czytnik USB w trybie klawiatury wysyĹ‚a cyfry numeru karty i Enter. Windows nie pozwala zwykĹ‚emu programowi
jednoczeĹ›nie sprawdziÄ‡, z ktĂłrej klawiatury przyszedĹ‚ znak, i go zablokowaÄ‡ (wymagaĹ‚oby to sterownika),
dlatego program rozpoznaje czytnik **po tempie**:

1. Globalny hak `WH_KEYBOARD_LL` dziaĹ‚a w osobnym wÄ…tku z wĹ‚asnÄ… pÄ™tlÄ… komunikatĂłw
   (niezaleĹĽnie od obciÄ…ĹĽenia interfejsu).
2. KaĹĽda cyfra jest na chwilÄ™ **wstrzymywana** w buforze.
3. JeĹ›li przyjdzie **â‰Ą `min_cyfr` cyfr + Enter**, a kaĹĽdy odstÄ™p miÄ™dzy zdarzeniami jest **< `max_odstep_ms`**
   (domyĹ›lnie 50 ms) â€“ to karta. Bufor jest zjadany, numer trafia do `OnCard`.
4. W kaĹĽdym innym przypadku (przerwa, inny klawisz, przytrzymanie klawisza) wstrzymane klawisze sÄ…
   **odtwarzane** przez `SendInput` w oryginalnej kolejnoĹ›ci, oznaczone znacznikiem `dwExtraInfo`,
   ĹĽeby hak ich ponownie nie przechwyciĹ‚.
5. Zdarzenia wstrzykniÄ™te przez inne programy (np. menedĹĽery haseĹ‚) sÄ… ignorowane.
6. Hak jest odĹ›wieĹĽany co 5 minut oraz po wybudzeniu z uĹ›pienia i odblokowaniu sesji
   (Windows potrafi go po cichu odpiÄ…Ä‡).

Ograniczenia: hak nie dziaĹ‚a na ekranie blokady / logowania; programy uruchomione jako administrator
mogÄ… nie przyjÄ…Ä‡ odtworzonych klawiszy.

## Pliki danych

Folder: `%USERPROFILE%\Documents\EwidencjaCzasu` (lub inny â€“ parametr `/dane:`).

| Plik | ZawartoĹ›Ä‡ |
|---|---|
| `pracownicy.csv` | `UID;ImiÄ™ i nazwisko;Aktywny;Urlop roczny (dni);Urlop zalegĹ‚y (dni);Forma zatrudnienia;Etat` |
| `odbicia.csv` | `Data;Godzina;UID;Pracownik;Typ;ĹąrĂłdĹ‚o` â€“ typ `WEJĹšCIE`/`WYJĹšCIE`, ĹşrĂłdĹ‚o `KARTA`/`RÄCZNIE`/`KARTA-POPRAWIONE` |
| `oczekujace.csv` | odbicia zapisane tymczasowo, gdy `odbicia.csv` byĹ‚ zablokowany (np. otwarty w Excelu) |
| `nieobecnosci.csv` | `Data;UID;Pracownik;Rodzaj;Uwagi` â€“ kody z tabeli w instrukcji |
| `dni_wolne_firmowe.csv` | `Data;Rodzaj;Nazwa` â€“ rodzaj `DW` (pĹ‚atny) lub `WS` (za Ĺ›wiÄ™to w sobotÄ™) |
| `historia_zmian.csv` | `Kiedy;Operacja;Przed;Po;UĹĽytkownik Windows` |
| `przerwy_w_dzialaniu.csv` | okresy, w ktĂłrych program nie dziaĹ‚aĹ‚ |
| `ustawienia.txt` | ustawienia (klucz=wartoĹ›Ä‡) |
| `pin.dat` | sĂłl i skrĂłt PIN-u (PBKDF2) |
| `ostatnio_aktywny.txt` | znacznik czasu aktualizowany co 30 s |
| `ostatnia_kopia.txt`, `awarie.txt`, `bledy.log` | stan kopii, licznik awarii, log bĹ‚Ä™dĂłw |
| `kopie\` | codzienne kopie plikĂłw CSV (60 dni) |
| `raporty\` | wygenerowane raporty CSV i karty HTML |

Zasady zapisu:
- dopisywanie odbiÄ‡ â€“ do koĹ„ca pliku, z zachowaniem kodowania pliku (Excel potrafi zapisaÄ‡ CSV w Windows-1250),
- peĹ‚ny zapis (poprawki) â€“ do pliku tymczasowego, potem podmiana; przy blokadzie â€“ czytelny komunikat,
- porĂłwnania numerĂłw kart ignorujÄ… zera wiodÄ…ce.

## Ustawienia

`ustawienia.txt` â€“ wiÄ™kszoĹ›Ä‡ opcji jest teĹĽ w oknie *Ustawienia*.

| Klucz | DomyĹ›lnie | Opis |
|---|---|---|
| `godzina_od`, `godzina_do` | `08:00`, `16:00` | godziny pracy (spĂłĹşnienia, wczeĹ›niejsze wyjĹ›cia) |
| `norma_godzin` | `8` | norma dobowa (peĹ‚ny etat) |
| `tolerancja_min` | `5` | po ilu minutach liczy siÄ™ spĂłĹşnienie |
| `noc_od`, `noc_do` | `22:00`, `06:00` | pora nocna |
| `przypomnienie`, `przypomnienie_o` | `1`, `16:30` | przypomnienie o odbiciu wyjĹ›cia |
| `nie_usypiaj`, `nie_usypiaj_od`, `nie_usypiaj_do` | `1`, `07:00`, `18:00` | blokada uĹ›pienia w dni robocze |
| `folder_kopii`, `kopia_co_dni` | â€“, `7` | kopia ZIP poza komputer |
| `motyw`, `akcent` | `system`, `niebieski` | wyglÄ…d |
| `autostart` | `1` | program pilnuje wpisu w `HKCU\â€¦\Run` |
| `blokada_sekund` | `60` | ignorowanie ponownego przyĹ‚oĹĽenia tej samej karty |
| `min_cyfr` | `8` | minimalna dĹ‚ugoĹ›Ä‡ numeru karty |
| `max_odstep_ms` | `50` | maksymalny odstÄ™p miÄ™dzy znakami z czytnika |

## Wyliczenia czasu pracy

**Pary wejĹ›cieâ€“wyjĹ›cie** (`Calc.PairDay`) â€“ odbicia z jednego dnia Ĺ‚Ä…czone kolejno; brak pary oznacza
`BRAK WYJĹšCIA` / `BRAK WEJĹšCIA` (dziĹ› otwarta para = â€žw pracyâ€ť).

**Wymiar czasu pracy** (`PlCalendar.NormBetween`, art. 130 KP):

```
wymiar = norma Ă— etat Ă— (dni ponâ€“pt  â’  Ĺ›wiÄ™ta przypadajÄ…ce w dniu innym niĹĽ niedziela)
```

ĹšwiÄ™ta: 1.01, 6.01, Wielkanoc i PoniedziaĹ‚ek Wielkanocny, 1.05, 3.05, Zielone ĹšwiÄ…tki, BoĹĽe CiaĹ‚o,
15.08, 1.11, 11.11, 24.12 (od 2025 r.), 25.12, 26.12. Wielkanoc liczona algorytmem Meeusa/Jonesa/Butchera.

**DzieĹ„ umowy o pracÄ™** (`Calc.Day`):
- nieobecnoĹ›Ä‡ usprawiedliwiona (rodzaj 1) â€“ zalicza do normy brakujÄ…ce godziny (obniĹĽa wymiar),
- praca zdalna / delegacja (rodzaj 0) â€“ liczy siÄ™ jako przepracowane,
- nadgodziny â€“ czas ponad normÄ™ dobowÄ… (od peĹ‚nej minuty); w dzieĹ„ wolny caĹ‚y czas to praca w dzieĹ„ wolny,
- spĂłĹşnienie â€“ pierwsze wejĹ›cie po `godzina_od + tolerancja`,
- wczeĹ›niejsze wyjĹ›cie â€“ ostatnie wyjĹ›cie przed `godzina_do â’ tolerancja`, gdy norma nie zostaĹ‚a wykonana,
- pora nocna â€“ czÄ™Ĺ›Ä‡ pracy w przedziale `noc_odâ€“noc_do`.

**Saldo** = przepracowane + nieobecnoĹ›ci usprawiedliwione â’ wymiar do dnia dzisiejszego.

**Brak obecnoĹ›ci** â€“ dzieĹ„ roboczy bez odbiÄ‡ i nieobecnoĹ›ci (od pierwszego wpisu danej osoby do wczoraj),
tylko dla umowy o pracÄ™.

**Umowy cywilnoprawne** (zlecenie, dzieĹ‚o, B2B) â€“ liczony jest wyĹ‚Ä…cznie przepracowany czas,
bez normy, nadgodzin, spĂłĹşnieĹ„, nieobecnoĹ›ci i brakĂłw obecnoĹ›ci.

**DzieĹ„ wolny firmowy** â€“ dla osĂłb na umowie o pracÄ™ dziaĹ‚a jak nieobecnoĹ›Ä‡ (`DW` lub `WS`)
generowana w locie przez `Data.AbsenceOn` (nie jest zapisywana w `nieobecnosci.csv`).

## WskaĹşniki absencji

NieobecnoĹ›ci nieplanowane: `CH`, `UĹ»`, `OP`, `NN`. Dni wolne firmowe i weekendy sÄ… pomijane.

- **WskaĹşnik absencji** = dni nieobecnoĹ›ci nieplanowanej / dni robocze w okresie (do dziĹ›) Ă— 100%.
- **WspĂłĹ‚czynnik Bradforda** = SÂ˛ Ă— D z ostatnich 12 miesiÄ™cy, gdzie S to liczba odrÄ™bnych ciÄ…gĂłw
  nieobecnoĹ›ci (kolejne dni robocze tworzÄ… jeden ciÄ…g), a D â€“ Ĺ‚Ä…czna liczba dni.
  Progi w interfejsie: < 50 zielony, 50â€“199 ĹĽĂłĹ‚ty, â‰Ą 200 czerwony.

## OdpornoĹ›Ä‡ na awarie

| Mechanizm | DziaĹ‚anie |
|---|---|
| Natychmiastowy zapis | kaĹĽde odbicie od razu na dysku |
| Zapis tymczasowy | gdy plik jest zablokowany â€“ `oczekujace.csv`, przeniesienie przy kolejnym zapisie |
| Autostart | wpis w `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, poprawiany po przeniesieniu `.exe` |
| Znacznik â€žĹĽyjÄ™â€ť | co 30 s; po starcie / wybudzeniu program liczy przerwÄ™ i ostrzega, jeĹ›li wypadĹ‚a w godzinach pracy |
| Auto-restart | nieobsĹ‚uĹĽony wyjÄ…tek â†’ `bledy.log` i ponowne uruchomienie (`/restart`); maks. 3 razy w 10 minut |
| Nieusypianie | `SetThreadExecutionState(ES_SYSTEM_REQUIRED)` w dni robocze w ustawionych godzinach |
| Kopie | codzienne lokalne + cotygodniowe ZIP (52 ostatnie) |

## WyglÄ…d

`Theme` przechowuje paletÄ™ dla motywu jasnego i ciemnego oraz 8 kolorĂłw akcentu.
`ThemedForm` stosuje motyw do okna (takĹĽe ciemny pasek tytuĹ‚u przez `DwmSetWindowAttribute`)
i odĹ›wieĹĽa je po zmianie motywu (`Theme.Changed`).

WĹ‚asne kontrolki: `Card`, `StatCard`, `Banner`, `NavButton`, `DateBox` (pole daty â€ą â€ş z kalendarzem),
`TimeBox`, `ThemedCombo`, `Swatch`, `BarChart`, `MonthSchedule`, `YearPlanner`.
Przyciski standardowe sÄ… rysowane od nowa: `Tag` = `primary` / `danger` / `ghost`,
pierwszy znak z czcionki *Segoe Fluent Icons* jest ikonÄ….

Siatka tabel jest rysowana przez program (`Theme.GridLines`), a kontrolki rysujÄ…ce z wygĹ‚adzaniem
zapisujÄ… i przywracajÄ… stan `Graphics` â€“ inaczej linie tabel rysowaĹ‚y siÄ™ niejednolicie.

## Kompilacja

```bat
zrodla\kompiluj.bat
```

lub rÄ™cznie:

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:winexe /optimize+ ^
  /r:System.IO.Compression.dll /out:EwidencjaCzasu.exe ^
  zrodla\Dane.cs zrodla\Czytnik.cs zrodla\Aplikacja.cs zrodla\Panel.cs zrodla\Wyglad.cs zrodla\Grafik.cs
```

## Testy

```bat
testy\uruchom_testy.bat
```

Testy (`testy/Testy.cs`) dziaĹ‚ajÄ… na folderze tymczasowym i sprawdzajÄ… m.in.:
rozpoznawanie czytnika (rĂłĹĽne tempa, kolejnoĹ›Ä‡ klawiszy, przytrzymanie), kalendarz Ĺ›wiÄ…t i wymiar czasu pracy,
spĂłĹşnienia, nadgodziny, pracÄ™ w dni wolne, urlopy i pracÄ™ zdalnÄ…, braki obecnoĹ›ci, poprawki i historiÄ™ zmian,
raporty CSV i HTML, kopiÄ™ ZIP, przerwy w dziaĹ‚aniu, formy zatrudnienia i etat, dni wolne firmowe, Bradforda.

## Parametry wiersza poleceĹ„

| Parametr | DziaĹ‚anie |
|---|---|
| `/dane:<folder>` | inny folder danych â€“ osobna instancja, bez autostartu (np. do testĂłw) |
| `/restart` | uruchomienie po awarii (czeka na zamkniÄ™cie poprzedniej instancji) |
| `/test-awaria` | celowa awaria po 3 s â€“ sprawdzenie auto-restartu |

## WskazĂłwki dla rozwoju

- Kod musi kompilowaÄ‡ siÄ™ kompilatorem C# 5 z .NET Framework (patrz [Technologia](#technologia)).
- Nie ustawiaÄ‡ `DataGridView.FirstDisplayedScrollingRowIndex`, gdy tabela nie ma jeszcze wysokoĹ›ci â€“
  Windows Forms potrafi siÄ™ wtedy zawiesiÄ‡ (`PanelForm.ScrollPunchesToEnd`).
- W `CellPainting` rysowaÄ‡ tĹ‚o przez `PaintCellBase` i nie zostawiaÄ‡ `SmoothingMode.AntiAlias` w obiekcie `Graphics`.
- Przy nowych polach w plikach CSV dopisywaÄ‡ kolumny na koĹ„cu i zachowaÄ‡ zgodnoĹ›Ä‡ wstecz (brak kolumny = wartoĹ›Ä‡ domyĹ›lna).
- KaĹĽda rÄ™czna zmiana danych powinna trafiaÄ‡ do `Store.Audit`.

