# 💻 Terminal na osobnym laptopie

[← Powrót do README](../README.md)

Najpewniejsza konfiguracja: **laptop przy wejściu** z podłączonym czytnikiem rejestruje odbicia przez całą dobę,
a **Twój komputer** służy tylko do podglądu i poprawek (tryb *panel*). Twój komputer możesz wtedy usypiać,
wyłączać i blokować – odbicia i tak się zapisują.

```
   ┌──────────────── laptop przy wejściu (TERMINAL) ───────────────┐          ┌──── Twój komputer (PANEL) ────┐
   │  czytnik USB ──▶ EwidencjaCzasu.exe                            │   sieć   │  EwidencjaCzasu.exe /panel    │
   │                  Dokumenty\EwidencjaCzasu  ◀── udostępniony ───┼─────────▶│  obecność, grafik, poprawki,  │
   │                  (dane, kopie zapasowe)        \\LAPTOP\...    │          │  raporty, karty ewidencji     │
   └────────────────────────────────────────────────────────────────┘          └───────────────────────────────┘
```

Dlaczego laptop:
- **bateria = zasilacz awaryjny** – zanik prądu lub wyłączona listwa nie przerywają pracy,
- **nikt na nim nie pracuje** – nie jest usypiany ani blokowany,
- **automatyczne logowanie** – po nocnym restarcie Windows Update program sam wraca do działania.

---

## Spis kroków

