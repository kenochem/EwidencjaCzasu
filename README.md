<div align="center">

# đź•’ Ewidencja Czasu Pracy

**Rejestracja czasu pracy kartami RFID/NFC dla maĹ‚ego biura â€“ prosto, lokalnie, zgodnie z Kodeksem pracy.**

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.x-512BD4?logo=dotnet&logoColor=white)
![JÄ™zyk](https://img.shields.io/badge/j%C4%99zyk-polski-DC143C)
![Bez instalacji](https://img.shields.io/badge/instalacja-niepotrzebna-success)

<img src="docs/img/obecnosc.png" alt="Panel â€“ obecnoĹ›Ä‡ dziĹ›" width="900">

</div>

---

## Spis treĹ›ci

- [Co to jest](#co-to-jest)
- [NajwaĹĽniejsze funkcje](#najwaĹĽniejsze-funkcje)
- [Zrzuty ekranu](#zrzuty-ekranu)
- [Wymagania](#wymagania)
- [Szybki start](#szybki-start)
- [Jak dziaĹ‚a czytnik](#jak-dziaĹ‚a-czytnik)
- [Dane i prywatnoĹ›Ä‡](#dane-i-prywatnoĹ›Ä‡)
- [Struktura projektu](#struktura-projektu)
- [Kompilacja i testy](#kompilacja-i-testy)
- [Dokumentacja](#dokumentacja)

---

## Co to jest

**Ewidencja Czasu Pracy** to program dla Windows, ktĂłry zamienia zwykĹ‚y czytnik kart RFID/NFC na USB
w system rejestracji czasu pracy (RCP). Pracownik przykĹ‚ada kartÄ™ przy wejĹ›ciu i wyjĹ›ciu, a program:

- rejestruje godziny i pokazuje duĹĽe, czytelne powitanie lub poĹĽegnanie,
- liczy czas pracy, normy, nadgodziny i saldo,
- prowadzi urlopy, L4 i inne nieobecnoĹ›ci,
- przygotowuje **kartÄ™ ewidencji czasu pracy** (art. 149 KP) i raport do Excela.

Program dziaĹ‚a w tle na zwykĹ‚ym komputerze biurowym, **bez serwera, abonamentu i internetu**.
Wszystkie dane zostajÄ… na dysku firmy.

## NajwaĹĽniejsze funkcje

| | Funkcja | Opis |
|---|---|---|
| đź’ł | **Odbicia kartÄ…** | Czytnik USB w trybie klawiatury. Numer karty nie trafia do okna, w ktĂłrym pracujesz â€“ program rozpoznaje czytnik po â€žmaszynowymâ€ť tempie pisania. |
| đź‘‹ | **Powitanie / poĹĽegnanie** | DuĹĽy, kolorowy komunikat: zielony przy wejĹ›ciu, grafitowy przy wyjĹ›ciu, z czasem pracy z danego dnia. |
| đź“Š | **ObecnoĹ›Ä‡ na ĹĽywo** | Kto jest w biurze, od kiedy, ile juĹĽ przepracowaĹ‚ (licznik z sekundami) i ile zostaĹ‚o do normy. |
| đź—“ď¸Ź | **Grafik miesiÄ™czny** | CaĹ‚y zespĂłĹ‚ i caĹ‚y miesiÄ…c na jednym ekranie: obecnoĹ›ci, urlopy, L4, braki odbiÄ‡. |
| đźŹ–ď¸Ź | **Urlopy i nieobecnoĹ›ci** | Urlop wypoczynkowy i na ĹĽÄ…danie, L4, opieka, praca zdalna, delegacja i inne. Licznik pozostaĹ‚ego urlopu. |
| đź“… | **Roczny plan urlopĂłw** | Kto i kiedy jest nieobecny w caĹ‚ym roku â€“ ostrzega, gdy nieobecnych jest 3 lub wiÄ™cej osĂłb naraz. |
| đźŹ˘ | **Dni wolne firmowe** | Jeden dzieĹ„ wolny dla wszystkich (np. za Ĺ›wiÄ™to w sobotÄ™, firmowa Wigilia). |
| âš–ď¸Ź | **Normy i nadgodziny** | Wymiar czasu pracy z polskim kalendarzem Ĺ›wiÄ…t, nadgodziny, praca w dni wolne, pora nocna, spĂłĹşnienia. |
| đź“ť | **Formy zatrudnienia** | Umowa o pracÄ™ (rĂłwnieĹĽ czÄ™Ĺ›Ä‡ etatu), umowa zlecenie, o dzieĹ‚o, B2B. Przy umowach cywilnych liczy siÄ™ tylko przepracowany czas. |
| đź–¨ď¸Ź | **Karta ewidencji (PDF)** | Gotowa do podpisu karta ewidencji czasu pracy, a dla zleceniobiorcĂłw â€“ ewidencja godzin wykonywania zlecenia. |
| đź“ | **Statystyki** | Ĺšrednie godziny, spĂłĹşnienia, saldo, wskaĹşnik absencji i **wspĂłĹ‚czynnik Bradforda**, wykres godzin. |
| đź”” | **Przypomnienia** | O odbiciu wyjĹ›cia o wybranej godzinie, o brakujÄ…cym wyjĹ›ciu z poprzedniego dnia. |
| âśŹď¸Ź | **Poprawki z historiÄ…** | RÄ™czne dodawanie i poprawianie odbiÄ‡; kaĹĽda zmiana trafia do historii (kto, kiedy, co byĹ‚o, co jest). |
| đź” | **PIN administratora** | Panel, zmiany i zamkniÄ™cie programu chronione PIN-em. Panel blokuje siÄ™ sam po 10 minutach. |
| đź’ľ | **Kopie zapasowe** | Codzienna kopia lokalna i cotygodniowa kopia `.zip` np. na Dysk Google. |
| đź›ˇď¸Ź | **OdpornoĹ›Ä‡** | Autostart z Windows, wykrywanie przerw w dziaĹ‚aniu, automatyczny restart po bĹ‚Ä™dzie, komputer nie usypia w godzinach pracy. |
| đźŽ¨ | **WyglÄ…d** | Motyw jasny, ciemny lub zgodny z Windows i 8 kolorĂłw akcentu. |

## Zrzuty ekranu

> Na zrzutach sÄ… wyĹ‚Ä…cznie **przykĹ‚adowe, zmyĹ›lone dane**.

<table>
<tr>
<td width="50%"><b>Grafik miesiÄ™czny</b><br><img src="docs/img/grafik.png" alt="Grafik miesiÄ™czny"></td>
<td width="50%"><b>Roczny plan urlopĂłw</b><br><img src="docs/img/plan-urlopow.png" alt="Plan urlopĂłw"></td>
</tr>
<tr>
<td><b>Podsumowanie miesiÄ…ca (motyw jasny)</b><br><img src="docs/img/podsumowanie-jasny.png" alt="Podsumowanie miesiÄ…ca"></td>
<td><b>Statystyki i wspĂłĹ‚czynnik Bradforda</b><br><img src="docs/img/statystyki.png" alt="Statystyki"></td>
</tr>
<tr>
<td><b>Pracownicy i karty</b><br><img src="docs/img/pracownicy.png" alt="Pracownicy i karty"></td>
<td><b>Ustawienia</b><br><img src="docs/img/ustawienia.png" alt="Ustawienia"></td>
</tr>
<tr>
<td><b>Powiadomienia przy odbiciu</b><br><img src="docs/img/powiadomienia.png" alt="Powiadomienia"></td>
<td><b>Karta ewidencji czasu pracy</b><br><img src="docs/img/karta-ewidencji.png" alt="Karta ewidencji"></td>
</tr>
</table>

## Wymagania

- **Windows 10 lub 11** (wbudowany .NET Framework 4.x â€“ nic nie trzeba instalowaÄ‡).
- **Czytnik RFID/NFC na USB dziaĹ‚ajÄ…cy jak klawiatura** â€“ po przyĹ‚oĹĽeniu karty wpisuje numer i naciska Enter
  (Ĺ‚atwo sprawdziÄ‡ w Notatniku). Program domyĹ›lnie oczekuje co najmniej 8 cyfr.
- Karty pasujÄ…ce do czytnika (np. 125 kHz EM4100 lub 13,56 MHz MIFARE â€“ zaleĹĽnie od czytnika).

## Szybki start

1. **Zbuduj program** â€“ uruchom [`zrodla/kompiluj.bat`](zrodla/kompiluj.bat).
   Kompilator jest czÄ™Ĺ›ciÄ… Windows, powstanie plik `EwidencjaCzasu.exe`.
2. **Uruchom `EwidencjaCzasu.exe`** â€“ przy zegarku pojawi siÄ™ ikonka, program dziaĹ‚a w tle
   i sam doda siÄ™ do autostartu.
3. **Ustaw PIN** â€“ przy pierwszym otwarciu panelu (dwuklik na ikonce) program poprosi o PIN administratora.
4. **Dodaj karty** â€“ przyĹ‚ĂłĹĽ nowÄ… kartÄ™ do czytnika, podaj PIN i wpisz imiÄ™ i nazwisko.
   Albo otwĂłrz *Pracownicy i karty* i przykĹ‚adaj karty po kolei.
5. **Ustaw formÄ™ zatrudnienia i urlop** w oknie *Pracownicy i karty*.
6. **Ustaw kopiÄ™ zapasowÄ…** w *Ustawieniach* (np. folder na Dysku Google).

Gotowe â€“ od teraz kaĹĽde przyĹ‚oĹĽenie karty to wejĹ›cie lub wyjĹ›cie.

SzczegĂłĹ‚owy opis wszystkich ekranĂłw: **[Instrukcja obsĹ‚ugi](docs/INSTRUKCJA.md)**.

## Jak dziaĹ‚a czytnik

Tani czytnik USB â€žudajeâ€ť klawiaturÄ™: wpisuje numer karty i Enter tam, gdzie akurat jest kursor.
Program instaluje w Windows globalny hak klawiatury i **rozpoznaje czytnik po tempie pisania** â€“
czytnik wpisuje caĹ‚y numer w kilkadziesiÄ…t milisekund (poniĹĽej 50 ms na znak), czĹ‚owiek pisze duĹĽo wolniej.

- Cyfry wpisane przez czytnik sÄ… przechwytywane â€“ nie trafiajÄ… do Worda, Excela ani przeglÄ…darki.
- Cyfry wpisane przez czĹ‚owieka sÄ… natychmiast odtwarzane w oryginalnej kolejnoĹ›ci (opĂłĹşnienie ok. 0,05 s â€“ niezauwaĹĽalne).
- PrĂłg tempa i minimalnÄ… liczbÄ™ cyfr moĹĽna zmieniÄ‡ w `ustawienia.txt`.

WiÄ™cej: [Dokumentacja techniczna](docs/TECHNICZNE.md#rozpoznawanie-czytnika).

## Dane i prywatnoĹ›Ä‡

- Dane sÄ… zapisywane lokalnie w folderze **`Dokumenty\EwidencjaCzasu`** w prostych plikach CSV
  (moĹĽna je otworzyÄ‡ w Excelu).
- Program **niczego nie wysyĹ‚a do internetu**. Jedyne kopiowanie poza komputer to kopia zapasowa
  do folderu, ktĂłry sam wskaĹĽesz (np. Dysk Google).
- Karty zbliĹĽeniowe to nie biometria â€“ wystarczy poinformowaÄ‡ pracownikĂłw o systemie
  (cel, zakres danych, okres przechowywania) i dopisaÄ‡ go do regulaminu pracy.
- Repozytorium zawiera **wyĹ‚Ä…cznie kod i dokumentacjÄ™** â€“ plik `.gitignore` wyklucza dane, kopie zapasowe i pliki programu.

## Struktura projektu

```
EwidencjaCzasu/
â”śâ”€â”€ zrodla/                 kod ĹşrĂłdĹ‚owy (C#, Windows Forms)
â”‚   â”śâ”€â”€ Dane.cs             start programu, ustawienia, pliki danych, kalendarz Ĺ›wiÄ…t,
â”‚   â”‚                       wyliczenia (normy, nadgodziny, absencja), raporty, kopie, PIN
â”‚   â”śâ”€â”€ Czytnik.cs          przechwytywanie czytnika (globalny hak klawiatury)
â”‚   â”śâ”€â”€ Aplikacja.cs        ikonka przy zegarku, obsĹ‚uga odbiÄ‡, przypomnienia, okna dialogowe
â”‚   â”śâ”€â”€ Panel.cs            panel zarzÄ…dzania (strony, tabele, wykres)
â”‚   â”śâ”€â”€ Grafik.cs           grafik miesiÄ™czny, roczny plan urlopĂłw, dni wolne firmowe
â”‚   â”śâ”€â”€ Wyglad.cs           motywy, kolory, przyciski, karty, pola dat, nawigacja
â”‚   â””â”€â”€ kompiluj.bat        kompilacja do EwidencjaCzasu.exe
â”śâ”€â”€ testy/
â”‚   â”śâ”€â”€ Testy.cs            testy logiki (czytnik, kalendarz, normy, raporty, kopieâ€¦)
â”‚   â””â”€â”€ uruchom_testy.bat
â””â”€â”€ docs/
    â”śâ”€â”€ INSTRUKCJA.md       instrukcja obsĹ‚ugi
    â”śâ”€â”€ TECHNICZNE.md       dokumentacja techniczna
    â””â”€â”€ img/                zrzuty ekranu
```

## Kompilacja i testy

Nie potrzeba Visual Studio â€“ wystarczy kompilator C# wbudowany w Windows (`csc.exe` z .NET Framework 4).

```bat
zrodla\kompiluj.bat        :: buduje EwidencjaCzasu.exe w folderze gĹ‚Ăłwnym
testy\uruchom_testy.bat    :: kompiluje i uruchamia testy
```

Przed kompilacjÄ… zamknij dziaĹ‚ajÄ…cy program (prawy klik na ikonce â†’ *Zamknij program*).

## Dokumentacja

- đź“ **[Instrukcja obsĹ‚ugi](docs/INSTRUKCJA.md)** â€“ codzienna praca, wszystkie ekrany, najczÄ™stsze pytania.
- đź”§ **[Dokumentacja techniczna](docs/TECHNICZNE.md)** â€“ architektura, pliki danych, ustawienia, algorytmy, rozwĂłj.

---

<div align="center">
<sub>Kenochem Â· Ewidencja Czasu Pracy</sub>
</div>

