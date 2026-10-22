# Skeleton Tracker

Mod do gry Valheim (BepInEx 5), który ułatwia śledzenie i dowodzenie szkieletami przyzwanymi
kosturem **Dead Raiser**. Mod działa wyłącznie po stronie klienta i dotyczy tylko szkieletów
przyzwanych przez Ciebie.

## Funkcje

- **Pinezki na minimapie** – każdy Twój szkielet ma na mapie pinezkę z ikoną trofeum szkieletu,
  aktualizowaną co 0,5 s. Na dużej mapie podpis pokazuje stan: „Szkielet” lub „Szkielet (czeka)”.
- **Licznik w HUD** – pod minimapą: liczba szkieletów oraz ile z nich podąża za Tobą, a ile czeka.
  Opcjonalnie lista szczegółowa: imię, broń (łuk / miecz / maczuga / topór), HP, odległość i stan.
- **Paski zdrowia** – stale widoczne nad głowami Twoich szkieletów (domyślnie do 40 m),
  kolorowane według HP: zielony > 60%, żółty > 30%, czerwony poniżej.
- **Poświata** – delikatne światło wokół Twoich szkieletów, żeby łatwo je odróżnić od innych.
- **Powiadomienia o śmierci** – komunikat na ekranie, gdy któryś z Twoich szkieletów zginie.
- **Komendy i skróty klawiszowe** – rozkaz „za mną”, „czekaj” i odwołanie dla wszystkich szkieletów
  naraz lub tylko dla tego, w którego celujesz.

## Wymagania

- Valheim (PC)
- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) (BepInEx 5)

## Instalacja

1. Zainstaluj BepInExPack Valheim zgodnie z jego instrukcją i uruchom grę raz, żeby BepInEx utworzył
   swoje foldery.
2. Skopiuj `ValheimSkeletonTracker.dll` do folderu:
   ```
   <folder Valheim>/BepInEx/plugins/SkeletonTracker/
   ```
3. Uruchom grę. Przy pierwszym starcie zostanie utworzony plik konfiguracyjny
   `BepInEx/config/michal.valheim.skeletontracker.cfg`.

Odinstalowanie: usuń folder `BepInEx/plugins/SkeletonTracker/` (i opcjonalnie plik `.cfg`).

## Sterowanie

### Skróty klawiszowe

| Skrót (domyślnie) | Działanie |
|---|---|
| Lewy Alt + F | szkielety idą za Tobą |
| Lewy Alt + G | szkielety czekają w miejscu |
| Lewy Alt + H | odwołanie szkieletów |
| Lewy Alt + J | lista szkieletów na czacie |

Gdy celujesz w jednego ze swoich szkieletów, skróty Follow / Stay / Dismiss działają tylko na niego
(podpowiedzi klawiszy pojawiają się w jego opisie). Skróty nie działają podczas pisania na czacie,
w menu, ekwipunku i na dużej mapie.

### Komendy czatu

Wpisz na czacie (Enter) lub w konsoli F5 (bez ukośnika):

| Komenda | Działanie |
|---|---|
| `/sk` lub `/sk list` | lista szkieletów: imię, broń, HP, odległość, stan |
| `/sk follow` | wszystkie szkielety idą za Tobą |
| `/sk stay` | wszystkie szkielety czekają w miejscu |
| `/sk dismiss` | odwołanie wszystkich szkieletów |

Komendy nie wymagają `devcommands`.

> Polecenia działają na szkielety w obszarze wczytanym przez grę (w pobliżu gracza).

## Konfiguracja

Plik `BepInEx/config/michal.valheim.skeletontracker.cfg`. Zmiany w pliku działają po restarcie gry;
z modem [ConfigurationManager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/)
(domyślnie F1) można je zmieniać w trakcie gry.

| Sekcja | Opcja | Domyślnie | Opis |
|---|---|---|---|
| `Hotkeys` | `Follow` | `F + LeftAlt` | skrót „za mną” |
| `Hotkeys` | `Stay` | `G + LeftAlt` | skrót „czekaj” |
| `Hotkeys` | `Dismiss` | `H + LeftAlt` | skrót odwołania |
| `Hotkeys` | `List` | `J + LeftAlt` | skrót listy na czacie |
| `HUD` | `ShowCounter` | `true` | licznik szkieletów pod minimapą |
| `HUD` | `ShowDetails` | `true` | szczegółowa lista szkieletów pod licznikiem |
| `HUD` | `CounterPositionY` | `0.3` | pionowa pozycja licznika (0 = góra, 1 = dół ekranu) |
| `HealthBars` | `AlwaysShow` | `true` | stale widoczne paski zdrowia |
| `HealthBars` | `MaxDistance` | `40` | zasięg widoczności pasków (10–100 m) |
| `HealthBars` | `ColorByHealth` | `true` | kolorowanie pasków według HP |
| `Glow` | `Enabled` | `true` | poświata wokół szkieletów |
| `Glow` | `Color` | `66D9FFFF` | kolor poświaty (RGBA, szesnastkowo) |
| `Glow` | `Intensity` | `1.2` | jasność poświaty (0–5) |
| `Glow` | `Range` | `3` | zasięg poświaty (0,5–10 m) |
| `Notifications` | `Death` | `true` | komunikat o śmierci szkieletu |

Skróty zapisuje się jako klawisz główny i modyfikatory, np. `F + LeftAlt`. Nazwy klawiszy to wartości
Unity `KeyCode` (np. `Keypad1`, `F7`, `Mouse3`); `None` wyłącza skrót.

## Budowanie ze źródeł

Wymagany .NET SDK (projekt targetuje `netstandard2.1`).

1. Skopiuj do folderu `lib/` biblioteki z gry i BepInEx (nie są częścią repozytorium):
   - z `<Valheim>/valheim_Data/Managed/`: `assembly_valheim.dll`, `assembly_utils.dll`,
     `assembly_guiutils.dll`, `Splatform.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`,
     `UnityEngine.TextRenderingModule.dll`, `UnityEngine.UI.dll`, `UnityEngine.UIModule.dll`,
     `Unity.TextMeshPro.dll`
   - z `<Valheim>/BepInEx/core/`: `BepInEx.dll`, `0Harmony.dll`
2. Zbuduj:
   ```
   dotnet build
   ```
   Po kompilacji DLL jest automatycznie kopiowany do folderu pluginów ustawionego we właściwości
   `ValheimPluginsDir` w `ValheimSkeletonTracker.csproj`. Inną ścieżkę można podać przy budowaniu:
   ```
   dotnet build -p:ValheimPluginsDir=/sciezka/do/Valheim/BepInEx/plugins/SkeletonTracker/
   ```