1. [Przygotowanie laptopa](#1-przygotowanie-laptopa)
2. [Pobranie programu z GitHuba](#2-pobranie-programu-z-githuba)
3. [Instalacja terminala](#3-instalacja-terminala)
4. [Automatyczne logowanie](#4-automatyczne-logowanie)
5. [Przeniesienie danych ze starego komputera](#5-przeniesienie-danych-ze-starego-komputera)
6. [Panel na Twoim komputerze](#6-panel-na-twoim-komputerze)
7. [Sprawdzenie](#7-sprawdzenie)
8. [Aktualizacje programu](#8-aktualizacje-programu)
9. [Rozwiązywanie problemów](#9-rozwiązywanie-problemów)

---

## 1. Przygotowanie laptopa

- Windows 10 lub 11, **konto z uprawnieniami administratora i hasłem**
  (hasło jest potrzebne, żeby Twój komputer mógł łączyć się z danymi przez sieć).
- Laptop **podłączony do zasilacza**, w **tej samej sieci** co Twój komputer (kabel lub Wi-Fi biura).
- Warto zmienić nazwę komputera na czytelną, np. `TERMINAL`:
  *Ustawienia → System → Informacje → Zmień nazwę tego komputera* (wymaga restartu).

## 2. Pobranie programu z GitHuba

Program najlepiej umieścić w folderze **`C:\EwidencjaCzasu`** (krótka ścieżka bez spacji).

**Sposób A – z Gitem (polecany, łatwe aktualizacje).** Zainstaluj [Git for Windows](https://git-scm.com/download/win), potem w wierszu poleceń:

```bat
git clone https://github.com/kenochem/EwidencjaCzasu.git C:\EwidencjaCzasu
```

**Sposób B – bez Gita.** Wejdź na https://github.com/kenochem/EwidencjaCzasu → zielony przycisk **Code** → **Download ZIP**,
rozpakuj zawartość do `C:\EwidencjaCzasu` (tak, żeby był tam folder `zrodla`, `instalacja` itd.).

## 3. Instalacja terminala

W folderze `C:\EwidencjaCzasu\instalacja` kliknij **prawym przyciskiem** plik
**`1_zainstaluj_terminal.bat`** → **Uruchom jako administrator**.

Skrypt:

| Krok | Co ustawia |
|---|---|
| 1 | kompiluje program (kompilator jest w Windows – nic nie trzeba instalować) |
| 2 | **zasilanie**: nigdy nie usypiaj i nie hibernuj (także na baterii), **zamknięcie klapy nic nie robi**, porty USB nie są usypiane (czytnik zawsze aktywny), ekran gaśnie po 10 min |
| 3 | **Windows Update**: godziny aktywnego użytkowania 6:00–23:00 (restarty po aktualizacjach tylko w nocy) |
| 4 | tworzy folder danych `Dokumenty\EwidencjaCzasu` i **udostępnia go w sieci** jako `\\NAZWA-LAPTOPA\EwidencjaCzasu` (tylko dla konta laptopa) |
| 5 | włącza udostępnianie plików w zaporze **dla sieci prywatnej**; pyta, czy ustawić obecną sieć jako prywatną (tak – jeśli to sieć firmowa) |
| 6 | uruchamia program – sam doda się do autostartu |

Na końcu skrypt wypisze **nazwę laptopa i użytkownika** – zapisz je, przydadzą się w kroku 6.

## 4. Automatyczne logowanie

Bez tego po restarcie (np. po aktualizacji w nocy) laptop zatrzyma się na ekranie logowania i karty nie będą działać.

1. Naciśnij **Win+R**, wpisz `netplwiz`, Enter.
2. Odznacz **„Aby używać tego komputera, użytkownicy muszą wprowadzić nazwę użytkownika i hasło”**.
3. **OK** → podaj hasło konta dwa razy.

> Jeśli tego pola nie ma: *Ustawienia → Konta → Opcje logowania* → wyłącz
> **„Wymagaj logowania przy użyciu funkcji Windows Hello dla kont Microsoft”**, potem ponów krok 1.

Dodatkowo: *Ustawienia → Konta → Opcje logowania → „Jeśli nie było Cię przy komputerze…”* → **Nigdy**
oraz **nie blokuj** laptopa (Win+L) – przy zablokowanym ekranie Windows nie przekazuje programowi danych z czytnika.

## 5. Przeniesienie danych ze starego komputera

Historia odbić, pracownicy, nieobecności i PIN przechodzą na laptopa w całości.

1. **Na starym komputerze** uruchom `instalacja\2_eksport_danych_ze_starego_komputera.bat`.
   Program zostanie zamknięty, jego autostart wyłączony, a na Pulpicie powstanie **`EwidencjaCzasu_dane.zip`**.
2. Przenieś plik ZIP na **Pulpit laptopa** (pendrive, dysk sieciowy, Dysk Google).
3. **Na laptopie** uruchom `instalacja\3_import_danych_na_laptopie.bat`.
   Dotychczasowe dane laptopa (jeśli były) trafią do kopii `..._przed_importem_….zip`.
4. W panelu na laptopie ustaw **Ustawienia → Kopia zapasowa** (np. folder na Dysku Google zainstalowanym na laptopie).

## 6. Panel na Twoim komputerze

Na **swoim** komputerze (w folderze z repozytorium) uruchom
**`instalacja\4_podlacz_panel_na_moim_komputerze.bat`** (bez uprawnień administratora).

Skrypt zapyta o nazwę laptopa, użytkownika i hasło (zapamięta je w Menedżerze poświadczeń Windows), sprawdzi dostęp,
wyłączy stary terminal na tym komputerze i utworzy skrót **„Ewidencja Czasu – panel”** na Pulpicie i w autostarcie.

W trybie panelu program:
- **nie obsługuje czytnika**, nie robi kopii, nie pilnuje zasilania i nie pokazuje przypomnień – to robi laptop,
- pokazuje i poprawia dane laptopa na żywo (obecność, grafik, odbicia, urlopy, raporty, karty ewidencji),
- korzysta z tego samego **PIN-u** co laptop.

Jeśli laptop jest wyłączony lub poza siecią, panel pokaże komunikat z przyciskiem *Ponów*.

## 7. Sprawdzenie

- [ ] Przyłożenie karty do czytnika przy laptopie pokazuje zielone powitanie.
- [ ] Na Twoim komputerze panel pokazuje to odbicie w *Obecność dziś* (odświeżanie co 30 s).
- [ ] Po restarcie laptopa (*Start → Uruchom ponownie*) system sam się loguje, a program startuje sam.
- [ ] Po zamknięciu klapy laptop dalej działa (ikonka programu, odbicia się zapisują).
- [ ] Odłączenie zasilacza na chwilę nie przerywa pracy.

## 8. Aktualizacje programu

Gdy na GitHubie pojawi się nowa wersja, uruchom **`instalacja\aktualizuj.bat`** – na laptopie i na swoim komputerze.
Skrypt pobierze kod (`git pull`), skompiluje program i uruchomi go ponownie w tym samym trybie.
Dane nie są zmieniane – leżą w osobnym folderze.

Bez Gita: pobierz ZIP z GitHuba, rozpakuj na folder programu (zastępując pliki) i uruchom `aktualizuj.bat`.

## 9. Rozwiązywanie problemów

| Problem | Rozwiązanie |
|---|---|
| Panel: „Brak dostępu do danych na terminalu” | Laptop włączony? W tej samej sieci? W eksploratorze wpisz `\\NAZWA-LAPTOPA\EwidencjaCzasu` – jeśli pyta o hasło, uruchom ponownie skrypt 4. |
| Laptopa nie widać w sieci | Na laptopie: *Ustawienia → Sieć i Internet → Właściwości* → profil **Prywatny**. Uruchom ponownie skrypt 1 (krok 5). |
| Po restarcie laptop stoi na ekranie logowania | Krok 4 (automatyczne logowanie). |
| Karta wpisuje się jako cyfry w otwarte okno | Czytnik jest wolny – w `Dokumenty\EwidencjaCzasu\ustawienia.txt` zwiększ `max_odstep_ms` do 80, uruchom program ponownie. |
| Czytnik przestaje działać po pewnym czasie | Skrypt 1 wyłącza usypianie USB; sprawdź też w *Menedżerze urządzeń → Kontrolery USB → Główny koncentrator USB → Zarządzanie energią* – odznacz „Zezwalaj na wyłączanie tego urządzenia”. |
| Komunikaty o przerwach w działaniu | Program pokazuje, kiedy nie działał. Na laptopie przy prawidłowej konfiguracji pojawią się tylko po restartach z aktualizacji (w nocy, poza godzinami pracy – bez ostrzeżenia). |
