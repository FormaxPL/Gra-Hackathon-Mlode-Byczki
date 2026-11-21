# Menu Backrooms, losowe mapy i losowe awarie: projekt

Data: 2026-11-21
Status: do akceptacji

## Po co

Gra „The Backrooms: Isolation” ma przy każdym uruchomieniu wyglądać inaczej i od pierwszej chwili budzić niepokój. Autor wybrał:

- **kilka gotowych map** losowanych przy starcie,
- **losowe awarie prądu oprócz fabularnej**,
- **menu główne w stylu Backrooms** z potworem, który losowo zmienia zachowanie.

## Cele

1. **Menu główne jako osobna scena `MainMenu`:**
   - żółty korytarz Backrooms (tapeta, sufit z płyt, świetlówki, mgła AERO, jeśli jest),
   - z jednym narożnikiem i kratką wentylacyjną,
   - kamera powoli dryfuje, a nad nią tytuł **THE BACKROOMS: ISOLATION**,
   - pozycje: Rozpocznij grę, Ustawienia, Wyjdź.
2. **Potwór w menu co 12–25 s losuje jedno zachowanie** (bez powtórzenia tego samego dwa razy z rzędu):
   - **Zza rogu:** wychyla się zza narożnika, patrzy w kamerę 3–5 s, chowa się.
   - **Z wentylacji:** wypełza z kratki, schodzi na podłogę, przechodzi korytarzem i znika za rogiem.
   - **W głębi:** stoi nieruchomo daleko w mgle, z ledwo widocznymi oczami, a po chwili znika, gdy migają świetlówki.
3. **Losowe mapy:**
   - generator buduje **3 układy** (`TestLevel_A/B/C`, ziarna 12345 i dwa kolejne), każdy z wypalonym GI i okluzją,
   - „Rozpocznij grę” w menu losuje jeden z nich,
   - restart po śmierci (R) wczytuje **ten sam** układ, bo punkt kontrolny musi pasować.
4. **Losowe awarie:**
   - w fazie eksploracji co **3–6 min** zdarza się awaria na **20–45 s**, z tym samym migotaniem, dźwiękiem i przełączeniem GI co fabularna,
   - nie w trakcie przerywników, pościgu, czytania notatki ani awarii fabularnej,
   - fabularna awaria (po radiu) działa bez zmian.

## Poza zakresem

- Generowanie mapy w trakcie gry (wybrany wariant to gotowe mapy).
- Nowy model potwora: osobna praca. Menu używa obecnego `entity.fbx`.

## Jak to zbudować

- **Generator (`LevelBuilder`):**
  - nowe menu **Poza Zasiegiem → Zbuduj wszystkie mapy i menu**: buduje `MainMenu` oraz `TestLevel_A/B/C`, a każdą mapę od razu wypala,
  - całość trwa ok. 6–8 min,
  - zestaw wypalania i dane GI dostają nazwę od sceny, np. `PZ_GI_BakingSet_TestLevel_A`, żeby mapy się nie nadpisywały,
  - Build Settings: `MainMenu` jako pierwsza, potem trzy mapy.
- **Scena `MainMenu`** budowana kodem, z tych samych materiałów co strefa Backrooms. Zawiera kamerę, oświetlenie (świetlówki, część migająca), mgłę, potwora (`entity.fbx` + `MonsterAnimationDriver`) oraz nowy skrypt **`MenuStalkerDirector`**, który:
  - losuje i odgrywa 3 zachowania po stałych ścieżkach (punkty: róg, kratka, głębia),
  - ustawia tryb animacji (`Walk`, `Idle`, `LockerWait` do wychylania),
  - obraca głowę w stronę kamery.
- **`MenuSystem`:**
  - w scenie `MainMenu` pokazuje menu główne,
  - „Rozpocznij grę” wczytuje losową mapę (`MapPicker.PickRandom`, bez rozróżniania 1–3),
  - w mapie menu główne się nie otwiera, a gra startuje od intro,
  - pauza → „Menu główne” wczytuje `MainMenu`.
- **Losowe awarie:** nowy komponent **`RandomBlackouts`** obok `Blackout`:
  - odlicza losowy czas,
  - sprawdza warunki (`StoryDirector.CurrentPhase == Explore`, brak `CutsceneLock`, `!Blackout.Active`, nie czyta),
  - wywołuje `Blackout.Instance.Begin(czas, showHint: false)`.
- **Repozytorium:** każda mapa to ok. 25 MB sceny i 13 MB danych GI. Trzy mapy dają ok. +80 MB, a największy plik ma <30 MB, czyli poniżej limitu GitHuba.

## Jak sprawdzimy

1. Testy EditMode:
   - losowanie map (zawsze jedna z trzech, rozkład w miarę równy),
   - harmonogram awarii (odstęp 180–360 s, długość 20–45 s),
   - warunki blokujące awarię,
   - losowanie zachowań potwora bez powtórzeń z rzędu.
2. W trybie Play:
   - menu startuje jako pierwsza scena, potwór odgrywa każde z 3 zachowań (klatki),
   - „Rozpocznij grę” wczytuje mapę, a restart (R) tę samą,
   - wymuszona losowa awaria gasi i zapala światła razem z GI.
3. Pomiar czasu klatki menu w 1080p (cel <10 ms) i brak wyjątków.

## Moje decyzje

- 3 mapy, a nie więcej, ze względu na rozmiar repozytorium. Liczbę łatwo zmienić w generatorze.
- Losowe awarie są krótsze niż fabularna i bez podpowiedzi na ekranie, żeby zaskakiwały.
- Stary `TestLevel.unity` zostaje usunięty (zastępuje go `TestLevel_A`, z tym samym ziarnem 12345).
